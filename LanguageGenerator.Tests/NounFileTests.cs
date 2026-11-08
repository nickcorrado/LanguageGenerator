namespace LanguageGenerator.Tests;

public class NounFileTests
{
    //a fresh language with this many genders, since the sample's are shared and classifying changes one
    private static Language WithGenders(NounGenders genders) =>
        Enumerable.Range(0, 300).Select(seed => Generator.Generate(new Random(seed))).First(l => l.Genders.Genders == genders);

    [Fact]
    public void Parse_ReadsEveryTagAndSkipsComments()
    {
        var nouns = NounFile.Parse("# what they name\n\nmarta F\nking   M\nminer HUM\ncrow ANIM\nriver THING\n");

        Assert.Equal(new Dictionary<string, SemanticClass>
        {
            ["marta"] = SemanticClass.Female,
            ["king"] = SemanticClass.Male,
            ["miner"] = SemanticClass.Human,
            ["crow"] = SemanticClass.Animal,
            ["river"] = SemanticClass.Thing,
        }, nouns);
    }

    [Theory]
    [InlineData("crow", "Line 1", "a noun and a tag")]
    [InlineData("old crow ANIM", "Line 1", "a noun and a tag")]
    [InlineData("crow BIRD", "Line 1", "'BIRD' isn't a tag")]
    [InlineData("crow ANIM\n\ncrow F", "Line 3", "both Animal and Female")]
    [InlineData("Marta F\nmarta M", "Line 2", "both Female and Male")]
    public void Parse_BadLines_SayWhatsWrong(string text, string line, string message)
    {
        var error = Assert.Throws<ClauseFormatException>(() => NounFile.Parse(text));

        Assert.Contains(line, error.Message);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Parse_TheSameLineTwice_IsFine()
    {
        Assert.Single(NounFile.Parse("crow ANIM\ncrow ANIM"));
    }

    [Fact]
    public void Parse_IgnoresCase()
    {
        var nouns = NounFile.Parse("Marta F\nmarta F");

        Assert.Equal(SemanticClass.Female, Assert.Single(nouns).Value);
        Assert.True(nouns.ContainsKey("marta"));
    }

    [Fact]
    public void Clauses_IgnoreCaseInWords_ButNotInGlosses()
    {
        var entries = ClauseFile.Parse("""
            one
              See.PST  subject:Marta  object:(Wolf.PL Big)
            two
              see.PST  subject:marta  object:wolf.PL
            three
              Be  subject:Marta  adjective:Big
            """);

        Assert.Equal(["marta", "wolf"], ClauseFile.Nouns(entries).Select(n => n.Noun));
        var (first, second) = ((Clause)entries[0].Statement.Value!, (Clause)entries[1].Statement.Value!);
        Assert.Equal(second.Verb, first.Verb);
        Assert.Equal(second.Subject, first.Subject);
        Assert.Equal("big", Assert.Single(((NounPhrase)first.Object.GetValueOrDefault().Value!).Adjectives).Meaning);
        Assert.IsType<CopularClause>(entries[2].Statement.Value);
        Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse("one\n  see.pst  subject:marta"));
    }

    [Fact]
    public void Write_RoundTrips_InAlphabeticalOrder()
    {
        var text = NounFile.Write(new Dictionary<string, SemanticClass> { ["river"] = SemanticClass.Thing, ["marta"] = SemanticClass.Female });

        Assert.Equal("marta F\nriver THING\n", text);
        Assert.Equal(SemanticClass.Thing, NounFile.Parse(text)["river"]);
    }

    [Fact]
    public void Nouns_FindsThemEverywhereTheClausesPutThem()
    {
        var entries = ClauseFile.Parse("""
            one
              see  subject:(hunter possessor:king)  object:wolf  location:(house old)
            two
              be  subject:smith  noun:fool
            three
              be  subject:cat  location:barn
            four
              be  subject:3SG.F  adjective:big
            five
              drink.IMP  object:wine  source:well
            six
              see  subject:hunter
            seven
              sleep  subject:dog  addressee:marta
            """);

        var nouns = ClauseFile.Nouns(entries);

        Assert.Equal(["hunter", "king", "wolf", "house", "smith", "fool", "cat", "barn", "wine", "well", "dog", "marta"], nouns.Select(n => n.Noun));
        Assert.Equal("one", nouns[0].Translation);
    }

    [Fact]
    public void ClassOf_IsWhatTheFileSays_OrElseAThing()
    {
        var lexicon = new Lexicon((meaning, _) => new Root([], meaning));

        lexicon.Classify(new Dictionary<string, SemanticClass> { ["marta"] = SemanticClass.Female, ["dog"] = SemanticClass.Animal });

        Assert.Equal(SemanticClass.Female, lexicon.ClassOf("marta"));
        Assert.Equal(SemanticClass.Animal, lexicon.ClassOf("dog"));
        Assert.Equal(SemanticClass.Thing, lexicon.ClassOf("river"));
    }

    [Fact]
    public void Classify_ListsThatDisagree_Throw_WithoutChangingAnything()
    {
        var lexicon = new Lexicon((meaning, _) => new Root([], meaning));
        lexicon.Classify(new Dictionary<string, SemanticClass> { ["marta"] = SemanticClass.Female });

        Assert.Throws<ArgumentException>(() => lexicon.Classify(new Dictionary<string, SemanticClass> { ["crow"] = SemanticClass.Animal, ["marta"] = SemanticClass.Male }));

        Assert.Equal(SemanticClass.Female, lexicon.ClassOf("marta"));
        Assert.Equal(SemanticClass.Thing, lexicon.ClassOf("crow"));
    }

    [Fact]
    public void Classify_ChangesGenderButNeverTheRoot()
    {
        var language = WithGenders(NounGenders.MasculineFeminineNeuter);
        var before = language.Lexicon.Noun("marta").Root;

        language.Lexicon.Classify(new Dictionary<string, SemanticClass> { ["marta"] = SemanticClass.Female });

        Assert.Equal(Gender.Feminine, language.GenderOf(language.Lexicon.Noun("marta")));
        Assert.Equal(before, language.Lexicon.Noun("marta").Root);
    }

    //an animal is a thing but for animacy, so only a list can tell a crow from a stone there
    [Fact]
    public void Classify_MakesAnimalsAnimate()
    {
        var language = WithGenders(NounGenders.AnimateInanimate);
        language.Lexicon.Classify(new Dictionary<string, SemanticClass> { ["crow"] = SemanticClass.Animal });

        Assert.Equal(Gender.Animate, language.GenderOf(language.Lexicon.Noun("crow")));
    }

    [Fact]
    public void Classify_ALanguageWithoutGender_StillHasNone()
    {
        var language = WithGenders(NounGenders.None);
        language.Lexicon.Classify(new Dictionary<string, SemanticClass> { ["marta"] = SemanticClass.Female });

        Assert.Null(language.GenderOf(language.Lexicon.Noun("marta")));
    }
}
