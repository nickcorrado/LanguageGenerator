namespace LanguageGenerator.Cli.Tests;

//runs the program itself, since printing a language exercises paths the library tests don't, like
//showing a negative affix on its own. Nothing else in the tests writes to the console, so taking
//it over for a moment is safe
public class ProgramTests
{
    private static (int ExitCode, string Output) Run(params string[] args)
    {
        var entryPoint = typeof(GlossCommand).Assembly.EntryPoint!;
        var (originalOut, originalError) = (Console.Out, Console.Error);
        var output = new StringWriter();
        Console.SetOut(output);
        Console.SetError(output);
        try
        {
            return ((int)entryPoint.Invoke(null, [args])!, output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    //--again takes no value, so the option after it is read as an option, and the run stops at the
    //bad provider, before it would read any file or call any model
    [Fact]
    public void Gloss_Again_IsAFlag()
    {
        var (exitCode, output) = Run("gloss", "missing.txt", "--again", "--provider", "nobody");

        Assert.Equal(1, exitCode);
        Assert.Contains("'nobody' isn't a provider", output);
    }

    [Fact]
    public void Main_AnySeed_DescribesTheLanguageWithoutFailing()
    {
        foreach (var seed in Enumerable.Range(0, 100))
        {
            var (exitCode, output) = Run(seed.ToString());

            Assert.Equal(0, exitCode);
            Assert.StartsWith($"Seed: {seed}", output);
            Assert.Contains("'the dog didn't see the cat'", output);
        }
    }

    //a clause, and the same language with and without its noun file, whose only difference can be
    //the gender on the adjective and verb
    private static readonly string Clause = "marta slept\n  sleep.PST  subject:(marta big)\n";

    [Fact]
    public void Translate_FindsTheNounFileBesideTheClauses()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        var clauses = Path.Combine(folder, "tale.clauses.txt");
        File.WriteAllText(clauses, Clause);

        var seed = Enumerable.Range(0, 200).Select(s => s.ToString()).First(s =>
        {
            File.Delete(Path.Combine(folder, "tale.nouns.txt"));
            var without = Run("translate", clauses, s).Output;
            File.WriteAllText(Path.Combine(folder, "tale.nouns.txt"), "marta F");
            return without != Run("translate", clauses, s).Output;
        });

        Assert.Contains("F", Run("translate", clauses, seed).Output);
    }

    [Fact]
    public void Translate_NounsOption_NamesAnyFile_AndMustExist()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        var clauses = Path.Combine(folder, "tale.clauses.txt");
        File.WriteAllText(clauses, Clause);
        var shared = Path.Combine(folder, "world.txt");
        File.WriteAllText(shared, "marta F");

        Assert.Equal(0, Run("translate", clauses, "11", "--nouns", shared).ExitCode);
        Assert.Equal(1, Run("translate", clauses, "11", "--nouns", Path.Combine(folder, "missing.txt")).ExitCode);
    }

    [Fact]
    public void Translate_ABadNounFile_NamesItself()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        var clauses = Path.Combine(folder, "tale.clauses.txt");
        File.WriteAllText(clauses, Clause);
        var nouns = Path.Combine(folder, "tale.nouns.txt");
        File.WriteAllText(nouns, "marta WOMAN");

        var (exitCode, output) = Run("translate", clauses, "11");

        Assert.Equal(1, exitCode);
        Assert.Contains(nouns, output);
        Assert.Contains("'WOMAN' isn't a tag", output);
    }

    [Theory]
    [InlineData("hollow.clauses.txt", "hollow.nouns.txt")]
    [InlineData("hollow.txt", "hollow.nouns.txt")]
    public void NounsPathFor_SitsBesideTheClauses(string clauses, string nouns)
    {
        Assert.Equal(Path.Combine("examples", nouns), GlossCommand.NounsPathFor(Path.Combine("examples", clauses)));
    }

    [Fact]
    public void Main_TranslatingTheExampleScene_GlossesEveryClause()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "scene.txt");
        var clauses = ClauseFile.Parse(File.ReadAllText(path)).Count;

        var (exitCode, output) = Run("translate", path, "11");

        Assert.Equal(0, exitCode);
        Assert.Equal(clauses, output.Split(Environment.NewLine).Count(line => line.StartsWith('\'')));
    }
}
