namespace LanguageGenerator.Glossing;

//what a model is told about the clause format. The roles and glosses come from ClauseFile's own
//tables, so the prompt keeps up as the format grows; the system prompt is the same for every
//request, so that providers that cache can reuse it, and everything that varies is in the user
//message
public static class GlossPrompt
{
    public const string CantExpress = "# can't express yet";

    public static string System { get; } = BuildSystem();

    private static string Values<T>(Func<T, bool>? keep = null) where T : Grammeme =>
        string.Join(", ", ClauseFile.ValuesOf<T>().Where(v => keep?.Invoke(v) ?? true).Select(v => $"{v.Gloss} ({v.Name.ToLowerInvariant()})"));

    private static string BuildSystem()
    {
        var roles = ClauseFile.RoleNames.Where(r => r is not ("noun" or "adjective"));
        return $"""
            You turn English into clause files for a language generator, which translates them into
            invented languages. Each clause is a line of plain English saying what it means, then an
            indented line glossing it: the verb, then its arguments as role:word, separated by two spaces.

              the hunter saw the wolves
                see.PST.PFV  subject:hunter  object:wolf.PL

            Words are English meanings in dictionary form, lowercase: wolf, not wolves; see, not saw.
            Features follow a word after periods, in Leipzig glossing style.
            - Verb features: {Values<Tense>()}; {Values<Aspect>()}; {Values<Polarity>()}.
            - Noun features: {Values<Number>()}.
            - Unmarked defaults: present, imperfective, affirmative, singular. Leave defaults out.

            Pronouns are a person and number instead of a word, as in subject:1SG or object:3PL, then
            optionally .INCL or .EXCL for 'we' (whether it includes the hearer), and a gender for a
            third person pronoun: {Values<Gender>(g => g == Gender.Masculine || g == Gender.Feminine || g == Gender.Neuter)}.
            Give 'he', 'she' and 'it' their gender, and give 'they' one when context shows what it
            refers to. 'You' is 2SG or 2PL, as context shows.

            Roles: {string.Join(", ", roles)}. A recipient is who something is given or said to, a
            goal where something goes, a source where it comes from, a companion who it's done with,
            and an instrument what it's done with. A noun with adjectives goes in parentheses, noun
            first, as in object:(wolf.PL big gray). So does a number, in digits from 1 to {NumeralSystem.Largest}, as in
            subject:(wolf 3 gray); it gives the noun its number, so leave that out. So does a
            possessor, a noun or pronoun, as in object:(house possessor:king) for 'the king's house' or
            companion:(dog.PL possessor:3SG.M) for 'his dogs'. Roles not supported yet: {string.Join(", ", ClauseFile.UnsupportedRoleNames)}.

            The verb be says what its subject is, with noun: or adjective: in place of an object, or
            where it is, with location:, and nothing else:

              the gate was locked
                be.PST  subject:gate  adjective:locked
              the queen is in the house
                be  subject:queen  location:house

            Rules:
            - One clause per verb. Split a sentence with several verbs into several clauses, each with
              its own English line, as in 'the hunter saw the wolves' and 'he ran'.
            - Articles, demonstratives, adverbs and conjunctions have no gloss: leave them
              out of the gloss line when the meaning mostly survives. The English line is natural
              English saying what the gloss does, so 'the' stays, but a dropped adverb goes.
            - Paraphrase into what the format can say when that keeps the meaning, as with 'went' for
              walk.PST, or 'lives in' for be with location:.
            - When a sentence can't be said even loosely, as with a question, a command, a relative
              clause or a comparison, write instead one line: {CantExpress} (what's missing): the sentence.
              This is better than a gloss that changes what the sentence means.

            Reply with each numbered sentence's lines after its number in brackets, and nothing else:

              [1]
              the two dogs slept in the old house
                sleep.PST  subject:(dog 2)  location:(house old)
              [2]
              {CantExpress} (question): Where did they go?
            """;
    }

    //the sentences to gloss, numbered, after a few before them for context, like who 'she' is
    public static string Batch(IReadOnlyList<string> context, IReadOnlyList<string> sentences)
    {
        var lines = new List<string>();
        if (context.Count > 0)
        {
            lines.Add("Earlier in the text, for context only:");
            lines.AddRange(context.Select(c => $"  {c}"));
            lines.Add("");
        }
        lines.Add("Gloss these:");
        lines.AddRange(sentences.Select((s, i) => $"[{i + 1}] {s}"));
        return string.Join("\n", lines);
    }

    //sentences whose glosses didn't parse, with what was wrong, to try again
    public static string Retry(IReadOnlyList<string> context, IReadOnlyList<(string Sentence, string Attempt, string Error)> failures)
    {
        var lines = new List<string>();
        if (context.Count > 0)
        {
            lines.Add("Earlier in the text, for context only:");
            lines.AddRange(context.Select(c => $"  {c}"));
            lines.Add("");
        }
        lines.Add("These glosses didn't parse. Fix each one, or say it can't be expressed yet.");
        foreach (var (failure, i) in failures.Select((f, i) => (f, i)))
        {
            lines.Add("");
            lines.Add($"[{i + 1}] {failure.Sentence}");
            lines.Add("Your gloss:");
            lines.AddRange(failure.Attempt.Split('\n').Select(l => $"  {l}"));
            lines.Add($"The parser said: {failure.Error}");
        }
        return string.Join("\n", lines);
    }
}
