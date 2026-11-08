using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ComparisonTests
{
    private static readonly CopularClause DogIsBig = new(new Adjective("big"), Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular));
    private static readonly NounPhrase Cats = new("cat", Number.Plural);

    //'the dog is bigger than the cats', in only the basic meanings, since coining new ones would add
    //to the shared sample's lexicons
    private static Sentence Bigger(Language l) => Syntax.Realize(l, new Comparative(DogIsBig, Cats), "x");

    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    [Fact]
    public void GenerateComparison_ManyDraws_MatchTheWalsCounts()
    {
        var language = GeneratedLanguages.Sample[0];
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateComparison(rand, language.WordOrder, language.Phonology, 0.15)).ToList();

        //WALS 121A: 78 locational, 34 conjoined and 22 with a particle, of the 134 modeled
        Assert.InRange(drawn.Count(c => c.Value is LocationalComparison) / 4000.0, 0.56, 0.61);
        Assert.InRange(drawn.Count(c => c.Value is ConjoinedComparison) / 4000.0, 0.23, 0.28);
        Assert.InRange(drawn.Count(c => c.Value is ParticleComparison) / 4000.0, 0.15, 0.18);
        //and the guessed places a locational standard is marked as
        var locational = drawn.Select(c => c.Value).OfType<LocationalComparison>().ToList();
        Assert.InRange(locational.Count(c => c.Role == Role.Source) / (double)locational.Count, 0.56, 0.64);
        Assert.InRange(locational.Count(c => c.Role == Role.Goal) / (double)locational.Count, 0.17, 0.23);
        Assert.InRange(locational.Count(c => c.Role == Role.Location) / (double)locational.Count, 0.17, 0.23);
    }

    [Fact]
    public void Generate_Particles_GoOnTheirAdpositionsSide() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ParticleComparison), l =>
            Assert.Equal(l.WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial, ((ParticleComparison)l.Comparison.Value!).BeforeStandard));

    [Fact]
    public void Parse_Standard_MakesAComparative()
    {
        var comparative = Assert.IsType<Comparative>(Parsed("be  subject:marta  adjective:tall  standard:man.PL.DEF").Value);

        Assert.Equal(new Adjective("tall"), comparative.Clause.Complement.Value);
        Assert.Equal(new NounPhrase("man", Number.Plural, Definiteness: Definiteness.Definite), comparative.Standard.Value);
    }

    [Fact]
    public void Parse_StandardWithAnAddressee_IsAnAddressedComparative()
    {
        var addressed = Assert.IsType<Addressed>(Parsed("be  subject:2SG  adjective:tall  standard:1SG  addressee:marta").Value);

        Assert.Equal(new Pronoun(Person.First, Number.Singular), Assert.IsType<Comparative>(addressed.Statement.Value).Standard.Value);
    }

    [Theory]
    [InlineData("see  subject:dog  standard:cat", "Only be takes a standard:")]
    [InlineData("be  subject:dog  noun:fool  standard:cat", "compares an adjective:")]
    [InlineData("be  subject:dog  location:house  standard:cat", "compares an adjective:")]
    [InlineData("be.NEG  subject:dog  adjective:big  standard:cat", "can't be negated yet")]
    [InlineData("be.IMP  adjective:big  standard:cat", "A command can't compare yet")]
    public void Parse_BadStandards_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);

    [Fact]
    public void Realize_Comparative_SaysTheDogTheCatsAndWhatTheyCompareIn()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Bigger(l);

            Assert.All(new[] { "dog", "cat", "big" }, meaning => Assert.True(IndexOf(sentence, meaning) >= 0, meaning));
        });
    }

    [Fact]
    public void Realize_LocationalComparison_MarksTheStandardLikeItsPlace()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is LocationalComparison), l =>
        {
            var sentence = Bigger(l);
            var marked = l.RoleMarkers[((LocationalComparison)l.Comparison.Value!).Role].Value switch
            {
                Case c => sentence.Words[IndexOf(sentence, "cat")].Gloss.Split('-', '.', '[', ']').Contains(c.Gloss)
                    || sentence.Words.Any(w => w.Word.Morphemes.Any(m => m.Value is Particle { Grammemes: var g } && g.Contains(c))),
                Adposition a => IndexOf(sentence, a.Lexeme.Root) >= 0,
                _ => true,
            };

            Assert.True(marked);
        });
    }

    [Fact]
    public void Realize_ParticleComparison_PutsTheParticleBesideTheStandard()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ParticleComparison), l =>
        {
            var particle = (ParticleComparison)l.Comparison.Value!;
            var sentence = Bigger(l);
            var (than, cat) = (IndexOf(sentence, particle.Than.Root), IndexOf(sentence, "cat"));

            Assert.True(particle.BeforeStandard ? than < cat : than > cat);
            Assert.False(sentence.Words.Any(w => w.Word.Morphemes.Any(m => m.Value is Root { Meaning: "from" or "to" or "at" })));
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Comparison.Value is ParticleComparison);
    }

    [Fact]
    public void Realize_ConjoinedComparison_IsTheClauseThenTheStandardDenied()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ConjoinedComparison), l =>
        {
            var first = Syntax.Realize(l, DogIsBig, "x");
            var second = Syntax.Realize(l, DogIsBig with { Subject = Cats, Negative = true }, "x");

            Assert.Equal(first.Words.Concat(second.Words).Select(w => w.Gloss), Bigger(l).Words.Select(w => w.Gloss));
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Comparison.Value is ConjoinedComparison);
    }
}
