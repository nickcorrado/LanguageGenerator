using LanguageGenerator;
using LanguageGenerator.Cli;

//pass a seed to regenerate the same language; without one, a fresh seed is picked and printed.
//"translate <file> [seed]" translates a file of clauses (see ClauseFile) instead of describing
//the language, and "gloss <english.txt>" writes such a file from English (see Glosser)
if (args is ["gloss", .. var glossArguments])
{
    return await GlossCommand.Run(glossArguments);
}
const string Usage = "Usage: LanguageGenerator.Cli [seed], or LanguageGenerator.Cli translate <file> [seed], where the seed is an integer, or LanguageGenerator.Cli gloss <english.txt>.";
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
if (translating)
{
    var path = args[1];
    try
    {
        script = ClauseFile.Parse(File.ReadAllText(path));
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or ClauseFormatException)
    {
        Console.Error.WriteLine($"{path}: {e.Message}");
        return 1;
    }
}

Console.WriteLine($"Seed: {seed}");
var rand = new Random(seed);

var lang = Generator.Generate(rand);

if (translating)
{
    foreach (var entry in script)
    {
        Console.WriteLine();
        Console.WriteLine(Syntax.Realize(lang, entry.Statement, entry.Translation).Interlinear());
    }
    return 0;
}

LanguageDescription.Write(lang, Console.Out);
return 0;
