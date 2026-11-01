namespace LanguageGenerator.Tests;

//runs the program itself, since printing a language exercises paths the library tests don't, like
//showing a negative affix on its own. Nothing else in the tests writes to the console, so taking
//it over for a moment is safe
public class ProgramTests
{
    private static (int ExitCode, string Output) Run(params string[] args)
    {
        var entryPoint = typeof(Generator).Assembly.EntryPoint!;
        var original = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            return ((int)entryPoint.Invoke(null, [args])!, output.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
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
