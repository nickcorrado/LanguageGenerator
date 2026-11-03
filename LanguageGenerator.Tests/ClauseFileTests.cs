namespace LanguageGenerator.Tests;

public class ClauseFileTests
{
    private static ClauseFile.Entry Single(string text) => Assert.Single(ClauseFile.Parse(text));

    //the clause an entry says, which most of these tests expect to be a verb's
    private static Clause Verbal(ClauseFile.Entry entry) => Assert.IsType<Clause>(entry.Statement.Value);

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    [Fact]
    public void Parse_TransitiveClause_ReadsEveryPart()
    {
        var entry = Single("the dog saw the cats\n  see.PST.PFV  subject:dog  object:cat.PL\n");

        Assert.Equal("the dog saw the cats", entry.Translation);
        Assert.Equal(new Clause("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), new NounPhrase("cat", Number.Plural)), Verbal(entry));
        Assert.Equal(2, entry.Line);
    }

    [Fact]
    public void Parse_Obliques_KeepTheirRolesInTheOrderGiven()
    {
        var clause = Verbal(Single("we went from the village to the river with you\n  walk.PST  source:village  subject:1PL  goal:(river big)  companion:2SG"));

        Assert.Equal(new Clause("walk", Tense.Past, Aspect.Imperfective, new Pronoun(Person.First, Number.Plural), Obliques:
        [
            new(CaseUse.MovementFrom, new NounPhrase("village", Number.Singular)),
            new(CaseUse.MovementTo, new NounPhrase("river", Number.Singular, [new("big")])),
            new(CaseUse.Accompaniment, new Pronoun(Person.Second, Number.Singular)),
        ]), clause);
        Assert.NotEqual(clause with { Obliques = [.. clause.Adjuncts.Reverse()] }, clause);
    }

    [Theory]
    [InlineData("recipient", CaseUse.IndirectObject)]
    [InlineData("location", CaseUse.Location)]
    [InlineData("goal", CaseUse.MovementTo)]
    [InlineData("source", CaseUse.MovementFrom)]
    [InlineData("companion", CaseUse.Accompaniment)]
    [InlineData("instrument", CaseUse.Instrument)]
    public void Parse_ObliqueRole_IsItsCaseUse(string role, CaseUse use)
    {
        Assert.Equal(use, Assert.Single(Verbal(Single($"x\n  see subject:dog object:cat {role}:stone")).Adjuncts).Role);
    }

    [Fact]
    public void Parse_NegativeVerb_NegatesTheClause()
    {
        Assert.True(Verbal(Single("the dog didn't sleep\n  sleep.PST.NEG subject:dog")).Negative);
        Assert.False(Verbal(Single("the dog slept\n  sleep.PST subject:dog")).Negative);
    }

    [Fact]
    public void Parse_Be_IsACopularClause()
    {
        Assert.Equal(new CopularClause(new Adjective("locked"), Tense.Past, Aspect.Imperfective, new NounPhrase("gate", Number.Singular)),
            Single("the gate was locked\n  be.PST subject:gate adjective:locked").Statement.Value);
        Assert.Equal(new CopularClause(new NounPhrase("fool", Number.Plural), Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, Number.Plural), Negative: true),
            Single("you aren't fools\n  be.NEG subject:2PL noun:fool.PL").Statement.Value);
    }

    [Fact]
    public void Parse_BeWithALocation_SaysWhereItsSubjectIs()
    {
        Assert.Equal(new CopularClause(new Oblique(CaseUse.Location, new NounPhrase("house", Number.Singular, [new("big")])), Tense.Past, Aspect.Imperfective, new NounPhrase("dog", Number.Singular), Negative: true),
            Single("the dog wasn't in the big house\n  be.PST.NEG location:(house big) subject:dog").Statement.Value);
    }

    [Theory]
    [InlineData("x\n  be subject:dog","be needs a noun, adjective or location")]
    [InlineData("x\n  be subject:dog noun:cat location:house", "be says one thing of its subject")]
    [InlineData("x\n  be subject:dog location:house adjective:big", "be says one thing of its subject")]
    [InlineData("x\n  be subject:dog object:cat", "be has no object")]
    [InlineData("x\n  be subject:dog noun:cat adjective:big", "more than one noun or adjective")]
    [InlineData("x\n  be subject:dog noun:1SG", "has to be a noun, not a pronoun")]
    [InlineData("x\n  be subject:dog adjective:big.PL", "adjectives have no features yet")]
    [InlineData("x\n  sleep subject:dog adjective:big", "Only be takes a noun: or adjective:")]
    public void Parse_CopulaMistakes_SayWhat(string text, string message)
    {
        Assert.Contains(message, Error(text).Message);
    }

    [Fact]
    public void Parse_NounWithAdjectives_KeepsThemInOrder()
    {
        var clause = Verbal(Single("the big old dogs saw the cat\n  see.PST  subject:(dog.PL big old)  object:(cat)"));

        Assert.Equal(new NounPhrase("dog", Number.Plural, [new("big"), new("old")]), clause.Subject.Value);
        Assert.Equal(new NounPhrase("cat", Number.Singular), clause.Object!.Value.Value);
    }

    [Theory]
    [InlineData("x\n  sleep subject:(dog big", "isn't closed")]
    [InlineData("x\n  sleep subject:dog big)", "without a ( before it")]
    [InlineData("x\n  sleep subject:(dog (big))", "can't be nested")]
    [InlineData("x\n  sleep subject:()", "needs a noun")]
    [InlineData("x\n  sleep subject:(1SG big)", "pronouns don't")]
    [InlineData("x\n  sleep subject:(cat possessor:king)", "can't have a possessor yet")]
    [InlineData("x\n  sleep subject:(dog big.PL)", "adjectives have no features yet")]
    [InlineData("x\n  sleep subject:(dog.ACC big)", "case comes from its role")]
    public void Parse_NounPhraseMistakes_SayWhat(string text, string message)
    {
        Assert.Contains(message, Error(text).Message);
    }

    [Fact]
    public void Parse_FeaturesLeftOut_AreTheUnmarkedValues()
    {
        var entry = Single("the bird sleeps\n  sleep subject:bird");

        Assert.Equal(new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("bird", Number.Singular)), Verbal(entry));
    }

