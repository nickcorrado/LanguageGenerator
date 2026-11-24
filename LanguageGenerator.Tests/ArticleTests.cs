using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ArticleTests
{
    private static readonly Root Dog = new([Consonant.d, Vowel.o, Consonant.g], "dog", null);
    private static readonly Affix Plural = new([Consonant.s], [Number.Plural], AffixAccent.None);
    private static readonly Affix Accusative = new([Vowel.a], [Case.Accusative], AffixAccent.None);
    private static readonly Affix DefiniteAffix = new([Consonant.n], [Definiteness.Definite], AffixAccent.None);
    private static readonly Particle DefiniteWord = new([Consonant.t, Vowel.a], [Definiteness.Definite]);
    private static readonly Particle IndefiniteWord = new([Vowel.u], [Definiteness.Indefinite]);

    private static readonly IReadOnlyList<Word> DogsAccusative = [new Word([Dog, Plural, Accusative])];

    //an article with one form, agreeing in nothing
    private static Article Plain(Morpheme form, ExponentPosition position) => new(position, new Dictionary<(Gender?, Number?), Morpheme> { [(null, null)] = form });

    //a definite word for each of masculine and feminine, singular and plural, glossed by what it agrees in
    private static readonly Article Agreeing = new(ExponentPosition.PrecedingParticle, new Dictionary<(Gender?, Number?), Morpheme>
    {
        [(Gender.Masculine, Number.Singular)] = new Particle([], [Definiteness.Definite, Gender.Masculine, Number.Singular]),
        [(Gender.Masculine, Number.Plural)] = new Particle([], [Definiteness.Definite, Gender.Masculine, Number.Plural]),
        [(Gender.Feminine, Number.Singular)] = new Particle([], [Definiteness.Definite, Gender.Feminine, Number.Singular]),
        [(Gender.Feminine, Number.Plural)] = new Particle([], [Definiteness.Definite, Gender.Feminine, Number.Plural]),
    });

    private static Morpheme FirstForm(Article article) => article.Forms.Values.First();

    private static string Glossed(IReadOnlyList<Word> words) => string.Join(" ", words.Select(w => w.Gloss));

    [Fact]
    public void Apply_Suffix_GoesOutsideNumberInsideCase() =>
        Assert.Equal("dog-PL-DEF-ACC", Glossed(new Articles(Plain(DefiniteAffix, ExponentPosition.Suffix), null, false, false)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural, null)));

    [Fact]
    public void Apply_Prefix_GoesBeforeTheRoot() =>
        Assert.Equal("DEF-dog-PL-ACC", Glossed(new Articles(Plain(DefiniteAffix, ExponentPosition.Prefix), null, false, false)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural, null)));

    [Theory]
    [InlineData(ExponentPosition.PrecedingParticle, "DEF dog-PL-ACC")]
    [InlineData(ExponentPosition.FollowingParticle, "dog-PL-ACC DEF")]
    public void Apply_Word_GoesAtTheEdgeOfThePhrase(ExponentPosition position, string expected) =>
        Assert.Equal(expected, Glossed(new Articles(Plain(DefiniteWord, position), null, false, false)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural, null)));

    [Fact]
    public void Apply_Indefinite_OnlyMarksOneThing()
    {
        var articles = new Articles(null, Plain(IndefiniteWord, ExponentPosition.PrecedingParticle), false, false);
        IReadOnlyList<Word> dog = [new Word([Dog])];

        Assert.Equal("NDF dog", Glossed(articles.Apply(dog, Dog, Definiteness.Indefinite, Number.Singular, null)));
        Assert.Equal("dog", Glossed(articles.Apply(dog, Dog, Definiteness.Indefinite, Number.Plural, null)));
    }

    [Fact]
    public void Apply_BareOrMissingArticle_LeavesThePhrase()
    {
        var definiteOnly = new Articles(Plain(DefiniteWord, ExponentPosition.PrecedingParticle), null, false, false);

        Assert.Same(DogsAccusative, definiteOnly.Apply(DogsAccusative, Dog, null, Number.Plural, null));
        Assert.Same(DogsAccusative, definiteOnly.Apply(DogsAccusative, Dog, Definiteness.Indefinite, Number.Singular, null));
    }

    [Theory]
    [InlineData("Masculine", "Singular", "DEF.M.SG dog")]
    [InlineData("Feminine", "Plural", "DEF.F.PL dog")]
    [InlineData("Feminine", "Dual", "DEF.F.PL dog")]
    [InlineData("Masculine", "Paucal", "DEF.M.PL dog")]
    public void Apply_AgreeingArticle_TakesTheNounsGenderAndNumber(string gender, string number, string expected)
    {
        var g = ClauseFile.ValuesOf<Gender>().Single(v => v.Name == gender);
        var n = ClauseFile.ValuesOf<Number>().Single(v => v.Name == number);

        Assert.Equal(expected, Glossed(new Articles(Agreeing, null, false, false).Apply([new Word([Dog])], Dog, Definiteness.Definite, n, g)));
    }

    [Fact]
    public void For_ArticleAgreeingInNothing_IgnoresGenderAndNumber() =>
        Assert.Equal(DefiniteWord, Plain(DefiniteWord, ExponentPosition.PrecedingParticle).For(Gender.Feminine, Number.Plural).Value);

    private static Language WithGender => GeneratedLanguages.With(s => s.Inflecting().Fix(Settings.Genders, [.. Enum.GetValues<NounGenders>().Where(g => g != NounGenders.None)]));

    [Fact]
    public void GenerateArticles_IndefiniteFromAWord_IsTheNumeralOne()
    {
        var language = WithGender;
        var rand = new Random(1);

        var indefinites = Enumerable.Range(0, 1000)
            .Select(_ => Generator.GenerateArticles(rand, language.MorphologicalProfile, language.Genders, language.Numerals, language.Demonstratives, language.Phonology).Indefinite)
            .OfType<Article>().ToList();

        Assert.Contains(indefinites, a => FirstForm(a).Value is Root);
        Assert.All(indefinites.Where(a => FirstForm(a).Value is Root), a => Assert.Equal(language.Numerals.Words[1].Root, FirstForm(a).Value));
    }

    [Fact]
    public void GenerateArticles_Definite_HasAFormForEveryCombinationItAgreesIn()
    {
        var language = WithGender;
        var rand = new Random(1);

        var definites = Enumerable.Range(0, 1000)
            .Select(_ => Generator.GenerateArticles(rand, language.MorphologicalProfile, language.Genders, language.Numerals, language.Demonstratives, language.Phonology).Definite)
            .OfType<Article>().ToList();

        Assert.All(definites, a => Assert.Equal((a.AgreesInGender ? language.Genders.Values.Count : 1) * (a.AgreesInNumber ? 2 : 1), a.Forms.Count));
    }

    [Fact]
    public void Generate_IndefiniteArticles_NeverAgreeInNumber_AndOneAgreesInNothing()
    {
        Assert.All(GeneratedLanguages.Sample.Select(l => l.Articles.Indefinite).OfType<Article>(), a =>
        {
            Assert.False(a.AgreesInNumber);
            if (FirstForm(a).Value is Root)
            {
                Assert.Single(a.Forms);
            }
        });
    }

    [Fact]
    public void Generate_LanguagesWithoutGender_HaveArticlesThatDontAgreeInIt() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Genders.Values.Count == 0),
            l => Assert.DoesNotContain(new[] { l.Articles.Definite, l.Articles.Indefinite }, a => a?.AgreesInGender == true));

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoArticleAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.DoesNotContain(new[] { l.Articles.Definite, l.Articles.Indefinite }.OfType<Article>(), a => FirstForm(a).Value is Affix));

    //an article that's the demonstrative goes where the demonstrative does
    [Fact]
    public void Generate_ArticleWords_GoOnOneSide()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles is { Definite: { } d, Indefinite: { } i, DefiniteFromDemonstrative: false } && FirstForm(d).Value is Particle && FirstForm(i).Value is Particle),
            l => Assert.Equal(l.Articles.Definite!.Position, l.Articles.Indefinite!.Position));
    }

    private static bool IsArticle(Morpheme m) => m.Value is Affix { Grammemes: [Definiteness, ..] } or Particle { Grammemes: [Definiteness, ..] };

    [Fact]
    public void Realize_DefiniteArticleWord_IsOutsideTheAdjectivesAndNumeral()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles.Definite is { } d && FirstForm(d).Value is Particle), l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective,
                new NounPhrase("dog", Number.Dual, [new("big", false)], Count: 2, Definiteness: Definiteness.Definite)) { Adverbs = [] }, "x");
            var article = sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(IsArticle));
            var others = new[] { IndexOf(sentence, "two"), IndexOf(sentence, "big"), IndexOf(sentence, "dog") };

            Assert.True(l.Articles.Definite!.Position == ExponentPosition.PrecedingParticle ? article < others.Min() : article > others.Max());
        });
    }

    [Fact]
    public void Realize_DefiniteArticle_AgreesWithItsNoun()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles.Definite is not null), l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective,
                new NounPhrase("dog", Number.Plural, Definiteness: Definiteness.Definite)) { Adverbs = [] }, "x");
            var expected = l.Articles.Definite!.For(l.GenderOf(l.Lexicon.Noun("dog")), Number.Plural);

            Assert.Contains(expected, sentence.Words.SelectMany(w => w.Word.Morphemes));
        });
    }

    [Fact]
    public void Realize_BareNoun_HasNoArticle()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular)) { Adverbs = [] }, "x");

            Assert.DoesNotContain(sentence.Words.SelectMany(w => w.Word.Morphemes), IsArticle);
        });
    }
}
