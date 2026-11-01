namespace LanguageGenerator.Tests;

public class ClauseFileTests
{
    private static ClauseFile.Entry Single(string text) => Assert.Single(ClauseFile.Parse(text));

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    [Fact]
    public void Parse_TransitiveClause_ReadsEveryPart()
    {
        var entry = Single("the dog saw the cats\n  see.PST.PFV  subject:dog  object:cat.PL\n");

        Assert.Equal("the dog saw the cats", entry.Translation);
        Assert.Equal(new Clause("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), new NounPhrase("cat", Number.Plural)), entry.Clause);
        Assert.Equal(2, entry.Line);
    }

    [Fact]
    public void Parse_FeaturesLeftOut_AreTheUnmarkedValues()
    {
        var entry = Single("the bird sleeps\n  sleep subject:bird");

        Assert.Equal(new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("bird", Number.Singular)), entry.Clause);
    }

    [Fact]
    public void Parse_RolesInAnyOrder_MeanTheSame()
    {
        Assert.Equal(Single("x\n  see subject:dog object:cat").Clause, Single("x\n  see object:cat subject:dog").Clause);
    }

    [Fact]
    public void Parse_CommentsBlankLinesAndWindowsLineEndings_AreSkipped()
    {
        var entries = ClauseFile.Parse("# a scene\r\n\r\nthe dog sleeps\r\n  # not this\r\n  sleep subject:dog\r\n\r\nthe cat sleeps\r\n\tsleep subject:cat\r\n");

        Assert.Equal(["dog", "cat"], entries.Select(e => e.Clause.Subject.Value is NounPhrase n ? n.Noun : null));
    }

    [Theory]
    [InlineData("the dog sleeps\n", 1, "needs an indented clause")]
    [InlineData("the dog sleeps\nthe cat sleeps\n  sleep subject:cat", 1, "needs an indented clause")]
    [InlineData("  sleep subject:dog", 1, "on an unindented line above it")]
    [InlineData("x\n  sleep object:dog", 2, "needs a subject")]
    [InlineData("x\n  subject:dog sleep", 2, "starts with its verb")]
    [InlineData("x\n  sleep dog", 2, "needs a role")]
    [InlineData("x\n  sleep subject:dog subject:cat", 2, "more than one subject")]
    [InlineData("x\n  sleep agent:dog", 2, "'agent' isn't a role")]
    [InlineData("x\n  give subject:dog recipient:cat", 2, "can't have a recipient yet")]
    [InlineData("x\n  sleep.PST.FUT subject:dog", 2, "more than one tense")]
    [InlineData("x\n  sleep.PL subject:dog", 2, "agrees with its subject")]
    [InlineData("x\n  sleep.SBJV subject:dog", 2, "only tense and aspect")]
    [InlineData("x\n  sleep subject:dog.ACC", 2, "case comes from its role")]
    [InlineData("x\n  sleep subject:dog.SG.PL", 2, "more than one number")]
    [InlineData("x\n  sleep subject:.PL", 2, "needs a word")]
    public void Parse_Mistakes_SayWhatAndWhere(string text, int line, string message)
    {
        var error = Error(text);

        Assert.Equal(line, error.Line);
        Assert.Contains(message, error.Message);
        Assert.StartsWith($"Line {line}:", error.Message);
    }

    [Fact]
    public void Parse_Pronouns_ArePersonAndNumber()
    {
        var entry = Single("we see you\n  see subject:1PL.INCL object:2SG");

        Assert.Equal(new Pronoun(Person.First, Number.Plural, Clusivity.Inclusive), entry.Clause.Subject.Value);
        Assert.Equal(new Pronoun(Person.Second, Number.Singular), entry.Clause.Object!.Value.Value);
    }

    [Theory]
    [InlineData("x\n  sleep subject:3PL.INCL", "only 'we' includes or excludes")]
    [InlineData("x\n  sleep subject:1SG.EXCL", "only 'we' includes or excludes")]
    [InlineData("x\n  sleep subject:1PL.INCL.EXCL", "more than one clusivity")]
    [InlineData("x\n  sleep subject:1SG.NOM", "a pronoun's case comes from its role")]
    [InlineData("x\n  sleep subject:3SG.F", "pronouns have only person, number and clusivity")]
    public void Parse_PronounMistakes_SayWhat(string text, string message)
    {
        Assert.Contains(message, Error(text).Message);
    }

    [Theory]
    [InlineData("PAST", "PST")]
    [InlineData("pst", "PST")]
    [InlineData("PERF", "PFV")]
    [InlineData("plural", "PL")]
    public void Parse_UnknownGloss_SuggestsTheOneMeant(string gloss, string suggested)
    {
        var error = Error($"x\n  sleep.{gloss} subject:dog");

        Assert.Contains($"'{gloss}' isn't a gloss. Did you mean {suggested}", error.Message);
    }

    [Fact]
    public void Parse_EveryGlossTheGeneratorPrints_CanBeWritten()
    {
        //the input vocabulary is the output's: any tense, aspect or number a paradigm uses reads back
        var values = GeneratedLanguages.Sample.SelectMany(l => l.Paradigms.Values).SelectMany(p => p.Categories)
            .Where(c => c.Name is "Tense" or "Aspect" or "Number").SelectMany(c => c.Values).Distinct();

        Assert.All(values, value =>
        {
            var clause = Single(value is Number ? $"x\n  sleep subject:dog.{value.Gloss}" : $"x\n  sleep.{value.Gloss} subject:dog").Clause;
            Assert.Contains(value, (Tagmeme[])[clause.Tense, clause.Aspect, clause.Subject.Agreement.Number]);
        });
    }

    [Fact]
    public void ExampleScene_ParsesAndTranslatesInEveryLanguage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "scene.txt");
        var entries = ClauseFile.Parse(File.ReadAllText(path));

        Assert.NotEmpty(entries);
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var language = Generator.Generate(new Random(seed));
            Assert.All(entries, e => Assert.NotEmpty(Syntax.Realize(language, e.Clause, e.Translation).Text));
        }
    }
}
