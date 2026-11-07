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
        TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { Stress = stress, SyllableChanges = syllableChanges };

    private static readonly Phoneme[] Pata = [Consonant.p, Vowel.a, Consonant.t, Vowel.a];

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

    [Fact]
    public void SelectStress_ManyDraws_MatchWalsCounts()
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectStress(rand)).ToList();

        //WALS 14A: of 502 languages, 92 initial, 110 penultimate, 51 final, and 220 without a fixed place
        double Share(Func<Stress, bool> which) => draws.Count(which) / 100000.0;
        Assert.InRange(Share(s => s == Stress.Initial), 92 / 502.0 - 0.005, 92 / 502.0 + 0.005);
        Assert.InRange(Share(s => s == Stress.Penultimate), 110 / 502.0 - 0.005, 110 / 502.0 + 0.005);
        Assert.InRange(Share(s => s == Stress.Ultimate), 51 / 502.0 - 0.005, 51 / 502.0 + 0.005);
        Assert.InRange(Share(s => s.IsWeightSensitive), 220 / 502.0 - 0.005, 220 / 502.0 + 0.005);
        Assert.Equal(Enum.GetValues<Stress>().Length, draws.Distinct().Count());
    }

    [Fact]
    public void Generate_GeneratedLanguages_StressOneSyllableOfEveryWord()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.All(l.Lexicon.Select(x => l.Phonology.Realize(new Word([x.Root]))), form =>
        {
            Assert.InRange(form.StressedSyllable, 0, form.Syllables.Count - 1);
            Assert.Equal(form.Syllables.Count > 1 ? 1 : 0, form.Transcribed().Count(c => c == 'ˈ'));
        }));
    }

    [Fact]
    public void Generate_StressDependentChanges_OnlyWhereTheyCanApply()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var changes = l.Phonology.SyllableChanges;
            Assert.True(!changes.Contains(SoundChange.UnstressedReduction) || l.Phonology.Vowels.Contains(Vowel.ə));
            Assert.True(!changes.Contains(SoundChange.Syncope) || l.Phonology.SyllableRule.Template.Slots.Contains(Slot.Final));
            Assert.True(!changes.Contains(SoundChange.UnstressedShortening) || l.Phonology.Vowels.Any(v => v.Long));
            Assert.False(changes.Contains(SoundChange.UnstressedReduction) && changes.Contains(SoundChange.UnstressedRaising));
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Phonology.SyllableChanges.Contains(SoundChange.Syncope));
        Assert.Contains(GeneratedLanguages.Sample, l => l.Phonology.SyllableChanges.Contains(SoundChange.UnstressedReduction));
    }
}
