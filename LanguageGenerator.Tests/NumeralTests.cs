namespace LanguageGenerator.Tests;

public class NumeralTests
{
    //formless words glossed with their number, enough for any base
    private static NumeralSystem System(NumeralBase numeralBase, bool largestFirst = true, int limit = NumeralSystem.Largest) => new(
        numeralBase, limit,
        new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20, 100, 400 }.ToDictionary(n => n, n => new Lexeme(new Root([], n.ToString()), PartOfSpeech.Numeral)),
        new Lexeme(new Root([], "many"), PartOfSpeech.Numeral), BeforeNoun: true, NounStaysSingular: false, largestFirst);

    private static string Say(NumeralSystem system, int n) => string.Join(" ", system.Say(n).Select(w => w.Root.Meaning));

    [Theory]
    [InlineData(7, "7")]
    [InlineData(10, "10")]
    [InlineData(17, "10 7")]
    [InlineData(30, "3 10")]
    [InlineData(37, "3 10 7")]
    [InlineData(100, "100")]
    [InlineData(999, "9 100 9 10 9")]
    public void Say_Decimal_CountsInTens(int n, string words) => Assert.Equal(words, Say(System(NumeralBase.Decimal), n));

    [Theory]
    [InlineData(17, "10 7")]
    [InlineData(20, "20")]
    [InlineData(37, "20 10 7")]
    [InlineData(60, "3 20")]
    [InlineData(300, "10 5 20")]
    [InlineData(999, "2 400 9 20 10 9")]
    public void Say_Vigesimal_CountsInTwentiesWithTensBelowThem(int n, string words) => Assert.Equal(words, Say(System(NumeralBase.Vigesimal), n));

    [Fact]
    public void Say_UnitsFirst_ReversesTheParts()
    {
        Assert.Equal("7 3 10", Say(System(NumeralBase.Decimal, largestFirst: false), 37));
        Assert.Equal("7 10 20", Say(System(NumeralBase.Vigesimal, largestFirst: false), 37));
    }

    [Fact]
    public void Say_Restricted_HasManyPastItsLimit()
    {
        var system = System(NumeralBase.Restricted, limit: 4);

        Assert.Equal("4", Say(system, 4));
        Assert.Equal("many", Say(system, 5));
        Assert.Equal("many", Say(system, 999));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Say_OutOfRange_Throws(int n) => Assert.Throws<ArgumentOutOfRangeException>(() => System(NumeralBase.Decimal).Say(n));

    [Fact]
    public void GenerateNumerals_ManyDraws_MatchTheWalsCounts()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 3000).Select(_ => Generator.GenerateNumerals(rand, phonology, 0.15)).ToList();

        //WALS 131A: 125, 42 and 20 of 187 languages modeled; 89A: 479 of 1,086 put the numeral first
        Assert.InRange(drawn.Count(s => s.Base == NumeralBase.Decimal) / 3000.0, 0.64, 0.70);
        Assert.InRange(drawn.Count(s => s.Base == NumeralBase.Vigesimal) / 3000.0, 0.20, 0.25);
        Assert.InRange(drawn.Count(s => s.Base == NumeralBase.Restricted) / 3000.0, 0.09, 0.13);
        Assert.InRange(drawn.Count(s => s.BeforeNoun) / 3000.0, 0.41, 0.47);
        Assert.All(drawn.Where(s => s.Base == NumeralBase.Restricted), s => Assert.InRange(s.Limit, 3, 5));
        //every number the format allows has words, each at most two syllables
        Assert.All(drawn, s =>
        {
            Assert.All(new[] { 1, 12, 37, 400, 999 }, n => Assert.NotEmpty(s.Say(n)));
            Assert.All(s.Words.Values, w => Assert.InRange(w.Root.Form.Count(p => p is Vowel), 1, 2));
        });
    }

    [Fact]
    public void Generate_AnyLanguage_HasNumeralsThatRarelySoundAlike()
    {
        var homophonous = GeneratedLanguages.Sample.Count(l =>
            l.Numerals.Words.Values.Select(w => string.Concat(w.Root.Form.Select(p => p.IpaSymbol))).Distinct().Count() < l.Numerals.Words.Count);

        Assert.InRange(homophonous / (double)GeneratedLanguages.Sample.Count, 0, 0.05);
    }

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static Sentence ThreeBigDogsSlept(Language l) =>
        Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Paucal, [new("big")], Count: 3)), "x");

    private static int IndexOf(Sentence sentence, string meaning) =>
        sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Root r && r.Meaning == meaning));

    [Fact]
    public void Realize_Numeral_GoesOutsideTheAdjectivesOnItsSide()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = ThreeBigDogsSlept(l);
            var (three, big, dog) = (IndexOf(sentence, "three"), IndexOf(sentence, "big"), IndexOf(sentence, "dog"));

            Assert.True(l.Numerals.BeforeNoun ? three < Math.Min(big, dog) : three > Math.Max(big, dog));
        });
    }

    //leaving out languages that mark the phrase at its edges, where the noun's markers move to the
    //numeral or an adjective
    [Fact]
    public void Realize_Numeral_LeavesTheNounAndVerbSingularWhereTheLanguageDoes()
    {
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.AdjectiveAgreement != AdjectiveAgreement.Phrasal))
        {
            var counted = ThreeBigDogsSlept(language);
            var expected = new NounPhrase("dog", language.Numerals.NounStaysSingular ? Number.Singular : Number.Paucal);
            var plain = Syntax.Realize(language, new Clause("sleep", Tense.Past, Aspect.Perfective, expected), "x");

            Assert.Equal(plain.Words[IndexOf(plain, "dog")].Gloss, counted.Words[IndexOf(counted, "dog")].Gloss);
            Assert.Equal(plain.Words[IndexOf(plain, "sleep")].Gloss, counted.Words[IndexOf(counted, "sleep")].Gloss);
        }
    }

    [Fact]
    public void Realize_Numerals_AreGlossedByTheirNumbers()
    {
        var decimalLanguage = GeneratedLanguages.Sample.First(l => l.Numerals.Base == NumeralBase.Decimal);
        var sentence = Syntax.Realize(decimalLanguage, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Plural, Count: 37)), "x");

        var glosses = sentence.Words.Select(w => w.Gloss).Where(g => g is "three" or "ten" or "seven").ToList();
        Assert.Equal(decimalLanguage.Numerals.LargestFirst ? ["three", "ten", "seven"] : ["seven", "three", "ten"], glosses);
    }
}
