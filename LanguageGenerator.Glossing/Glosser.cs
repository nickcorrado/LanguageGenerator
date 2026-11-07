using System.Text.RegularExpressions;

namespace LanguageGenerator.Glossing;

/// <summary>
/// Turns English text into a clause file, with a language model doing the glossing and ClauseFile
/// checking it.
/// </summary>
//tokens go only where they're needed:
//- sentences go in batches, so the instructions are paid for once per batch, not per sentence
//- a sentence already glossed in an earlier output file is kept, so editing a text and glossing it
//  again sends only what changed
//- a gloss that doesn't parse is sent back alone with the parser's error, not the whole batch
//
//Each sentence's clauses follow a "# > " comment quoting it, which is how a later run finds them.
//If a request fails partway, what was glossed before it is still returned, and the rest is left
//out, so the next run picks up where this one stopped
public sealed partial class Glosser(IChatModel model, int batchSize = 20, int retries = 2, int contextSentences = 3)
{
    public const string SentenceMarker = "# > ";
    public const string CouldNotGloss = "# couldn't gloss";

    //Missing counts the sentences left out because a request failed with Error
    public record Result(string Text, TokenUsage Usage, int Sentences, int Reused, int Unexpressible, int Failed, int Missing, Exception? Error);

    public async Task<Result> Gloss(string english, string? previousOutput = null, Action<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var sentences = Sentences(english);
        var reused = previousOutput is null ? [] : Blocks(previousOutput);
        var glossed = new Dictionary<string, string>();
        var usage = TokenUsage.None;

        //each distinct sentence still to gloss, in order, with the sentences before it for context
        var pending = sentences.Select((s, i) => (Sentence: s, Index: i))
            .Where(p => !reused.ContainsKey(p.Sentence))
            .DistinctBy(p => p.Sentence)
            .ToList();
        IReadOnlyList<string> ContextFor(int index) => [.. sentences.Take(index).TakeLast(contextSentences)];

        Exception? interrupted = null;
        try
        {
            foreach (var batch in pending.Chunk(batchSize))
            {
                progress?.Invoke($"Glossing sentences {batch[0].Index + 1}-{batch[^1].Index + 1} of {sentences.Count}...");
                usage += await GlossBatch([.. batch.Select(b => b.Sentence)], ContextFor(batch[0].Index), glossed, progress, cancellationToken);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            interrupted = e;
        }

        var blocks = sentences.Where(s => reused.ContainsKey(s) || glossed.ContainsKey(s))
            .Select(s => (Sentence: s, Block: reused.TryGetValue(s, out var r) ? r : glossed[s])).ToList();
        var text = string.Join("\n\n", blocks.Select(b => $"{SentenceMarker}{b.Sentence}\n{b.Block}")) + "\n";
        return new Result(text, usage, sentences.Count,
            Reused: sentences.Count(reused.ContainsKey),
            Unexpressible: blocks.Count(b => b.Block.StartsWith(GlossPrompt.CantExpress)),
            Failed: blocks.Count(b => b.Block.StartsWith(CouldNotGloss)),
            Missing: sentences.Count - blocks.Count,
            interrupted);
    }

    //Error is what stopped it, and then Nouns is only what was already known
    public record NounResult(IReadOnlyDictionary<string, SemanticClass> Nouns, TokenUsage Usage, int Asked, Exception? Error);

    //a noun file for these clauses: everything in known, which is kept so that a word's class can't
    //change from one run to the next, and the nouns it doesn't have yet. A noun the model leaves out
    //gets what the generator knows of it, which for most is that it's a thing
    public async Task<NounResult> ClassifyNouns(IEnumerable<ClauseFile.Entry> clauses, IReadOnlyDictionary<string, SemanticClass> known,
        Action<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var nouns = new Dictionary<string, SemanticClass>(known);
        var usage = TokenUsage.None;
        var pending = ClauseFile.Nouns(clauses).Where(n => !known.ContainsKey(n.Noun)).ToList();
        if (pending.Count == 0)
        {
            return new NounResult(nouns, usage, 0, null);
        }

        progress?.Invoke($"Sorting {pending.Count} nouns...");
        try
        {
            var request = GlossPrompt.Nouns(pending);
            for (int round = 0; ; round++)
            {
                var reply = await model.Complete(GlossPrompt.NounSystem, request, cancellationToken);
                usage += reply.Usage;
                var error = ReadNouns(reply.Text, pending, out var classified);
                if (error is null)
                {
                    foreach (var (noun, _) in pending)
                    {
                        nouns[noun] = classified.TryGetValue(noun, out var meaning) ? meaning : Semantics.Of(noun);
                    }
                    return new NounResult(nouns, usage, pending.Count, null);
                }
                if (round >= retries)
                {
                    throw new ChatModelException($"The nouns couldn't be sorted: {error}");
                }
                progress?.Invoke("Retrying the nouns...");
                request = GlossPrompt.NounRetry(pending, reply.Text, error);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new NounResult(known, usage, pending.Count, e);
        }
    }

    //what's wrong with a reply listing nouns, if anything
    private static string? ReadNouns(string reply, IReadOnlyList<(string Noun, string Translation)> asked, out Dictionary<string, SemanticClass> nouns)
    {
        nouns = [];
        var lines = reply.ReplaceLineEndings("\n").Split('\n').Where(l => !l.TrimStart().StartsWith("```"));
        var text = string.Join("\n", lines);
        if (text.Trim() == "NONE")
        {
            return null;
        }
        try
        {
            nouns = NounFile.Parse(text);
        }
        catch (ClauseFormatException e)
        {
            return e.Message;
        }
        return nouns.Keys.FirstOrDefault(n => !asked.Any(a => a.Noun == n)) is { } stray ? $"'{stray}' wasn't one of the nouns." : null;
    }

    //one batch's glosses, into glossed, with the ones that don't parse retried on their own and
    //left as comments if they still don't. Returns what it cost
    private async Task<TokenUsage> GlossBatch(IReadOnlyList<string> batch, IReadOnlyList<string> context, Dictionary<string, string> glossed,
        Action<string>? progress, CancellationToken cancellationToken)
    {
        var reply = await model.Complete(GlossPrompt.System, GlossPrompt.Batch(context, batch), cancellationToken);
        var usage = reply.Usage;
        var failures = Keep(batch, Numbered(reply.Text), glossed);

        for (int round = 0; round < retries && failures.Count > 0; round++)
        {
            progress?.Invoke($"Retrying {failures.Count} that didn't parse...");
            var retry = await model.Complete(GlossPrompt.System, GlossPrompt.Retry(context, failures), cancellationToken);
            usage += retry.Usage;
            failures = Keep([.. failures.Select(f => f.Sentence)], Numbered(retry.Text), glossed);
        }
        //what still doesn't parse is left as a comment, so the file still does
        foreach (var failure in failures)
        {
            glossed[failure.Sentence] = $"{CouldNotGloss} ({failure.Error.ReplaceLineEndings(" ")})";
        }
        return usage;
    }

    //the numbered answers that parse go into glossed, and the rest come back with what's wrong
    private static List<(string Sentence, string Attempt, string Error)> Keep(IReadOnlyList<string> asked,
        Dictionary<int, string> answers, Dictionary<string, string> glossed)
    {
        var failures = new List<(string Sentence, string Attempt, string Error)>();
        foreach (var (sentence, i) in asked.Select((s, i) => (s, i)))
        {
            var attempt = answers.GetValueOrDefault(i + 1, "");
            if (Check(attempt) is { } error)
            {
                failures.Add((sentence, attempt, error));
            }
            else
            {
                glossed[sentence] = attempt;
            }
        }
        return failures;
    }

    //null if a sentence's lines are a usable gloss: clauses that parse, or a note that it can't be
    //expressed yet, and otherwise what's wrong
    public static string? Check(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return "There was no gloss for this sentence.";
        }
        if (block.Split('\n').Any(l => l.TrimStart().StartsWith(SentenceMarker.TrimEnd())))
        {
            return $"Lines can't start with '{SentenceMarker.TrimEnd()}'.";
        }
        try
        {
            var entries = ClauseFile.Parse(block);
            return entries.Count > 0 || block.TrimStart().StartsWith(GlossPrompt.CantExpress) ? null
                : $"There were no clauses, and no '{GlossPrompt.CantExpress}' line either.";
        }
        catch (ClauseFormatException e)
        {
            return e.Message;
        }
    }

    //a reply's lines by the number in brackets before them, trimmed of blank lines at either end
    public static Dictionary<int, string> Numbered(string reply)
    {
        var answers = new Dictionary<int, string>();
        int? current = null;
        var lines = new List<string>();
        void Flush()
        {
            if (current is { } n && !answers.ContainsKey(n))
            {
                answers[n] = string.Join("\n", lines.SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse());
            }
            lines.Clear();
        }
        foreach (var line in reply.ReplaceLineEndings("\n").Split('\n'))
        {
            var number = NumberLine().Match(line);
            if (number.Success)
            {
                Flush();
                current = int.Parse(number.Groups[1].Value);
            }
            else if (current is not null && !line.TrimStart().StartsWith("```"))
            {
                lines.Add(line.TrimEnd());
            }
        }
        Flush();
        return answers;
    }

    //the blocks of an earlier output, by the sentence each glosses, leaving out the ones that failed
    public static Dictionary<string, string> Blocks(string output)
    {
        var blocks = new Dictionary<string, string>();
        string? sentence = null;
        var lines = new List<string>();
        void Flush()
        {
            var block = string.Join("\n", lines).Trim('\n');
            if (sentence is not null && !block.StartsWith(CouldNotGloss) && Check(block) is null)
            {
                blocks.TryAdd(sentence, block);
            }
            lines.Clear();
        }
        foreach (var line in output.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith(SentenceMarker))
            {
                Flush();
                sentence = line[SentenceMarker.Length..];
            }
            else
            {
                lines.Add(line.TrimEnd());
            }
        }
        Flush();
        return blocks;
    }

    //a text's sentences, split after ., ! or ? (and any closing quote or bracket) where the next
    //starts with a capital, a digit or an opening quote. Paragraph breaks always end one, and other
    //line breaks are spaces. Abbreviations like 'Mr.' will split wrongly; models cope with that
    public static List<string> Sentences(string text) =>
        [.. ParagraphBreak().Split(text.ReplaceLineEndings("\n"))
            .Select(p => Whitespace().Replace(p, " ").Trim())
            .Where(p => p.Length > 0)
            .SelectMany(p => SentenceBreak().Split(p))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)];

    [GeneratedRegex(@"^\s*\[(\d+)\]\s*$")]
    private static partial Regex NumberLine();
    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"(?<=[.!?][""'”’)\]]*)\s+(?=[\p{Lu}\d""'“‘(\[])")]
    private static partial Regex SentenceBreak();
}
