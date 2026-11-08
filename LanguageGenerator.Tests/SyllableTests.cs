namespace LanguageGenerator.Tests;

public class SyllableTests
{
    private static SyllableTemplate Template(string name) => name == "VF" ? Templates.Onsetless : SyllableTemplates.All.Single(t => t.Name == name);

    private static List<Syllable> Generate(SyllableTemplate template, List<Consonant> consonants, int count)
    {
        var rule = new SyllableRule(template, TestData.Vowels, consonants);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GenerateSyllable(rand, rule, 0.15, needsOnset: false))];
    }

    [Fact]
    public void Name_OfEachTemplate_ComesFromItsSlots()
    {
        Assert.Equal(["CV", "CVF", "CLV", "CLVF", "SCV", "SCVF", "SCLVF"], SyllableTemplates.All.Select(t => t.Name));
        Assert.Equal("VF", Templates.Onsetless.Name);
    }

    [Fact]
    public void Constructor_WithNoVowelSlot_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SyllableTemplate(Slot.Consonant));
    }

    [Fact]
    public void Constructor_WithTwoVowelSlots_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SyllableTemplate(Slot.Vowel, Slot.Vowel));
    }

    [Fact]
    public void Constructor_WithTwoCodaSlots_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SyllableTemplate(Slot.Vowel, Slot.Final, Slot.Final));
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
        var onsetSlots = template.Slots.TakeWhile(s => s is not Slot.Vowel).Count();
        var hasCoda = template.Slots.Any(s => s is Slot.Final);

        var syllables = Generate(template, TestData.Consonants, 2000);

        Assert.All(syllables, s => Assert.True(s.Onset.Count <= onsetSlots));
        Assert.All(syllables, s => Assert.True(hasCoda || s.Coda is null));
    }

    [Fact]
    public void GenerateSyllable_FinalSlot_IsFilledAboutHalfTheTime()
    {
        var syllables = Generate(SyllableTemplates.CVF, TestData.Consonants, 20000);

        Assert.InRange(syllables.Count(s => s.Coda is not null) / 20000.0, 0.48, 0.52);
    }

    [Theory]
    [InlineData("p", "l", true)]
    [InlineData("t", "ɾ", true)]
    [InlineData("f", "r", true)]
    [InlineData("k", "w", true)]
    [InlineData("m", "j", true)]
    [InlineData("s", "t", true)]
    [InlineData("s", "n", true)]
    [InlineData("s", "kʷ", true)]
    [InlineData("n", "ɹ", false)]
    [InlineData("l", "ɹ", false)]
    [InlineData("n", "l", false)]
    [InlineData("l", "w", true)]
    [InlineData("r", "j", true)]
    [InlineData("w", "j", false)]
    [InlineData("p", "w", false)]
    [InlineData("t", "l", false)]
    [InlineData("ʔ", "l", false)]
    [InlineData("kʷ", "ɹ", false)]
    [InlineData("s", "tʰ", false)]
    [InlineData("s", "h", false)]
    [InlineData("s", "ʔ", false)]
    [InlineData("t", "k", false)]
    public void CanPrecede_FollowsSonorityAndTheCommonGaps(string first, string second, bool expected)
    {
        Consonant Sound(string ipa) => Consonant.All.Single(c => c.IpaSymbol == ipa);

        Assert.Equal(expected, SyllableRule.CanPrecede(Sound(first), Sound(second)));
    }

    [Fact]
    public void IsLegalOnset_NasalBeforeLiquid_IsIllegalEvenWhereTheSlotsFit()
    {
        var rule = new SyllableRule(SyllableTemplates.CLV, TestData.Vowels, TestData.Consonants);

        Assert.True(rule.IsLegalOnset([Consonant.p, Consonant.ɹ]));
        Assert.False(rule.IsLegalOnset([Consonant.n, Consonant.ɹ]));
    }

    [Fact]
    public void IsLegalCoda_AspiratedOrLabializedStop_IsNeverACoda()
    {
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);

        Assert.True(rule.IsLegalCoda([Consonant.k]));
        Assert.False(rule.IsLegalCoda([Consonant.kʰ]));
        Assert.False(rule.IsLegalCoda([Consonant.kʷ]));
        Assert.True(rule.IsLegalOnset([Consonant.kʰ]));
    }

    [Fact]
    public void GenerateSyllable_TwoConsonantOnsetInCLV_EndsInALiquid()
    {
        var twoConsonants = Generate(SyllableTemplates.CLV, TestData.Consonants, 20000).Where(s => s.Onset.Count == 2).ToList();

        Assert.All(twoConsonants, s => Assert.True(s.Onset.Last().Manner is ConsonantManner.Approximant or ConsonantManner.LateralApproximant or ConsonantManner.Trill or ConsonantManner.Flap));
        //C fills 85% of the time and L 30% of the time
        Assert.InRange(twoConsonants.Count / 20000.0, 0.24, 0.27);
    }

    [Fact]
    public void GenerateSyllable_AnyTemplate_UsuallyHasAnOnset()
    {
        Assert.All(SyllableTemplates.All, t =>
            Assert.InRange(Generate(t, TestData.Consonants, 5000).Count(s => s.Onset.Any()) / 5000.0, 0.82, 0.92));
    }

    [Fact]
    public void GenerateSyllable_InventoryWithoutTheSlotsClass_LeavesThatSlotUnfilled()
    {
        var noSibilants = TestData.Consonants.Where(c => c.Manner != ConsonantManner.Sibilant).ToList();

        var syllables = Generate(SyllableTemplates.SCV, noSibilants, 2000);

        Assert.All(syllables, s => Assert.True(s.Onset.Count <= 1 && !s.Onset.Any(c => c.Manner == ConsonantManner.Sibilant)));
    }

    [Theory]
    [InlineData("CLVF", "pl", true)]
    [InlineData("CLVF", "lp", false)]
    [InlineData("CLVF", "st", false)]
    [InlineData("SCLVF", "spl", true)]
    [InlineData("SCLVF", "stl", false)]
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

        var codaConsonants = Enumerable.Range(0, 5000).Select(_ => Generator.GenerateSyllable(rand, rule, 0.15, needsOnset: false).Coda).OfType<Consonant>().ToList();

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

        Assert.All(Enumerable.Range(0, 2000).Select(_ => Generator.GenerateSyllable(rand, rule, 0.15, needsOnset: false)), s => Assert.Null(s.Coda));
        Assert.All(noNasals, c => Assert.False(rule.IsLegalCoda([c])));
    }

    [Fact]
    public void SelectSyllableRule_ManyDraws_RestrictCodasOnlyWhereTheTemplateHasThem()
    {
        var rand = new Random(1);
        var rules = Enumerable.Range(0, 40000).Select(_ => Generator.SelectSyllableRule(rand, TestData.Vowels, TestData.Consonants)).ToList();
        //only templates with a coda get a restriction, and every template has an onset
        var withCodas = rules.Where(r => r.Template.Slots.Any(s => s is Slot.Final)).ToList();

        Assert.All(rules.Except(withCodas), r => Assert.Equal(CodaRestriction.Any, r.Codas));
        Assert.All(rules, r => Assert.NotEqual(Slot.Vowel, r.Template.Slots[0]));
        double Share(CodaRestriction codas) => withCodas.Count(r => r.Codas == codas) / (double)withCodas.Count;
        Assert.InRange(Share(CodaRestriction.Any), 0.48, 0.52);
        Assert.InRange(Share(CodaRestriction.Sonorants), 0.23, 0.27);
        Assert.InRange(Share(CodaRestriction.Nasals), 0.13, 0.17);
        Assert.InRange(Share(CodaRestriction.SingleNasal), 0.08, 0.12);
    }

    [Fact]
    public void SelectSyllableRule_ManyDraws_MatchWalsSyllableComplexity()
    {
        var rand = new Random(1);
        var templates = Enumerable.Range(0, 40000).Select(_ => Generator.SelectSyllableRule(rand, TestData.Vowels, TestData.Consonants).Template).ToList();

        //WALS 12A: 61 simple, 274 moderately complex and 151 complex of 486. A sibilant slot makes a
        //template complex, and anything else beyond CV moderately so
        double Share(Func<SyllableTemplate, bool> which) => templates.Count(which) / 40000.0;
        Assert.InRange(Share(t => t == SyllableTemplates.CV), 61 / 486.0 - 0.01, 61 / 486.0 + 0.01);
        Assert.InRange(Share(t => t != SyllableTemplates.CV && !t.Slots.Contains(Slot.Sibilant)), 274 / 486.0 - 0.01, 274 / 486.0 + 0.01);
        Assert.InRange(Share(t => t.Slots.Contains(Slot.Sibilant)), 151 / 486.0 - 0.01, 151 / 486.0 + 0.01);
        //and every template comes up
        Assert.Equal(SyllableTemplates.All.Count, templates.Distinct().Count());
    }

    private static double ExtraSyllables(Phonology phonology, double noise = 1) =>
        Enumerable.Range(0, 20).Average(seed => Generator.SelectExtraSyllables(noise, seed, phonology, 0.15));

    [Fact]
    public void SelectExtraSyllables_FewerLikelySyllables_GiveLongerRoots()
    {
        var fewVowels = TestData.Vowels.Take(3).ToList();
        var fewConsonants = TestData.Consonants.Take(8).ToList();
        var simple = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis) with
        {
            Vowels = fewVowels,
            Consonants = fewConsonants,
            SyllableRule = new SyllableRule(SyllableTemplates.CV, fewVowels, fewConsonants),
        };
        var complex = TestData.Phonology(SyllableTemplates.SCLVF, ClusterRepair.Epenthesis);

        var (simpleMean, complexMean) = (ExtraSyllables(simple), ExtraSyllables(complex));

        Assert.True(simpleMean > complexMean + 0.5, $"CV roots average {1 + simpleMean:F2} syllables and SCLVF roots {1 + complexMean:F2}");
    }

    [Fact]
    public void SelectExtraSyllables_SoundChangesThatMergeSounds_GiveLongerRoots()
    {
        var merger = new SoundChange("every consonant becomes k", site => site.Sound is Consonant, (_, _) => Consonant.k);
        var plain = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis);

        Assert.True(ExtraSyllables(plain with { SoundChanges = [merger] }) > ExtraSyllables(plain));
    }

    [Fact]
    public void SelectExtraSyllables_MoreNoise_GivesLongerRoots()
    {
        var phonology = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis);

        Assert.True(ExtraSyllables(phonology, 1.25) > ExtraSyllables(phonology, 0.75));
    }

    [Fact]
    public void Generate_AnyLanguage_RootsStayMostlyDistinct()
    {
        static int Homophones(Language language)
        {
            var forms = Enumerable.Range(0, 250).Select(i => string.Concat(language.Phonology.Realize(new Word([language.Lexicon.Noun($"word {i}").Root])).Phonemes.Select(p => p.IpaSymbol)));
            return forms.GroupBy(form => form).Sum(same => same.Count() * (same.Count() - 1) / 2);
        }

        var homophones = Enumerable.Range(0, 40).Select(seed => Homophones(Generator.Generate(new Random(seed)))).ToList();

        Assert.InRange(homophones.Average(), 3, 20);
    }

    //single-letter IPA symbols only, which covers everything these tests need
    private static List<Consonant> Consonants(string ipa) =>
        [.. ipa.Select(symbol => TestData.Consonants.Single(c => c.IpaSymbol == symbol.ToString()))];
}
