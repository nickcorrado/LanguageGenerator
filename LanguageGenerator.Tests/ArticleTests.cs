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
        Assert.Equal("dog-PL-DEF-ACC", Glossed(new Articles(Plain(DefiniteAffix, ExponentPosition.Suffix), null)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural, null)));

    [Fact]
    public void Apply_Prefix_GoesBeforeTheRoot() =>
        Assert.Equal("DEF-dog-PL-ACC", Glossed(new Articles(Plain(DefiniteAffix, ExponentPosition.Prefix), null)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural, null)));

    [Theory]
    [InlineData(ExponentPosition.PrecedingParticle, "DEF dog-PL-ACC")]
    [InlineData(ExponentPosition.FollowingParticle, "dog-PL-ACC DEF")]
    public void Apply_Word_GoesAtTheEdgeOfThePhrase(ExponentPosition position, string expected) =>
        Assert.Equal(expected, Glossed(new Articles(Plain(DefiniteWord, position), null)
            .Apply(DogsAccusative, Dog, Definiteness.Definite, Number.Plural, null)));

    [Fact]
    public void Apply_Indefinite_OnlyMarksOneThing()
    {
        var articles = new Articles(null, Plain(IndefiniteWord, ExponentPosition.PrecedingParticle));
        IReadOnlyList<Word> dog = [new Word([Dog])];

        Assert.Equal("NDF dog", Glossed(articles.Apply(dog, Dog, Definiteness.Indefinite, Number.Singular, null)));
        Assert.Equal("dog", Glossed(articles.Apply(dog, Dog, Definiteness.Indefinite, Number.Plural, null)));
    }

    [Fact]
    public void Apply_BareOrMissingArticle_LeavesThePhrase()
    {
        var definiteOnly = new Articles(Plain(DefiniteWord, ExponentPosition.PrecedingParticle), null);

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

        Assert.Equal(expected, Glossed(new Articles(Agreeing, null).Apply([new Word([Dog])], Dog, Definiteness.Definite, n, g)));
    }

    [Fact]
    public void For_ArticleAgreeingInNothing_IgnoresGenderAndNumber() =>
        Assert.Equal(DefiniteWord, Plain(DefiniteWord, ExponentPosition.PrecedingParticle).For(Gender.Feminine, Number.Plural).Value);

    private static Language WithGender => GeneratedLanguages.Sample.First(l => l.Genders.Values.Count > 0 && l.MorphologicalProfile.Fusion != Fusion.Isolating);

    [Fact]
    public void GenerateArticles_ManyDraws_MatchTheWalsCounts()
    {
        var language = WithGender;
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateArticles(rand, language.MorphologicalProfile, language.Genders, language.Numerals, language.Phonology, 0.15)).ToList();

        //WALS 37A: 285 of 620 with a definite word, 92 with an affix and 243 with neither
        Assert.InRange(drawn.Count(a => a.Definite is { } d && FirstForm(d).Value is Particle) / 4000.0, 0.43, 0.49);
        Assert.InRange(drawn.Count(a => a.Definite is { } d && FirstForm(d).Value is Affix) / 4000.0, 0.13, 0.17);
        //38A, given the definite article or its absence
        var withDefinite = drawn.Where(a => a.Definite is not null).ToList();
        var withoutDefinite = drawn.Where(a => a.Definite is null).ToList();
        Assert.InRange(withDefinite.Count(a => a.Indefinite is not null) / (double)withDefinite.Count, 0.62, 0.71);
        Assert.InRange(withoutDefinite.Count(a => a.Indefinite is not null) / (double)withoutDefinite.Count, 0.15, 0.23);
        //of indefinite articles, 102 a word, 112 'one' and 24 an affix
        var indefinites = drawn.Select(a => a.Indefinite).OfType<Article>().ToList();
        Assert.InRange(indefinites.Count(a => FirstForm(a).Value is Root) / (double)indefinites.Count, 0.42, 0.52);
        Assert.InRange(indefinites.Count(a => FirstForm(a).Value is Affix) / (double)indefinites.Count, 0.07, 0.13);
        Assert.All(indefinites.Where(a => FirstForm(a).Value is Root), a => Assert.Equal(language.Numerals.Words[1].Root, FirstForm(a).Value));
    }

    [Fact]
    public void GenerateArticles_ManyDraws_AgreeAsGuessed()
    {
        var language = WithGender;
        var rand = new Random(1);

        var definites = Enumerable.Range(0, 4000)
            .Select(_ => Generator.GenerateArticles(rand, language.MorphologicalProfile, language.Genders, language.Numerals, language.Phonology, 0.15).Definite)
            .OfType<Article>().ToList();

        Assert.InRange(definites.Count(a => a.AgreesInGender) / (double)definites.Count, 0.76, 0.84);
        Assert.InRange(definites.Count(a => a.AgreesInNumber) / (double)definites.Count, 0.46, 0.54);
        //a form for every combination it agrees in
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

    [Fact]
    public void Generate_ArticleWords_GoOnOneSide()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles is { Definite: { } d, Indefinite: { } i } && FirstForm(d).Value is Particle && FirstForm(i).Value is Particle),
            l => Assert.Equal(l.Articles.Definite!.Position, l.Articles.Indefinite!.Position));
    }

    private static bool IsArticle(Morpheme m) => m.Value is Affix { Grammemes: [Definiteness, ..] } or Particle { Grammemes: [Definiteness, ..] };

    [Fact]
    public void Realize_DefiniteArticleWord_IsOutsideTheAdjectivesAndNumeral()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Articles.Definite is { } d && FirstForm(d).Value is Particle), l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective,
                new NounPhrase("dog", Number.Dual, [new("big")], Count: 2, Definiteness: Definiteness.Definite)), "x");
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
                new NounPhrase("dog", Number.Plural, Definiteness: Definiteness.Definite)), "x");
            var expected = l.Articles.Definite!.For(l.GenderOf(l.Lexicon.Noun("dog")), Number.Plural);

            Assert.Contains(expected, sentence.Words.SelectMany(w => w.Word.Morphemes));
        });
    }

    [Fact]
    public void Realize_BareNoun_HasNoArticle()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular)), "x");

            Assert.DoesNotContain(sentence.Words.SelectMany(w => w.Word.Morphemes), IsArticle);
        });
    }
}
