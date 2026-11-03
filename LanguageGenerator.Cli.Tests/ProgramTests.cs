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
