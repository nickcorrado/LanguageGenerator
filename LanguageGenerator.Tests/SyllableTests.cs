namespace LanguageGenerator.Tests;

public class SyllableTests
{
    private static SyllableTemplate Template(string name) => SyllableTemplates.All.Single(t => t.Name == name);

    private static List<Syllable> Generate(SyllableTemplate template, List<Consonant> consonants, int count)
    {
        var rule = new SyllableRule(template, TestData.Vowels, consonants);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => rule.GenerateSyllable(rand, 0.15))];
    }

    [Fact]
    public void Name_OfEachTemplate_ComesFromItsSlots()
    {
        Assert.Equal(["CV", "CVF", "VF", "CLV", "CLVF", "SCV", "SCVF", "SCLVF"], SyllableTemplates.All.Select(t => t.Name));
    }

    [Fact]
    public void Constructor_WithNoVowelSlot_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SyllableTemplate(new ConsonantSlot(1)));
    }

    [Fact]
    public void Constructor_WithTwoVowelSlots_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SyllableTemplate(new VowelSlot(), new VowelSlot()));
    }

    [Fact]
    public void Constructor_WithTwoCodaSlots_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SyllableTemplate(new VowelSlot(), new FinalSlot(1), new FinalSlot(1)));
    }

    [Theory]
    [InlineData("CV")]
    [InlineData("CVF")]
    [InlineData("VF")]
    [InlineData("CLV")]
    [InlineData("CLVF")]
    [InlineData("SCV")]
    [InlineData("SCVF")]
    [InlineData("SCLVF")]
    public void GenerateSyllable_AnyTemplate_StaysWithinItsSlots(string name)
    {
        var template = Template(name);
        var onsetSlots = template.Slots.TakeWhile(s => s is not VowelSlot).Count();
        var hasCoda = template.Slots.Any(s => s is FinalSlot);

        var syllables = Generate(template, TestData.Consonants, 2000);

        Assert.All(syllables, s => Assert.True(s.Onset.Count() <= onsetSlots));
        Assert.All(syllables, s => Assert.True(hasCoda || s.Coda is null));
    }

    [Fact]
    public void GenerateSyllable_FinalSlot_IsFilledAboutHalfTheTime()
    {
        var syllables = Generate(SyllableTemplates.CVF, TestData.Consonants, 20000);

        Assert.InRange(syllables.Count(s => s.Coda is not null) / 20000.0, 0.48, 0.52);
    }

    [Fact]
    public void GenerateSyllable_TwoConsonantOnsetInCLV_EndsInALiquid()
    {
        var twoConsonants = Generate(SyllableTemplates.CLV, TestData.Consonants, 20000).Where(s => s.Onset.Count() == 2).ToList();

        Assert.All(twoConsonants, s => Assert.True(s.Onset.Last().Manner is ConsonantManner.Approximant or ConsonantManner.LateralApproximant));
        //C fills half the time and L 30% of the time
        Assert.InRange(twoConsonants.Count / 20000.0, 0.13, 0.17);
    }

    [Fact]
    public void GenerateSyllable_CategoryPrunedFromInventory_LeavesThatSlotUnfilled()
    {
        var noSibilants = TestData.Consonants.Where(c => c.Manner != ConsonantManner.Sibilant).ToList();

        var syllables = Generate(SyllableTemplates.SCV, noSibilants, 2000);

        Assert.All(syllables, s => Assert.True(s.Onset.Count() <= 1 && !s.Onset.Any(c => c.Manner == ConsonantManner.Sibilant)));
    }

    [Theory]
    [InlineData("CLVF", "pl", true)]
    [InlineData("CLVF", "lp", false)]
    [InlineData("CLVF", "st", false)]
    [InlineData("SCLVF", "stl", true)]
    [InlineData("SCLVF", "sl", true)]
    [InlineData("VF", "t", false)]
    [InlineData("VF", "", true)]
    public void IsLegalOnset_FillsSlotsInOrder(string template, string onset, bool legal)
    {
        var rule = new SyllableRule(Template(template), TestData.Vowels, TestData.Consonants);

        Assert.Equal(legal, rule.IsLegalOnset(Consonants(onset)));
    }

    [Theory]
    [InlineData("CVF", "k", true)]
    [InlineData("CVF", "ks", false)]
    [InlineData("CV", "k", false)]
    [InlineData("CV", "", true)]
    public void IsLegalCoda_AllowsAtMostTheFinalSlot(string template, string coda, bool legal)
    {
        var rule = new SyllableRule(Template(template), TestData.Vowels, TestData.Consonants);

        Assert.Equal(legal, rule.IsLegalCoda(Consonants(coda)));
    }

    [Theory]
    [InlineData(CodaRestriction.Any, "k", true)]
    [InlineData(CodaRestriction.Sonorants, "l", true)]
    [InlineData(CodaRestriction.Sonorants, "m", true)]
    [InlineData(CodaRestriction.Sonorants, "t", false)]
    [InlineData(CodaRestriction.Nasals, "m", true)]
    [InlineData(CodaRestriction.Nasals, "l", false)]
    [InlineData(CodaRestriction.SingleNasal, "n", true)]
    [InlineData(CodaRestriction.SingleNasal, "m", false)]
    public void IsLegalCoda_WithARestriction_AllowsOnlyItsClass(CodaRestriction codas, string coda, bool legal)
    {
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, codas);

        Assert.Equal(legal, rule.IsLegalCoda(Consonants(coda)));
    }

    [Theory]
    [InlineData(CodaRestriction.Sonorants)]
    [InlineData(CodaRestriction.Nasals)]
    [InlineData(CodaRestriction.SingleNasal)]
    public void GenerateSyllable_WithARestriction_OnlyGeneratesLegalCodas(CodaRestriction codas)
    {
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, codas);
        var rand = new Random(1);

        var codaConsonants = Enumerable.Range(0, 5000).Select(_ => rule.GenerateSyllable(rand, 0.15).Coda).OfType<Consonant>().ToList();

        Assert.NotEmpty(codaConsonants);
        Assert.All(codaConsonants, c => Assert.True(rule.IsLegalCoda([c]), $"{c.IpaSymbol} isn't a legal {codas} coda"));
    }

    [Fact]
    public void SingleNasal_WithoutN_FallsBackOnAnotherNasal()
    {
        var noN = TestData.Consonants.Where(c => c != Consonant.n).ToList();

        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, noN, CodaRestriction.SingleNasal);

        Assert.True(rule.IsLegalCoda([Consonant.m]));
    }

    [Fact]
    public void CodaRestriction_LeavingNoConsonants_MeansNoCodas()
    {
        var noNasals = TestData.Consonants.Where(c => c.Manner != ConsonantManner.Nasal).ToList();
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, noNasals, CodaRestriction.Nasals);
        var rand = new Random(1);

        Assert.All(Enumerable.Range(0, 2000).Select(_ => rule.GenerateSyllable(rand, 0.15)), s => Assert.Null(s.Coda));
        Assert.All(noNasals, c => Assert.False(rule.IsLegalCoda([c])));
    }

    [Fact]
    public void SelectSyllableRule_ManyDraws_RestrictCodasOnlyWhereTheTemplateHasThem()
    {
        var rand = new Random(1);
        var rules = Enumerable.Range(0, 40000).Select(_ => Generator.SelectSyllableRule(rand, TestData.Vowels, TestData.Consonants)).ToList();
        //only templates with both an onset and a coda get a restriction; in VF every consonant is a coda
        var withCodas = rules.Where(r => r.Template.Slots.Any(s => s is FinalSlot) && r.Template.Slots[0] is not VowelSlot).ToList();

        Assert.All(rules.Except(withCodas), r => Assert.Equal(CodaRestriction.Any, r.Codas));
        Assert.Contains(rules, r => r.Template == SyllableTemplates.VF);
        double Share(CodaRestriction codas) => withCodas.Count(r => r.Codas == codas) / (double)withCodas.Count;
        Assert.InRange(Share(CodaRestriction.Any), 0.48, 0.52);
        Assert.InRange(Share(CodaRestriction.Sonorants), 0.23, 0.27);
        Assert.InRange(Share(CodaRestriction.Nasals), 0.13, 0.17);
        Assert.InRange(Share(CodaRestriction.SingleNasal), 0.08, 0.12);
    }

    [Fact]
    public void DistinctSyllables_CV_IsAnOptionalConsonantTimesAVowel()
    {
        var rule = new SyllableRule(SyllableTemplates.CV, TestData.Vowels, TestData.Consonants);

        Assert.Equal((1 + TestData.Consonants.Count) * TestData.Vowels.Count, rule.DistinctSyllables);
    }

    [Fact]
    public void DistinctSyllables_RestrictedCodas_CountOnlyTheirClass()
    {
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, CodaRestriction.SingleNasal);

        Assert.Equal((1 + TestData.Consonants.Count) * TestData.Vowels.Count * 2, rule.DistinctSyllables);
    }

    [Fact]
    public void SelectExtraSyllables_FewerPossibleSyllables_GiveLongerRoots()
    {
        var fewVowels = TestData.Vowels.Take(3).ToList();
        var fewConsonants = TestData.Consonants.Take(8).ToList();
        var simple = new SyllableRule(SyllableTemplates.CV, fewVowels, fewConsonants);
        var complex = new SyllableRule(SyllableTemplates.SCLVF, TestData.Vowels, TestData.Consonants);
        var rand = new Random(1);

        var simpleMean = Enumerable.Range(0, 200).Average(_ => Generator.SelectExtraSyllables(rand, simple));
        var complexMean = Enumerable.Range(0, 200).Average(_ => Generator.SelectExtraSyllables(rand, complex));

        Assert.True(simpleMean > complexMean + 1, $"CV roots average {1 + simpleMean:F2} syllables and SCLVF roots {1 + complexMean:F2}");
    }

    //single-letter IPA symbols only, which covers everything these tests need
    private static List<Consonant> Consonants(string ipa) =>
        [.. ipa.Select(symbol => TestData.Consonants.Single(c => c.IpaSymbol == symbol.ToString()))];
}
