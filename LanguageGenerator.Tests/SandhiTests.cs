namespace LanguageGenerator.Tests;

//what happens to sounds where words meet: changes across words, final consonant loss and liaison, and clitics
public class SandhiTests
{
    private static Phonology Phonology(Sandhi sandhi, params SoundChange[] changes) =>
        TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis, changes) with { Sandhi = sandhi };

    private static Word Noun(string meaning, params Phoneme[] form) => new([new Root(form, meaning, null)]);

    private static Word Clitic(Lean leans, params Phoneme[] form) => Word.Marker(new Particle(form, [Definiteness.Definite]), leans);

    private static Rendering Said(Phonology phonology, params Word[] words) => new([], phonology.RealizePhrase(words), "x", Orthography.Ipa);

    private static readonly Word Pan = Noun("bread", Consonant.p, Vowel.a, Consonant.n);
    private static readonly Word Ka = Noun("house", Consonant.k, Vowel.a);
    private static readonly Word Pa = Noun("father", Consonant.p, Vowel.a);
    private static readonly Word Ta = Noun("tea", Consonant.t, Vowel.a);
    private static readonly Word Pat = Noun("cat", Consonant.p, Vowel.a, Consonant.t);
    private static readonly Word Pas = Noun("step", Consonant.p, Vowel.a, Consonant.s);
    private static readonly Word A = Noun("water", Vowel.a);

    [Fact]
    public void ChangeAcrossWords_AppliesWhereWordsMeet_ButIsNotSpelled()
    {
        var phonology = Phonology(TestData.NoSandhi with { AcrossWords = [SoundChange.NasalAssimilation] }, SoundChange.NasalAssimilation);

        var sentence = Said(phonology, Pan, Ka);

        Assert.Equal("[paŋ ka]", sentence.Transcribed());
        Assert.Equal("pan ka", sentence.Text);
    }

    [Fact]
    public void ChangeOnlyWithinWords_LeavesWhereWordsMeetAlone()
    {
        var phonology = Phonology(TestData.NoSandhi, SoundChange.NasalAssimilation);

        Assert.Equal("[pan ka]", Said(phonology, Pan, Ka).Transcribed());
    }

    [Fact]
    public void LenitionAcrossWords_WeakensAStopStartingAWordAfterAVowel()
    {
        var phonology = Phonology(TestData.NoSandhi with { AcrossWords = [SoundChange.Lenition] }, SoundChange.Lenition);

        var sentence = Said(phonology, Pa, Ta);

        Assert.Equal("[pa θa]", sentence.Transcribed());
        Assert.Equal("pa ta", sentence.Text);
    }

    [Fact]
    public void FinalConsonantLoss_SilencesAWordsLastObstruentAlone_ButSpellsIt()
    {
        var phonology = Phonology(TestData.NoSandhi with { FinalConsonantLoss = true });

        var cat = phonology.Realize(Pat);

        Assert.Equal("pa", cat.Transcribed());
        Assert.Equal("pat", cat.Spell(Orthography.Ipa));
    }

    [Fact]
    public void FinalConsonantLoss_SaysTheConsonantAgainBeforeAVowel()
    {
        var phonology = Phonology(TestData.NoSandhi with { FinalConsonantLoss = true });

        Assert.Equal("[pat‿a]", Said(phonology, Pat, A).Transcribed());
        Assert.Equal("[pa ka]", Said(phonology, Pat, Ka).Transcribed());
        Assert.Equal("[ka pa]", Said(phonology, Ka, Pat).Transcribed());
    }

    [Fact]
    public void Liaison_FeedsChangesAcrossWords()
    {
        //as French les amis is said lezami
        var phonology = Phonology(TestData.NoSandhi with { AcrossWords = [SoundChange.IntervocalicVoicing], FinalConsonantLoss = true }, SoundChange.IntervocalicVoicing);

        var sentence = Said(phonology, Pas, A);

        Assert.Equal("[paz‿a]", sentence.Transcribed());
        Assert.Equal("pas a", sentence.Text);
    }

    [Fact]
    public void Clitic_IsSaidAsOneWordWithItsHost()
    {
        //as French le ami is l'ami
        var phonology = Phonology(TestData.NoSandhi, SoundChange.VowelElision);

        var sentence = Said(phonology, Clitic(Lean.Forward, Consonant.l, Vowel.a), Noun("friend", Vowel.a, Consonant.m, Vowel.i));

        var group = Assert.Single(sentence.Groups);
        Assert.Equal("l=ami", group.Segmented(Orthography.Ipa));
        Assert.Equal("DEF=friend", group.Gloss);
        Assert.Equal(2, sentence.Words.Count);
        Assert.Equal("l ami", sentence.Text);
    }

    [Theory]
    //as Latin virúmque moves the stress, and Spanish dígamelo doesn't
    [InlineData(CliticStress.Group, "[paˈta.ka]")]
    [InlineData(CliticStress.Host, "[ˈpa.ta.ka]")]
    public void Clitic_IsStressedWithItsHostAsTheLanguageHasIt(CliticStress stress, string expected)
    {
        var phonology = Phonology(TestData.NoSandhi with { CliticStress = stress });

        var sentence = Said(phonology, Noun("duck", Consonant.p, Vowel.a, Consonant.t, Vowel.a), Clitic(Lean.Back, Consonant.k, Vowel.a));

        Assert.Equal(expected, sentence.Transcribed());
    }

    [Fact]
    public void Clitics_InARow_LeanOnTheSameHost()
    {
        var phonology = Phonology(TestData.NoSandhi);

        var sentence = Said(phonology, Clitic(Lean.Forward, Consonant.m, Vowel.e), Clitic(Lean.Forward, Consonant.l, Vowel.o), Ka, Clitic(Lean.Back, Consonant.n, Vowel.i));

        Assert.Equal("me=lo=ka=ni", Assert.Single(sentence.Groups).Segmented(Orthography.Ipa));
    }

    [Fact]
    public void Clitic_WithNothingToLeanOn_StandsAloneUnstressed()
    {
        var phonology = Phonology(TestData.NoSandhi);

        var sentence = Said(phonology, Ka, Clitic(Lean.Forward, Consonant.l, Vowel.a, Consonant.m, Vowel.a));

        Assert.Equal(2, sentence.Groups.Count);
        Assert.Null(sentence.Groups[1].StressedSyllable);
    }

    [Fact]
    public void RealizePhrase_WordWithoutARootThatDoesntLean_Throws()
    {
        var phonology = Phonology(TestData.NoSandhi);

        Assert.Throws<ArgumentException>(() => phonology.RealizePhrase([Ka, new Word([new Particle([Consonant.l, Vowel.a], [Definiteness.Definite])])]));
    }

    [Fact]
    public void RealizePhrase_WordWithARootThatLeans_Throws()
    {
        var phonology = Phonology(TestData.NoSandhi);

        Assert.Throws<ArgumentException>(() => phonology.RealizePhrase([Ka with { Leans = Lean.Back }]));
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public void Interlinear_HasAPhoneticLineOnlyWhenAskedFor(bool phonetic, int lines)
    {
        var sentence = Said(Phonology(TestData.NoSandhi), Pa, Ka);

        var written = sentence.Interlinear(phonetic).Split(Environment.NewLine);

        Assert.Equal(lines, written.Length);
        Assert.Equal(phonetic, written.Contains("[pa ka]"));
    }
}
