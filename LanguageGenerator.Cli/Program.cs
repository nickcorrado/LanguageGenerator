using LanguageGenerator;
using LanguageGenerator.Cli;

//pass a seed to regenerate the same language; without one, a fresh seed is picked and printed.
//"translate <file> [seed]" translates a file of clauses (see ClauseFile) instead of describing
//the language, with the noun file (see NounFile) that --nouns names or else the one beside it, and
//"gloss <english.txt>" writes such files from English (see Glosser). Either of the first two takes
//--spec with a JSON file fixing some of the language's settings (see SpecFile)
if (args is ["gloss", .. var glossArguments])
{
    return await GlossCommand.Run(glossArguments);
}
const string Usage = "Usage: LanguageGenerator.Cli [seed] [--spec file], or LanguageGenerator.Cli translate <file> [seed] [--nouns file] [--spec file], where the seed is an integer, or LanguageGenerator.Cli gloss <english.txt>.";
//an option and the file after it, taken out of the arguments
string? Option(string name)
{
    if (Array.IndexOf(args, name) is not (>= 0 and var at))
    {
        return null;
    }
    var value = at + 1 < args.Length ? args[at + 1] : null;
    args = [.. args[..at], .. args[Math.Min(at + 2, args.Length)..]];
    return value ?? "";
}
var nounsArgument = Option("--nouns");
var specArgument = Option("--spec");
if (nounsArgument == "" || specArgument == "")
{
    Console.Error.WriteLine(Usage);
    return 1;
}
var translating = args.Length > 0 && args[0] == "translate";
if (translating && args.Length < 2)
{
    Console.Error.WriteLine(Usage);
    return 1;
}
var seedArgument = translating ? args.ElementAtOrDefault(2) : args.ElementAtOrDefault(0);
int seed;
if (seedArgument is null)
{
    seed = Random.Shared.Next();
}
else if (!int.TryParse(seedArgument, out seed))
{
    Console.Error.WriteLine($"{Usage} Got '{seedArgument}'.");
    return 1;
}

IReadOnlyList<ClauseFile.Entry> script = [];
IReadOnlyDictionary<string, SemanticClass> nouns = new Dictionary<string, SemanticClass>();
if (translating)
{
    var path = args[1];
    var nounsPath = nounsArgument ?? GlossCommand.NounsPathFor(path);
    try
    {
        script = ClauseFile.Parse(File.ReadAllText(path));
        path = nounsPath;
        if (nounsArgument is not null || File.Exists(nounsPath))
        {
            nouns = NounFile.Parse(File.ReadAllText(nounsPath));
        }
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or ClauseFormatException)
    {
        Console.Error.WriteLine($"{path}: {e.Message}");
        return 1;
    }
}

var spec = LanguageSpec.Free;
if (specArgument is not null)
{
    try
    {
        spec = SpecFile.Parse(File.ReadAllText(specArgument));
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or SpecException)
    {
        Console.Error.WriteLine($"{specArgument}: {e.Message}");
        return 1;
    }
    if (spec.Problems() is [_, ..] problems)
    {
        Console.Error.WriteLine($"{specArgument} can't be met:");
        foreach (var problem in problems)
        {
            Console.Error.WriteLine($"  {problem}");
        }
        return 1;
    }
    foreach (var warning in spec.Warnings())
    {
        Console.Error.WriteLine($"{specArgument}: {warning}");
    }
}

Console.WriteLine($"Seed: {seed}");

var lang = Generator.Generate(seed, spec);

if (translating)
{
    lang.Lexicon.Classify(nouns);
    foreach (var entry in script)
    {
        Console.WriteLine();
        Console.WriteLine(Syntax.Realize(lang, entry.Statement, entry.Translation).Interlinear());
    }
    return 0;
}

LanguageDescription.Write(lang, Console.Out);
return 0;
