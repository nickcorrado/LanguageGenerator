namespace LanguageGenerator.Tests;

public class SoundChangeTests
{
    private static readonly Phoneme[] Ka = [Consonant.k, Vowel.a];

    private static string Surface(SyllableTemplate template, SoundChange change, params Phoneme[][] morphemes) =>
        string.Concat(TestData.Phonology(template, ClusterRepair.Epenthesis, change).Realize(TestData.Word(morphemes)).Phonemes.Select(p => p.IpaSymbol));

    [Theory]
    [InlineData("p", "f")]
    [InlineData("b", "v")]
    [InlineData("t", "θ")]
    [InlineData("d", "ð")]
    [InlineData("k", "x")]
    [InlineData("g", "w")]
    [InlineData("ʧ", "ʃ")]
    [InlineData("ʤ", "ʒ")]
    public void Lenition_BetweenVowels_WeakensAStop(string stop, string weakened)
    {
        var consonant = Consonant.All.Single(c => c.IpaSymbol == stop);

        Assert.Equal($"ka{weakened}a", Surface(SyllableTemplates.CV, SoundChange.Lenition, [.. Ka, consonant, Vowel.a]));
    }

    [Fact]
    public void Lenition_WithoutTheLikeliestSound_TakesTheNext()
    {
        List<Consonant> noθ = [.. Consonant.All.Where(c => c != Consonant.θ)];

        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, noθ, SoundChange.Lenition).Realize(TestData.Word([Vowel.a, Consonant.t, Vowel.a]));

        Assert.Equal("asa", string.Concat(surface.Phonemes.Select(p => p.IpaSymbol)));
    }

    [Fact]
    public void NasalAssimilation_BeforeAVelar_GivesAVelarNasal()
    {
        Assert.Equal("kaŋka", Surface(SyllableTemplates.CVF, SoundChange.NasalAssimilation, [.. Ka, Consonant.n], Ka));
    }

    [Fact]
    public void Lenition_NotBetweenVowels_LeavesAStopAlone()
    {
        Assert.Equal("pat", Surface(SyllableTemplates.CVF, SoundChange.Lenition, [Consonant.p, Vowel.a, Consonant.t]));
    }

    [Fact]
    public void Lenition_WithoutTheSoundInTheInventory_LeavesAStopAlone()
    {
        List<Consonant> noF = [.. Consonant.All.Where(c => c != Consonant.f)];

        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, noF, SoundChange.Lenition).Realize(TestData.Word([Vowel.a, Consonant.p, Vowel.a]));

        Assert.Equal("apa", string.Concat(surface.Phonemes.Select(p => p.IpaSymbol)));
    }

    [Theory]
    [InlineData("p", "d", "bd")]
    [InlineData("b", "t", "pt")]
    [InlineData("s", "d", "zd")]
    [InlineData("p", "t", "pt")]
    [InlineData("p", "n", "pn")]
    public void VoicingAssimilation_TakesTheVoicingOfTheObstruentAfter(string first, string second, string result)
    {
        Consonant Sound(string ipa) => Consonant.All.Single(c => c.IpaSymbol == ipa);

        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Deletion, SoundChange.VoicingAssimilation);
        var changed = SoundChange.VoicingAssimilation.Apply([new(Vowel.a, 0), new(Sound(first), 0), new(Sound(second), 1), new(Vowel.a, 1)], surface);

        Assert.Equal($"a{result}a", string.Concat(changed.Select(s => s.Phoneme.IpaSymbol)));
    }

    [Fact]
    public void FinalDevoicing_DevoicesOnlyAtTheEndOfAWord()
    {
        Assert.Equal("dap", Surface(SyllableTemplates.CVF, SoundChange.FinalDevoicing, [Consonant.d, Vowel.a, Consonant.b]));
        Assert.Equal("kaban", Surface(SyllableTemplates.CVF, SoundChange.FinalDevoicing, [.. Ka, Consonant.b, Vowel.a, Consonant.n]));
    }

    [Fact]
    public void VowelElision_AtAMorphemeBoundary_LosesTheFirstVowel()
    {
        Assert.Equal("ki", Surface(SyllableTemplates.CV, SoundChange.VowelElision, Ka, [Vowel.i]));
        //within a morpheme, the vowels stay
        Assert.Equal("kaai", Surface(SyllableTemplates.CV, SoundChange.VowelElision, [.. Ka, Vowel.a, Vowel.i]));
    }

    [Fact]
    public void VowelElision_OfAWholeMorpheme_LeavesItEmptyInTheSegmentation()
    {
        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, SoundChange.VowelElision).Realize(TestData.Word(Ka, [Vowel.a], [Vowel.i]));

        Assert.Equal("ki", string.Concat(surface.Phonemes.Select(p => p.IpaSymbol)));
        Assert.Contains("∅", surface.Segmented(Orthography.Ipa));
    }

    //p voices to b, and then b weakens to v, so one change feeds the next
    [Fact]
    public void Realize_ChangesInOrder_CanFeedEachOther()
    {
        var phonology = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, SoundChange.IntervocalicVoicing, SoundChange.Lenition);

        Assert.Equal("ava", string.Concat(phonology.Realize(TestData.Word([Vowel.a, Consonant.p, Vowel.a])).Phonemes.Select(p => p.IpaSymbol)));
    }

    [Fact]
    public void Generate_AnyLanguage_OnlyDevoicesFinallyIfSyllablesCanEndInAConsonant()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Phonology.SoundChanges.Contains(SoundChange.FinalDevoicing)),
            l => Assert.Contains(l.Phonology.SyllableRule.Template.Slots, s => s is FinalSlot));
    }

    [Fact]
    public void SelectLaterSoundChanges_ManyDraws_EachComeUp()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);
        var drawn = Enumerable.Range(0, 2000).SelectMany(_ => Generator.SelectLaterSoundChanges(rand, rule)).ToList();

        Assert.InRange(drawn.Count(c => c == SoundChange.Lenition) / 2000.0, 0.22, 0.28);
        Assert.InRange(drawn.Count(c => c == SoundChange.VoicingAssimilation) / 2000.0, 0.27, 0.33);
        Assert.InRange(drawn.Count(c => c == SoundChange.FinalDevoicing) / 2000.0, 0.27, 0.33);
        Assert.InRange(drawn.Count(c => c == SoundChange.VowelElision) / 2000.0, 0.37, 0.43);
    }
}
