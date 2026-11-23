namespace LanguageGenerator.Tests;

public class StressTests
{
    //a word from a pattern of syllables: L a light ta, H a heavy tak, V a heavy taː
    private static List<Syllable> Syllables(string pattern) =>
    [
        .. pattern.Select(c => c switch
        {
            'L' => new Syllable([Consonant.t], Vowel.a, null),
            'H' => new Syllable([Consonant.t], Vowel.a, Consonant.k),
            'V' => new Syllable([Consonant.t], Vowel.aː, null),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern)),
        }),
    ];

    [Theory]
    [InlineData(Stress.Initial, "LLL", 0)]
    [InlineData(Stress.Second, "LLL", 1)]
    [InlineData(Stress.Second, "L", 0)]
    [InlineData(Stress.Third, "LL", 1)]
    [InlineData(Stress.Antepenultimate, "LLLL", 1)]
    [InlineData(Stress.Antepenultimate, "LL", 0)]
    [InlineData(Stress.Penultimate, "LLL", 1)]
    [InlineData(Stress.Penultimate, "L", 0)]
    [InlineData(Stress.Ultimate, "LLL", 2)]
    [InlineData(Stress.UltimateIfHeavy, "LLH", 2)]
    [InlineData(Stress.UltimateIfHeavy, "LLV", 2)]
    [InlineData(Stress.UltimateIfHeavy, "LLL", 1)]
    [InlineData(Stress.PenultimateIfHeavy, "LHL", 1)]
    [InlineData(Stress.PenultimateIfHeavy, "LVL", 1)]
    [InlineData(Stress.PenultimateIfHeavy, "LLL", 0)]
    [InlineData(Stress.PenultimateIfHeavy, "LL", 0)]
    [InlineData(Stress.InitialIfHeavy, "HLL", 0)]
    [InlineData(Stress.InitialIfHeavy, "LLL", 1)]
    [InlineData(Stress.LastHeavy, "LHLHL", 3)]
    [InlineData(Stress.LastHeavy, "LLL", 0)]
    public void StressedSyllable_FollowsThePlaceOrTheWeight(Stress stress, string pattern, int expected)
    {
        Assert.Equal(expected, stress.StressedSyllable(Syllables(pattern)));
    }

    private static Phonology WithStress(Stress stress, params SoundChange[] syllableChanges) =>
        TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { Stress = new StressSystem(stress, false), SyllableChanges = syllableChanges };

    private static Phonology WithFreeStress(Stress stress, params SoundChange[] soundChanges) =>
        TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis, soundChanges) with { Stress = new StressSystem(stress, true) };

    private static readonly Phoneme[] Pata = [Consonant.p, Vowel.a, Consonant.t, Vowel.a];

    //a root storing an accent on the given vowel, or none, with plural suffixes after it
    private static Word Accented(int? accent, Phoneme[] root, params Phoneme[][] suffixes) =>
        new([new Root(root, "dog", accent), .. suffixes.Select(s => (Morpheme)new Affix(s, [Number.Plural]))]);

    [Fact]
    public void Realize_FreeStress_KeepsAStoredAccentWhereASuffixWouldMoveTheRules()
    {
        var phonology = WithFreeStress(Stress.Penultimate);

        Assert.Equal("ˈpa.ta.ma", phonology.Realize(Accented(0, Pata, [Consonant.m, Vowel.a])).Transcribed());
        Assert.Equal("paˈta", phonology.Realize(Accented(1, Pata)).Transcribed());
        //a root that stores none takes the rule's stress, which the suffix moves
        Assert.Equal("paˈta.ma", phonology.Realize(Accented(null, Pata, [Consonant.m, Vowel.a])).Transcribed());
    }

    [Fact]
    public void Realize_FreeStress_PassesTheAccentOfALostVowelToTheOneItMet()
    {
        Phoneme[] pati = [Consonant.p, Vowel.a, Consonant.t, Vowel.i];
        Phoneme[] pate = [Consonant.p, Vowel.a, Consonant.t, Vowel.e];

        //elided before the suffix's vowel, which takes its place
        Assert.Equal(1, WithFreeStress(Stress.Initial, SoundChange.VowelElision).Realize(Accented(1, pate, [Vowel.o, Consonant.m, Vowel.a])).StressedSyllable);
        //turned into a glide before it
        Assert.Equal("patˈjo.ma", WithFreeStress(Stress.Initial, SoundChange.GlideFormation).Realize(Accented(1, pati, [Vowel.o, Consonant.m, Vowel.a])).Transcribed());
        //merged into the vowel before it
        Assert.Equal("paˈteː", WithFreeStress(Stress.Initial, SoundChange.IdenticalVowelFusion).Realize(Accented(2, [.. pate, Vowel.e])).Transcribed());
    }

    [Fact]
    public void Realize_AWordWithoutARoot_HasNoStress()
    {
        var phonology = WithStress(Stress.Penultimate, SoundChange.UnstressedReduction);
        var particle = phonology.Realize(new Word([new Particle(Pata, [Definiteness.Definite])]));

        Assert.Null(particle.StressedSyllable);
        //so it's unmarked, and every vowel it has is unstressed
        Assert.Equal("pə.tə", particle.Transcribed());
    }

    [Fact]
    public void Transcribed_MarksTheStressedSyllableButNotAWordOfOne()
    {
        Assert.Equal("paˈta.ma", WithStress(Stress.Penultimate).Realize(TestData.Word([.. Pata, Consonant.m, Vowel.a])).Transcribed());
        Assert.Equal("ˈpa.ta", WithStress(Stress.Initial).Realize(TestData.Word(Pata)).Transcribed());
        Assert.Equal("pa", WithStress(Stress.Initial).Realize(TestData.Word([Consonant.p, Vowel.a])).Transcribed());
    }

    [Fact]
    public void Realize_ASuffix_MovesThePenultimateStress()
    {
        var phonology = WithStress(Stress.Penultimate);

        Assert.Equal(0, phonology.Realize(TestData.Word(Pata)).StressedSyllable);
        Assert.Equal(1, phonology.Realize(TestData.Word(Pata, [Consonant.m, Vowel.a])).StressedSyllable);
    }

    private static string Sounds(Phonology phonology, params Phoneme[][] morphemes) =>
        string.Concat(phonology.Realize(TestData.Word(morphemes)).Phonemes.Select(p => p.IpaSymbol));

    [Fact]
    public void UnstressedReduction_WeakensUnstressedVowelsToSchwa()
    {
        Assert.Equal("patə", Sounds(WithStress(Stress.Penultimate, SoundChange.UnstressedReduction), Pata));
        //close vowels don't reduce
        Assert.Equal("pati", Sounds(WithStress(Stress.Penultimate, SoundChange.UnstressedReduction), [Consonant.p, Vowel.a, Consonant.t, Vowel.i]));
    }

    [Fact]
    public void UnstressedRaising_RaisesUnstressedMidVowels()
    {
        Assert.Equal("petu", Sounds(WithStress(Stress.Penultimate, SoundChange.UnstressedRaising), [Consonant.p, Vowel.e, Consonant.t, Vowel.o]));
        Assert.Equal("pito", Sounds(WithStress(Stress.Ultimate, SoundChange.UnstressedRaising), [Consonant.p, Vowel.e, Consonant.t, Vowel.o]));
    }

    [Fact]
    public void UnstressedShortening_ShortensLongVowelsOutsideTheStress()
    {
        Assert.Equal("patama", Sounds(WithStress(Stress.Penultimate, SoundChange.UnstressedShortening), [Consonant.p, Vowel.aː, Consonant.t, Vowel.a, Consonant.m, Vowel.a]));
        Assert.Equal("paːta", Sounds(WithStress(Stress.Penultimate, SoundChange.UnstressedShortening), [Consonant.p, Vowel.aː, Consonant.t, Vowel.a]));
    }

    [Fact]
    public void OpenSyllableLengthening_LengthensAStressedVowelInAnOpenSyllable()
    {
        Assert.Equal("paːta", Sounds(WithStress(Stress.Penultimate, SoundChange.OpenSyllableLengthening), Pata));
        //but not in a closed one
        Assert.Equal("pakta", Sounds(WithStress(Stress.Penultimate, SoundChange.OpenSyllableLengthening), [Consonant.p, Vowel.a, Consonant.k, Consonant.t, Vowel.a]));
    }

    [Fact]
    public void Syncope_LosesTheVowelRightAfterTheStress()
    {
        Phoneme[] calida = [Consonant.k, Vowel.a, Consonant.l, Vowel.i, Consonant.d, Vowel.a];

        Assert.Equal("kalda", Sounds(WithStress(Stress.Antepenultimate, SoundChange.Syncope), calida));
        //with the stress on the i, nothing comes right after it between single consonants but the last vowel
        Assert.Equal("kalida", Sounds(WithStress(Stress.Penultimate, SoundChange.Syncope), calida));
        //only one vowel goes, the one right after the stress
        Assert.Equal("kalidma", Sounds(WithStress(Stress.Antepenultimate, SoundChange.Syncope), [.. calida, Consonant.m, Vowel.a]));
    }

    [Fact]
    public void Syncope_WhereTheConsonantCantCloseASyllable_LeavesTheVowel()
    {
        var phonology = WithStress(Stress.Antepenultimate, SoundChange.Syncope) with
        {
            SyllableRule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, CodaRestriction.Nasals),
        };

        Assert.Equal("kalida", Sounds(phonology, [Consonant.k, Vowel.a, Consonant.l, Vowel.i, Consonant.d, Vowel.a]));
    }

    private static readonly Phoneme[] Patama = [.. Pata, Consonant.m, Vowel.a];

    [Fact]
    public void Apocope_LosesAFinalUnstressedVowel_AndTheRulePlacesTheStressAgain()
    {
        Assert.Equal("ˈpa.tam", WithStress(Stress.Penultimate, SoundChange.Apocope).Realize(TestData.Word(Patama)).Transcribed());
        //a stressed last vowel stays
        Assert.Equal("pa.taˈma", WithStress(Stress.Ultimate, SoundChange.Apocope).Realize(TestData.Word(Patama)).Transcribed());
    }

    [Fact]
    public void Apocope_LeavesAStoredAccentOnItsVowel()
    {
        var phonology = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with
        {
            Stress = new StressSystem(Stress.Penultimate, true), SyllableChanges = [SoundChange.Apocope],
        };

        Assert.Equal("paˈtam", phonology.Realize(Accented(1, Patama)).Transcribed());
    }

    [Fact]
    public void Apocope_KeepsAVowelWhoseLossWouldLeaveAnIllegalEndOrNoVowel()
    {
        var phonology = WithStress(Stress.Penultimate, SoundChange.Apocope);

        //CVF ends a word in one consonant at most
        Assert.Equal("pakta", Sounds(phonology, [Consonant.p, Vowel.a, Consonant.k, Consonant.t, Vowel.a]));
        //after a vowel, there's no consonant to end the word
        Assert.Equal("patia", Sounds(phonology, [Consonant.p, Vowel.a, Consonant.t, Vowel.i, Vowel.a]));
        //a word without a root is stressless, but keeps its only vowel
        Assert.Equal("pat", string.Concat(phonology.Realize(new Word([new Particle(Pata, [Definiteness.Definite])])).Phonemes.Select(p => p.IpaSymbol)));
        Assert.Equal("pa", string.Concat(phonology.Realize(new Word([new Particle([Consonant.p, Vowel.a], [Definiteness.Definite])])).Phonemes.Select(p => p.IpaSymbol)));
        //and a coda restriction limits what can end it
        var nasals = phonology with { SyllableRule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, CodaRestriction.Nasals) };
        Assert.Equal("pata", Sounds(nasals, Pata));
        Assert.Equal("pam", Sounds(nasals, [Consonant.p, Vowel.a, Consonant.m, Vowel.a]));
    }

    [Fact]
    public void Generate_GeneratedLanguages_StressOneSyllableOfEveryWord()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.All(l.Lexicon.Select(x => l.Phonology.Realize(new Word([x.Root]))), form =>
        {
            Assert.InRange(form.StressedSyllable!.Value, 0, form.Syllables.Count - 1);
            Assert.Equal(form.Syllables.Count > 1 ? 1 : 0, form.Transcribed().Count(c => c == 'ˈ'));
        }));
    }

    [Fact]
    public void Generate_OnlyFreeStress_StoresAccents()
    {
        var free = GeneratedLanguages.With(spec => spec.Fix(Settings.FreeStress, true));
        var roots = free.Lexicon.Select(x => x.Root).ToList();

        Assert.Contains(roots, r => r.Accent is not null);
        Assert.Contains(roots, r => r.Accent is null);
        Assert.All(roots, r => Assert.True(r.Accent is null || r.Accent < r.Form.Count(p => p is Vowel)));
        Assert.All(GeneratedLanguages.Sample.Where(l => !l.Phonology.Stress.Free), l => Assert.All(l.Lexicon, x => Assert.Null(x.Root.Accent)));
    }

    [Fact]
    public void Generate_StressDependentChanges_OnlyWhereTheyCanApply()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var changes = l.Phonology.SyllableChanges;
            Assert.True(!changes.Contains(SoundChange.UnstressedReduction) || l.Phonology.Vowels.Contains(Vowel.ə));
            Assert.True(!changes.Contains(SoundChange.Syncope) || l.Phonology.SyllableRule.Template.Slots.Contains(Slot.Final));
            Assert.True(!changes.Contains(SoundChange.Apocope) || l.Phonology.SyllableRule.Template.Slots.Contains(Slot.Final));
            Assert.True(!changes.Contains(SoundChange.UnstressedShortening) || l.Phonology.Vowels.Any(v => v.Long));
            Assert.False(changes.Contains(SoundChange.UnstressedReduction) && changes.Contains(SoundChange.UnstressedRaising));
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Phonology.SyllableChanges.Contains(SoundChange.Syncope));
        Assert.Contains(GeneratedLanguages.Sample, l => l.Phonology.SyllableChanges.Contains(SoundChange.Apocope));
        Assert.Contains(GeneratedLanguages.Sample, l => l.Phonology.SyllableChanges.Contains(SoundChange.UnstressedReduction));
    }
}
