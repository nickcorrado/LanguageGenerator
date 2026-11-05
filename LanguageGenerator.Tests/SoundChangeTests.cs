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
        var drawn = Enumerable.Range(0, 2000).SelectMany(_ => Generator.SelectLaterSoundChanges(rand, rule, TestData.Vowels)).ToList();

        Assert.InRange(drawn.Count(c => c == SoundChange.Lenition) / 2000.0, 0.22, 0.28);
        Assert.InRange(drawn.Count(c => c == SoundChange.VoicingAssimilation) / 2000.0, 0.27, 0.33);
        Assert.InRange(drawn.Count(c => c == SoundChange.FinalDevoicing) / 2000.0, 0.27, 0.33);
        Assert.InRange(drawn.Count(c => c == SoundChange.VowelElision) / 2000.0, 0.37, 0.43);
        Assert.InRange(drawn.Count(c => c == SoundChange.FrontBackHarmony) / 2000.0, 0.13, 0.17);
        Assert.InRange(drawn.Count(c => c == SoundChange.TongueRootHarmony) / 2000.0, 0.06, 0.10);
    }

    [Fact]
    public void SelectLaterSoundChanges_WithoutVowelsToPair_DrawsNoHarmony()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);
        var drawn = Enumerable.Range(0, 2000).SelectMany(_ => Generator.SelectLaterSoundChanges(rand, rule, [Vowel.a, Vowel.i, Vowel.ə])).ToList();

        Assert.DoesNotContain(SoundChange.FrontBackHarmony, drawn);
        Assert.DoesNotContain(SoundChange.TongueRootHarmony, drawn);
    }

    [Fact]
    public void Generate_HarmonyAndElision_HarmonyComesFirst()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var changes = l.Phonology.SoundChanges;
            var elision = changes.IndexOf(SoundChange.VowelElision);
            Assert.True(elision < 0 || elision > changes.IndexOf(SoundChange.FrontBackHarmony));
            Assert.True(elision < 0 || elision > changes.IndexOf(SoundChange.TongueRootHarmony));
        });
    }

    //a change that depends on syllables, applied to a word of one morpheme in a CVF language
    private static SurfaceForm Syllabic(SoundChange change, params Phoneme[] sounds) =>
        (TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { SyllableChanges = [change] }).Realize(TestData.Word(sounds));

    private static string Sounds(SurfaceForm form) => string.Concat(form.Phonemes.Select(p => p.IpaSymbol));

    [Fact]
    public void CodaDevoicing_DevoicesAtTheEndOfAnySyllable()
    {
        Assert.Equal("tak", Sounds(Syllabic(SoundChange.CodaDevoicing, Consonant.t, Vowel.a, Consonant.g)));
        Assert.Equal("takma", Sounds(Syllabic(SoundChange.CodaDevoicing, Consonant.t, Vowel.a, Consonant.g, Consonant.m, Vowel.a)));
    }

    [Fact]
    public void CodaDevoicing_LeavesAnOnsetAlone()
    {
        Assert.Equal("taga", Sounds(Syllabic(SoundChange.CodaDevoicing, Consonant.t, Vowel.a, Consonant.g, Vowel.a)));
    }

    //a suffix's vowel makes the g an onset, as German Tage does
    [Fact]
    public void CodaDevoicing_AcrossAMorphemeBoundary_GoesBySyllablesNotMorphemes()
    {
        var phonology = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { SyllableChanges = [SoundChange.CodaDevoicing] };

        Assert.Equal("taga", Sounds(phonology.Realize(TestData.Word([Consonant.t, Vowel.a, Consonant.g], [Vowel.a]))));
    }

    [Fact]
    public void CodaWeakening_WeakensAnSClosingASyllable()
    {
        Assert.Equal("pah", Sounds(Syllabic(SoundChange.CodaWeakening, Consonant.p, Vowel.a, Consonant.s)));
        Assert.Equal("pahta", Sounds(Syllabic(SoundChange.CodaWeakening, Consonant.p, Vowel.a, Consonant.s, Consonant.t, Vowel.a)));
        Assert.Equal("pasa", Sounds(Syllabic(SoundChange.CodaWeakening, Consonant.p, Vowel.a, Consonant.s, Vowel.a)));
    }

    [Fact]
    public void CodaWeakening_WithoutH_LeavesTheSAlone()
    {
        List<Consonant> noH = [.. Consonant.All.Where(c => c != Consonant.h)];
        var phonology = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis, noH) with { SyllableChanges = [SoundChange.CodaWeakening] };

        Assert.Equal("pas", Sounds(phonology.Realize(TestData.Word([Consonant.p, Vowel.a, Consonant.s]))));
    }

    [Fact]
    public void ClosedSyllableLaxing_OpensAVowelOnlyInAClosedSyllable()
    {
        Assert.Equal("pɛt", Sounds(Syllabic(SoundChange.ClosedSyllableLaxing, Consonant.p, Vowel.e, Consonant.t)));
        Assert.Equal("kɔl", Sounds(Syllabic(SoundChange.ClosedSyllableLaxing, Consonant.k, Vowel.o, Consonant.l)));
        Assert.Equal("peta", Sounds(Syllabic(SoundChange.ClosedSyllableLaxing, Consonant.p, Vowel.e, Consonant.t, Vowel.a)));
    }

    [Fact]
    public void ClosedSyllableLaxing_WithoutTheOpenVowel_LeavesTheVowelAlone()
    {
        var phonology = (TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { Vowels = [Vowel.a, Vowel.e], SyllableChanges = [SoundChange.ClosedSyllableLaxing] });

        Assert.Equal("pet", Sounds(phonology.Realize(TestData.Word([Consonant.p, Vowel.e, Consonant.t]))));
    }

    //the surface form's syllables are the ones after the change, not before it
    [Fact]
    public void Realize_SyllableChanges_LeaveSyllablesThatMatchTheSounds()
    {
        var form = Syllabic(SoundChange.CodaDevoicing, Consonant.t, Vowel.a, Consonant.g, Consonant.m, Vowel.a);

        Assert.Equal(Consonant.k, form.Syllables[0].Coda);
        Assert.Equal("tak.ma", form.Syllabified(Orthography.Ipa));
    }

    //in a language with no codas, an epenthetic vowel leaves the g an onset by the time the change runs
    [Fact]
    public void Realize_SyllableChanges_SeeTheWordAfterRepair()
    {
        var form = (TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis) with { SyllableChanges = [SoundChange.CodaDevoicing] })
            .Realize(TestData.Word([Consonant.t, Vowel.a, Consonant.g]));

        Assert.Contains(Consonant.g, form.Phonemes);
    }

    [Fact]
    public void SelectSyllableChanges_ManyDraws_EachComeUp()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);
        var drawn = Enumerable.Range(0, 3000).SelectMany(_ => Generator.SelectSyllableChanges(rand, rule)).ToList();

        Assert.InRange(drawn.Count(c => c == SoundChange.CodaDevoicing) / 3000.0, 0.17, 0.23);
        Assert.InRange(drawn.Count(c => c == SoundChange.CodaWeakening) / 3000.0, 0.13, 0.17);
        Assert.InRange(drawn.Count(c => c == SoundChange.ClosedSyllableLaxing) / 3000.0, 0.13, 0.17);
    }

    [Fact]
    public void SelectSyllableChanges_WithoutCodas_DrawsNone()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CV, TestData.Vowels, TestData.Consonants);

        Assert.Empty(Enumerable.Range(0, 500).SelectMany(_ => Generator.SelectSyllableChanges(rand, rule)));
    }

    [Fact]
    public void SelectSyllableChanges_WithSonorantCodas_LaxesButNeverDevoices()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, CodaRestriction.Sonorants);
        var drawn = Enumerable.Range(0, 500).SelectMany(_ => Generator.SelectSyllableChanges(rand, rule)).ToList();

        Assert.Contains(SoundChange.ClosedSyllableLaxing, drawn);
        Assert.DoesNotContain(SoundChange.CodaDevoicing, drawn);
        Assert.DoesNotContain(SoundChange.CodaWeakening, drawn);
    }

    private static readonly Phoneme[] La = [Consonant.l, Vowel.a];
    private static readonly Phoneme[] Læ = [Consonant.l, Vowel.æ];

    [Fact]
    public void FrontBackHarmony_SuffixTakesTheRootsSide()
    {
        Assert.Equal("kælæ", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, [Consonant.k, Vowel.æ], La));
        Assert.Equal("kala", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, Ka, Læ));
        Assert.Equal("kele", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, [Consonant.k, Vowel.e], [Consonant.l, Vowel.o]));
    }

    [Fact]
    public void FrontBackHarmony_RootsFirstHarmonizingVowel_RulesTheRootToo()
    {
        Assert.Equal("kala", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, [.. Ka, .. Læ]));
    }

    [Fact]
    public void FrontBackHarmony_NeutralVowels_StayAndLetItPass()
    {
        //i has no partner, so it doesn't change, and doesn't stop a from deciding the word
        Assert.Equal("kilala", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, [Consonant.k, Vowel.i, .. La], Læ));
        Assert.Equal("kali", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, Ka, [Consonant.l, Vowel.i]));
        //with only neutral vowels in the root, nothing decides the word
        Assert.Equal("kilæ", Surface(SyllableTemplates.CV, SoundChange.FrontBackHarmony, [Consonant.k, Vowel.i], Læ));
    }

    [Fact]
    public void FrontBackHarmony_Prefix_AgreesWithTheRootToo()
    {
        var word = new Word([new Affix([Consonant.l, Vowel.æ], [Number.Plural]), new Root(Ka, "dog")]);
        var phonology = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, SoundChange.FrontBackHarmony);

        Assert.Equal("laka", string.Concat(phonology.Realize(word).Phonemes.Select(p => p.IpaSymbol)));
    }

    [Fact]
    public void FrontBackHarmony_WithoutThePartnerInTheInventory_LeavesTheVowelAlone()
    {
        var phonology = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, SoundChange.FrontBackHarmony) with { Vowels = [Vowel.a, Vowel.e] };

        //a is on the back side, but this language has no o for e to become
        Assert.Equal("kale", string.Concat(phonology.Realize(TestData.Word(Ka, [Consonant.l, Vowel.e])).Phonemes.Select(p => p.IpaSymbol)));
    }

    [Fact]
    public void TongueRootHarmony_SuffixTakesTheRootsSide()
    {
        Assert.Equal("kele", Surface(SyllableTemplates.CV, SoundChange.TongueRootHarmony, [Consonant.k, Vowel.e], [Consonant.l, Vowel.ɛ]));
        Assert.Equal("kɔlɛ", Surface(SyllableTemplates.CV, SoundChange.TongueRootHarmony, [Consonant.k, Vowel.ɔ], [Consonant.l, Vowel.e]));
    }

    [Fact]
    public void Harmony_WordWithNoRoot_IsLeftAlone()
    {
        var word = new Word([new Particle([Consonant.l, Vowel.æ, Consonant.l, Vowel.a], [Number.Plural])]);
        var phonology = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, SoundChange.FrontBackHarmony);

        Assert.Equal("læla", string.Concat(phonology.Realize(word).Phonemes.Select(p => p.IpaSymbol)));
    }

    [Fact]
    public void Harmony_InASegmentedWord_ShowsOnEachMorpheme()
    {
        var phonology = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, SoundChange.FrontBackHarmony);

        Assert.Equal("kæ-læ", phonology.Realize(TestData.Word([Consonant.k, Vowel.æ], La)).Segmented(Orthography.Ipa));
    }
}
