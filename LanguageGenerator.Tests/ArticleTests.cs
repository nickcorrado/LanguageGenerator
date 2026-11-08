using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ArticleTests
{
    private static readonly Root Dog = new([Consonant.d, Vowel.o, Consonant.g], "dog");
    private static readonly Affix Plural = new([Consonant.s], [Number.Plural]);
    private static readonly Affix Accusative = new([Vowel.a], [Case.Accusative]);
    private static readonly Affix DefiniteAffix = new([Consonant.n], [Definiteness.Definite]);
    private static readonly Particle DefiniteWord = new([Consonant.t, Vowel.a], [Definiteness.Definite]);
    private static readonly Particle IndefiniteWord = new([Vowel.u], [Definiteness.Indefinite]);

    private static readonly IReadOnlyList<Word> DogsAccusative = [new Word([Dog, Plural, Accusative])];

    private static string Glossed(IReadOnlyList<Word> words) => string.Join(" ", words.Select(w => w.Gloss));

    [Fact]
    public void Apply_Suffix_GoesOutsideNumberInsideCase() =>
        Assert.Equal("dog-PL-DEF-ACC", Glossed(new Articles(new Article(DefiniteAffix, ExponentPosition.Suffix), null)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural)));

    [Fact]
    public void Apply_Prefix_GoesBeforeTheRoot() =>
        Assert.Equal("DEF-dog-PL-ACC", Glossed(new Articles(new Article(DefiniteAffix, ExponentPosition.Prefix), null)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural)));

    [Theory]
    [InlineData(ExponentPosition.PrecedingParticle, "DEF dog-PL-ACC")]
    [InlineData(ExponentPosition.FollowingParticle, "dog-PL-ACC DEF")]
    public void Apply_Word_GoesAtTheEdgeOfThePhrase(ExponentPosition position, string expected) =>
        Assert.Equal(expected, Glossed(new Articles(new Article(DefiniteWord, position), null)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural)));

    [Fact]
    public void Apply_Indefinite_OnlyMarksOneThing()
    {
        var articles = new Articles(null, new Article(IndefiniteWord, ExponentPosition.PrecedingParticle));
        IReadOnlyList<Word> dog = [new Word([Dog])];

        Assert.Equal("NDF dog", Glossed(articles.Apply(dog, Dog, Definiteness.Indefinite, Number.Singular)));
        Assert.Equal("dog", Glossed(articles.Apply(dog, Dog, Definiteness.Indefinite, Number.Plural)));
    }

    [Fact]
    public void Apply_BareOrMissingArticle_LeavesThePhrase()
    {
        var definiteOnly = new Articles(new Article(DefiniteWord, ExponentPosition.PrecedingParticle), null);

        Assert.Same(DogsAccusative, definiteOnly.Apply(DogsAccusative, Dog, null, Number.Plural));
        Assert.Same(DogsAccusative, definiteOnly.Apply(DogsAccusative, Dog, Definiteness.Indefinite, Number.Singular));
    }

    [Fact]
    public void GenerateArticles_ManyDraws_MatchTheWalsCounts()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateArticles(rand, language.MorphologicalProfile, language.Numerals, language.Phonology, 0.15)).ToList();

        //WALS 37A: 285 of 620 with a definite word, 92 with an affix and 243 with neither
        Assert.InRange(drawn.Count(a => a.Definite?.Marker.Value is Particle) / 4000.0, 0.43, 0.49);
        Assert.InRange(drawn.Count(a => a.Definite?.Marker.Value is Affix) / 4000.0, 0.13, 0.17);
        //38A, given the definite article or its absence
        var withDefinite = drawn.Where(a => a.Definite is not null).ToList();
        var withoutDefinite = drawn.Where(a => a.Definite is null).ToList();
        Assert.InRange(withDefinite.Count(a => a.Indefinite is not null) / (double)withDefinite.Count, 0.62, 0.71);
        Assert.InRange(withoutDefinite.Count(a => a.Indefinite is not null) / (double)withoutDefinite.Count, 0.15, 0.23);
        //of indefinite articles, 102 a word, 112 'one' and 24 an affix
        var indefinites = drawn.Select(a => a.Indefinite).OfType<Article>().ToList();
        Assert.InRange(indefinites.Count(a => a.Marker.Value is Root) / (double)indefinites.Count, 0.42, 0.52);
        Assert.InRange(indefinites.Count(a => a.Marker.Value is Affix) / (double)indefinites.Count, 0.07, 0.13);
        Assert.All(indefinites.Where(a => a.Marker.Value is Root), a => Assert.Equal(language.Numerals.Words[1].Root, a.Marker.Value));
    }

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoArticleAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.DoesNotContain(new[] { l.Articles.Definite, l.Articles.Indefinite }, a => a?.Marker.Value is Affix));

    [Fact]
    public void Generate_ArticleWords_GoOnOneSide()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles is { Definite.Marker.Value: Particle, Indefinite.Marker.Value: Particle }),
            l => Assert.Equal(l.Articles.Definite!.Position, l.Articles.Indefinite!.Position));
    }

    [Fact]
    public void Realize_DefiniteArticleWord_IsOutsideTheAdjectivesAndNumeral()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles.Definite is { Marker.Value: Particle }), l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective,
                new NounPhrase("dog", Number.Dual, [new("big")], Count: 2, Definiteness: Definiteness.Definite)), "x");
            var words = sentence.Words.ToList();
            var article = words.FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Particle { Grammemes: [Definiteness] }));
            var others = new[] { IndexOf(sentence, "two"), IndexOf(sentence, "big"), IndexOf(sentence, "dog") };

            Assert.True(l.Articles.Definite!.Position == ExponentPosition.PrecedingParticle ? article < others.Min() : article > others.Max());
        });
    }

    [Fact]
    public void Realize_BareNoun_HasNoArticle()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular)), "x");

            Assert.DoesNotContain(sentence.Words.SelectMany(w => w.Word.Morphemes), m => m.Value is Affix { Grammemes: [Definiteness] } or Particle { Grammemes: [Definiteness] });
        });
    }
}
