using Anthropic.Exceptions;

namespace LanguageGenerator.Glossing;

//gloss <english.txt> [output] [options]: writes a clause file for translate. The output defaults to
//the input's name with .clauses.txt, and if it's already there, the sentences it glosses are kept
public static class GlossCommand
{
    public const string Usage = """
        Usage: LanguageGenerator gloss <english.txt> [output] [--provider anthropic|openai] [--model name]
                 [--base-url url] [--effort low|medium|high|max] [--batch sentences]
          anthropic (the default) uses Claude, with ANTHROPIC_API_KEY or an ant auth login profile.
          openai is any OpenAI-compatible server: OpenAI with OPENAI_API_KEY, or a local one like
          Ollama with --base-url http://localhost:11434/v1. It needs --model.
        """;

    private const string DefaultAnthropicModel = "claude-opus-5-5";

    public static async Task<int> Run(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>();
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith("--"))
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
        if (options.Keys.Except(["provider", "model", "base-url", "effort", "batch"]).FirstOrDefault() is { } unknown)
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
        string english;
        string? previous;
        try
        {
            english = await File.ReadAllTextAsync(input);
            previous = File.Exists(output) ? await File.ReadAllTextAsync(output) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail($"{e.Message}", usage: false);
        }

        var result = await new Glosser(model, batchSize).Gloss(english, previous, Console.Error.WriteLine);
        var header = $"# Glossed from {Path.GetFileName(input)} by {model.Name}. Glossing it again keeps the sentences here that\n# still parse, so edit those freely; delete one to have it glossed again.\n\n";
        await File.WriteAllTextAsync(output, header + result.Text);
        (model as IDisposable)?.Dispose();

        Console.Error.WriteLine($"Wrote {output}: {result.Sentences} sentences, {result.Reused} kept from before, " +
            $"{result.Unexpressible} that can't be expressed yet, {result.Failed} that didn't parse.");
        Console.Error.WriteLine($"Used {result.Usage}.");
        if (result.Error is { } error)
        {
            var reason = error is ChatModelException or HttpRequestException or AnthropicException ? error.Message : error.ToString();
            Console.Error.WriteLine($"Stopped with {result.Missing} sentences left, which glossing again will pick up: {reason}");
            return 1;
        }
        return 0;
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
