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

    //single-letter IPA symbols only, which covers everything these tests need
    private static List<Consonant> Consonants(string ipa) =>
        [.. ipa.Select(symbol => TestData.Consonants.Single(c => c.IpaSymbol == symbol.ToString()))];
}
