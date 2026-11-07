using Anthropic.Exceptions;
using LanguageGenerator.Glossing;

namespace LanguageGenerator.Cli;

/// <summary>
/// The gloss command, which writes a clause file for translate from a file of English.
/// </summary>
//the output defaults to the input's name with .clauses.txt, and if it's already there, the
//sentences it glosses are kept, unless --again says to gloss them all afresh, as after the format
//learns something new
public static class GlossCommand
{
    public const string Usage = """
        Usage: LanguageGenerator.Cli gloss <english.txt> [output] [--provider anthropic|openai] [--model name]
                 [--base-url url] [--effort low|medium|high|max] [--batch sentences] [--nouns file] [--again]
          Sentences already glossed in the output are kept; --again glosses them all afresh.
          Nouns go in a list of their own, by default beside the output as <name>.nouns.txt. Its
          lines are always kept, so a noun's class only changes if you change it.
          anthropic (the default) uses Claude, with ANTHROPIC_API_KEY or an ant auth login profile.
          openai is any OpenAI-compatible server: OpenAI with OPENAI_API_KEY, or a local one like
          Ollama with --base-url http://localhost:11434/v1. It needs --model.
        """;

    private const string DefaultAnthropicModel = "claude-opus-5-5";
    private const string NounHeader = "# What each noun names: M, F, HUM, ANIM or THING. Gender follows from this in languages that\n# have it. Edit freely: glossing again keeps every line here.\n\n";

    public static async Task<int> Run(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>();
        var again = false;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "--again")
            {
                again = true;
            }
            else if (args[i].StartsWith("--"))
            {
                if (i + 1 >= args.Count)
                {
                    return Fail($"{args[i]} needs a value.");
                }
                options[args[i][2..]] = args[++i];
            }
            else
            {
                positional.Add(args[i]);
            }
        }
        if (positional.Count is 0 or > 2)
        {
            return Fail(positional.Count == 0 ? "" : "gloss takes an input file and at most one output file.");
        }
        if (options.Keys.Except(["provider", "model", "base-url", "effort", "batch", "nouns"]).FirstOrDefault() is { } unknown)
        {
            return Fail($"--{unknown} isn't an option.");
        }
        if (options.TryGetValue("effort", out var effort) && effort is not ("low" or "medium" or "high" or "max"))
        {
            return Fail($"'{effort}' isn't an effort.");
        }
        var batchSize = 20;
        if (options.TryGetValue("batch", out var batch) && (!int.TryParse(batch, out batchSize) || batchSize < 1))
        {
            return Fail($"--batch needs a positive number, not '{batch}'.");
        }

        IChatModel model;
        switch (options.GetValueOrDefault("provider", "anthropic"))
        {
            case "anthropic":
                model = new AnthropicChatModel(options.GetValueOrDefault("model", DefaultAnthropicModel), options.GetValueOrDefault("effort"));
                break;
            case "openai" when !options.ContainsKey("model"):
                return Fail("--provider openai needs a --model.");
            case "openai":
                var baseUrl = options.GetValueOrDefault("base-url", "https://api.openai.com/v1");
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                {
                    return Fail($"'{baseUrl}' isn't a URL.");
                }
                model = new OpenAICompatibleChatModel(uri, options["model"], Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                break;
            case var other:
                return Fail($"'{other}' isn't a provider. Use anthropic or openai.");
        }

        var input = positional[0];
        var output = positional.ElementAtOrDefault(1) ?? Path.Combine(Path.GetDirectoryName(input) ?? "", Path.GetFileNameWithoutExtension(input) + ".clauses.txt");
        var nounsPath = options.GetValueOrDefault("nouns") ?? NounsPathFor(output);
        string english;
        string? previous;
        Dictionary<string, SemanticClass> knownNouns;
        try
        {
            english = await File.ReadAllTextAsync(input);
            previous = !again && File.Exists(output) ? await File.ReadAllTextAsync(output) : null;
            knownNouns = File.Exists(nounsPath) ? NounFile.Parse(await File.ReadAllTextAsync(nounsPath)) : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ClauseFormatException)
        {
            return Fail($"{e.Message}", usage: false);
        }

        var glosser = new Glosser(model, batchSize);
        var result = await glosser.Gloss(english, previous, Console.Error.WriteLine);
        var header = $"# Glossed from {Path.GetFileName(input)} by {model.Name}. Glossing it again keeps the sentences here that\n# still parse, so edit those freely; delete one to have it glossed again.\n\n";
        await File.WriteAllTextAsync(output, header + result.Text);

        Console.Error.WriteLine($"Wrote {output}: {result.Sentences} sentences, {result.Reused} kept from before, " +
            $"{result.Unexpressible} that can't be expressed yet, {result.Failed} that didn't parse.");
        Console.Error.WriteLine($"Used {result.Usage}.");
        if (result.Error is { } error)
        {
            var reason = error is ChatModelException or HttpRequestException or AnthropicException ? error.Message : error.ToString();
            Console.Error.WriteLine($"Stopped with {result.Missing} sentences left, which glossing again will pick up: {reason}");
            (model as IDisposable)?.Dispose();
            return 1;
        }

        var nouns = await glosser.ClassifyNouns(ClauseFile.Parse(result.Text), knownNouns, Console.Error.WriteLine);
        (model as IDisposable)?.Dispose();
        if (nouns.Error is { } nounError)
        {
            Console.Error.WriteLine($"The clauses are saved but the new nouns aren't, which glossing again will pick up: {nounError.Message}");
            return 1;
        }
        await File.WriteAllTextAsync(nounsPath, NounHeader + NounFile.Write(nouns.Nouns));
        Console.Error.WriteLine($"Wrote {nounsPath}: {nouns.Nouns.Count} nouns, {nouns.Asked} of them new, using {nouns.Usage}.");
        return 0;
    }

    //where a clause file's nouns go, or are looked for: hollow.clauses.txt has hollow.nouns.txt
    public static string NounsPathFor(string clauses)
    {
        var name = Path.GetFileName(clauses);
        var stem = name.EndsWith(".clauses.txt") ? name[..^".clauses.txt".Length] : Path.GetFileNameWithoutExtension(name);
        return Path.Combine(Path.GetDirectoryName(clauses) ?? "", stem + ".nouns.txt");
    }

    private static int Fail(string message, bool usage = true)
    {
        if (message.Length > 0)
        {
            Console.Error.WriteLine(message);
        }
        if (usage)
        {
            Console.Error.WriteLine(Usage);
        }
        return 1;
    }
}