    [Fact]
    public void Parse_RolesInAnyOrder_MeanTheSame()
    {
        Assert.Equal(Verbal(Single("x\n  see subject:dog object:cat")), Verbal(Single("x\n  see object:cat subject:dog")));
    }

    [Fact]
    public void Parse_CommentsBlankLinesAndWindowsLineEndings_AreSkipped()
    {
        var entries = ClauseFile.Parse("# a scene\r\n\r\nthe dog sleeps\r\n  # not this\r\n  sleep subject:dog\r\n\r\nthe cat sleeps\r\n\tsleep subject:cat\r\n");

        Assert.Equal(["dog", "cat"], entries.Select(e => Verbal(e).Subject.Value is NounPhrase n ? n.Noun : null));
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
    [InlineData("x\n  sleep subject:dog addressee:cat", 2, "can't have an addressee yet")]
    [InlineData("x\n  walk subject:dog goal:house goal:tree", 2, "more than one goal")]
    [InlineData("x\n  be subject:dog goal:house", 2, "be can't take a goal yet")]
    [InlineData("x\n  sleep subject:dog location:house.ACC", 2, "case comes from its role")]
    [InlineData("x\n  sleep.PST.FUT subject:dog", 2, "more than one tense")]
    [InlineData("x\n  sleep.PL subject:dog", 2, "agrees with its subject")]
    [InlineData("x\n  sleep.SBJV subject:dog", 2, "only tense, aspect and polarity")]
    [InlineData("x\n  sleep.NEG.NEG subject:dog", 2, "more than one polarity")]
    [InlineData("x\n  sleep subject:dog.NEG", 2, "the verb that's negated")]
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

        Assert.Equal(new Pronoun(Person.First, Number.Plural, Clusivity.Inclusive), Verbal(entry).Subject.Value);
        Assert.Equal(new Pronoun(Person.Second, Number.Singular), Verbal(entry).Object!.Value.Value);
    }

    [Theory]
    [InlineData("x\n  sleep subject:3PL.INCL", "only 'we' includes or excludes")]
    [InlineData("x\n  sleep subject:1SG.EXCL", "only 'we' includes or excludes")]
    [InlineData("x\n  sleep subject:1PL.INCL.EXCL", "more than one clusivity")]
    [InlineData("x\n  sleep subject:1SG.NOM", "a pronoun's case comes from its role")]
    [InlineData("x\n  sleep subject:3SG.DEF", "pronouns have only person, number, clusivity and gender")]
    [InlineData("x\n  sleep subject:3SG.ANM", "a pronoun's gender is masculine, feminine or neuter")]
    [InlineData("x\n  sleep subject:3SG.M.F", "more than one gender")]
    public void Parse_PronounMistakes_SayWhat(string text, string message)
    {
        Assert.Contains(message, Error(text).Message);
    }

    [Fact]
    public void Parse_GenderedPronoun_KeepsItsGender()
    {
        Assert.Equal(new Pronoun(Person.Third, Number.Singular, Gender: Gender.Feminine), Verbal(Single("she sleeps\n  sleep subject:3SG.F")).Subject.Value);
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
            var clause = Verbal(Single(value is Number ? $"x\n  sleep subject:dog.{value.Gloss}" : $"x\n  sleep.{value.Gloss} subject:dog"));
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
            Assert.All(entries, e => Assert.NotEmpty(Syntax.Realize(language, e.Statement, e.Translation).Text));
        }
    }
}
