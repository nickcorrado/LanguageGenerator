using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class VocativeTests
{
    private static Statement Parse(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    private static ClauseFormatException Error(string gloss) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}"));

    [Fact]
    public void Parse_Addressee_WrapsTheClause()
    {
        var addressed = Assert.IsType<Addressed>(Parse("sleep subject:dog.DEF addressee:marta").Value);

        Assert.Equal(new NounPhrase("marta", Number.Singular), addressed.Addressee);
        Assert.IsType<Clause>(addressed.Statement.Value);
    }

    [Fact]
    public void Parse_AddresseeWithAdjectives_IsANounPhrase()
    {
        var addressed = Assert.IsType<Addressed>(Parse("be subject:wolf.PL.DEF location:village.DEF addressee:(man old)").Value);

        Assert.Equal("man", addressed.Addressee.Noun);
        Assert.Equal(["old"], addressed.Addressee.Adjectives.Select(a => a.Meaning));
        Assert.IsType<CopularClause>(addressed.Statement.Value);
    }

    [Theory]
    [InlineData("walk.IMP addressee:marta", "Singular")]
    [InlineData("walk.IMP addressee:child.PL", "Plural")]
    [InlineData("walk.IMP addressee:child.DU", "Plural")]
    [InlineData("walk.IMP subject:2SG addressee:child.PL", "Singular")]
    public void Parse_CommandToAnAddressee_TellsAsManyAsItSays(string gloss, string number)
    {
        var addressed = Assert.IsType<Addressed>(Parse(gloss).Value);

        Assert.Equal(number, Assert.IsType<Command>(addressed.Statement.Value).Number.Name);
    }

    [Theory]
    [InlineData("sleep subject:dog addressee:2SG", "An addressee is a noun")]
    [InlineData("sleep subject:dog addressee:man.DEF", "takes no article")]
    [InlineData("sleep subject:dog addressee:man addressee:woman", "more than one addressee")]
    public void Parse_BadAddressees_SayWhy(string gloss, string message) => Assert.Contains(message, Error(gloss).Message);

    [Fact]
    public void Realize_Addressee_ComesFirstInTheVocativeWhereThereIsOne()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Addressed(new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular)),
                new NounPhrase("person", Number.Singular)), "x");
            var (person, dog, sleep) = (IndexOf(sentence, "person"), IndexOf(sentence, "dog"), IndexOf(sentence, "sleep"));
            var vocative = sentence.Words.Any(w => w.Gloss.Contains("VOC"));

            Assert.True(person < dog && person < sleep);
            Assert.Equal(l.RoleMarkers[Role.Addressee].Value is Case { Name: "Vocative" }, vocative);
        });
    }

    [Fact]
    public void Realize_Addressed_KeepsTheClauseAsItWas()
    {
        var language = GeneratedLanguages.Sample[0];
        var clause = new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular));
        var plain = Syntax.Realize(language, clause, "x");
        var addressed = Syntax.Realize(language, new Addressed(clause, new NounPhrase("person", Number.Singular)), "x");

        Assert.Equal(plain.Words.Select(w => w.Gloss), addressed.Words.Skip(addressed.Words.Count - plain.Words.Count).Select(w => w.Gloss));
    }
}
