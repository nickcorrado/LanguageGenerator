using LanguageGenerator;
using LanguageGenerator.Cli;

//pass a seed to regenerate the same language; without one, a fresh seed is picked and printed.
//"translate <file> [seed]" translates a file of clauses (see ClauseFile) instead of describing
//the language, with the noun file (see NounFile) that --nouns names or else the one beside it, and
//"gloss <english.txt>" writes such files from English (see Glosser)
if (args is ["gloss", .. var glossArguments])
{
    return await GlossCommand.Run(glossArguments);
}
const string Usage = "Usage: LanguageGenerator.Cli [seed], or LanguageGenerator.Cli translate <file> [seed] [--nouns file], where the seed is an integer, or LanguageGenerator.Cli gloss <english.txt>.";
string? nounsArgument = null;
if (Array.IndexOf(args, "--nouns") is >= 0 and var nounsAt)
{
    if (nounsAt + 1 >= args.Length)
    {
        Console.Error.WriteLine(Usage);
        return 1;
    }
    nounsArgument = args[nounsAt + 1];
    args = [.. args[..nounsAt], .. args[(nounsAt + 2)..]];
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

Console.WriteLine($"Seed: {seed}");

var lang = Generator.Generate(seed);

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
