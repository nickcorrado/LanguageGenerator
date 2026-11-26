namespace LanguageGenerator.Tests;

//the settings of sound changes and syllable contact, and the rules that draw the syllables and sounds
//a change needs
public class SoundChangeSettingTests
{
    private static IEnumerable<Language> Generate(LanguageSpec spec, int count = 30) => Enumerable.Range(0, count).Select(seed => Generator.Generate(seed, spec));

    [Fact]
    public void Generate_FreeLanguages_HaveTheirChangesInTheOrderTheyApply()
    {
        //a change written in goes where the order says, so the order has to be the one drawn
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Assert.Equal(SoundChange.SoundChangeOrder.Where(l.Phonology.SoundChanges.Contains), l.Phonology.SoundChanges);
            Assert.Equal(SoundChange.SyllableChangeOrder.Where(l.Phonology.SyllableChanges.Contains), l.Phonology.SyllableChanges);
        });
    }

    [Fact]
    public void Generate_CodaChangesFixed_DrawSyllablesWithCodasThatCanBeAnything()
    {
        var spec = LanguageSpec.Free.Fix(Settings.FinalDevoicing, true).Fix(Settings.CodaWeakening, true);

        Assert.All(Generate(spec), l =>
        {
            Assert.Contains(Slot.Final, l.Phonology.SyllableRule.Template.Slots);
            Assert.Equal(CodaRestriction.Any, l.Phonology.SyllableRule.Codas);
            Assert.Contains(SoundChange.FinalDevoicing, l.Phonology.SoundChanges);
            Assert.Contains(SoundChange.CodaWeakening, l.Phonology.SyllableChanges);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_ChangesNeedingSoundsFixed_DrawInventoriesThatHaveThem()
    {
        var spec = LanguageSpec.Free
            .Fix(Settings.UnstressedVowels, UnstressedVowels.Reduction)
            .Fix(Settings.CompensatoryLengthening, true)
            .Fix(Settings.VowelHarmony, VowelHarmony.TongueRoot)
            .Fix(Settings.HiatusResolution, HiatusResolution.GlottalInsertion);

        Assert.All(Generate(spec), l =>
        {
            Assert.Contains(Vowel.ə, l.Phonology.Vowels);
            Assert.Contains(l.Phonology.Vowels, v => v.Long);
            Assert.True(SoundChange.TongueRootHarmony.CanApplyTo(l.Phonology.Vowels));
            Assert.True(l.Phonology.Consonants.Contains(Consonant.ʔ) || l.Phonology.Consonants.Contains(Consonant.h));
            Assert.Equal(UnstressedVowels.Reduction, Settings.UnstressedVowels.Get(l.Phonology.SyllableChanges));
            Assert.Contains(SoundChange.GlottalInsertion, l.Phonology.SoundChanges);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_EitherOfTwoChangesNeedingDifferentSounds_HasOneItsVowelsAllow()
    {
        var spec = LanguageSpec.Free.Fix(Settings.UnstressedVowels, UnstressedVowels.Reduction, UnstressedVowels.Raising);

        Assert.All(Generate(spec), l =>
        {
            Assert.NotEqual(UnstressedVowels.None, Settings.UnstressedVowels.Get(l.Phonology.SyllableChanges));
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_ContactRepairFixed_DrawsARestrictionAndCodas()
    {
        var spec = LanguageSpec.Free.Fix(Settings.ContactRepair, ContactRepair.OnsetHardening);

        Assert.All(Generate(spec), l =>
        {
            Assert.NotEqual(SyllableContact.Any, l.Phonology.SyllableContact);
            Assert.Contains(Slot.Final, l.Phonology.SyllableRule.Template.Slots);
            Assert.Contains(SoundChange.OnsetHardening, l.Phonology.SyllableChanges);
        });
    }

    [Fact]
    public void Generate_ChangeAcrossWordsFixed_HasItWithinWordsToo()
    {
        var spec = LanguageSpec.Free.Fix(Settings.LenitionAcrossWords, true);

        Assert.All(Generate(spec), l =>
        {
            Assert.Contains(SoundChange.Lenition, l.Phonology.SoundChanges);
            Assert.Contains(SoundChange.Lenition, l.Phonology.Sandhi.AcrossWords);
        });
    }

    [Fact]
    public void Generate_FinalConsonantLossFixed_DrawsCodasThatCanBeAnything()
    {
        var spec = LanguageSpec.Free.Fix(Settings.FinalConsonantLoss, true);

        Assert.All(Generate(spec), l =>
        {
            Assert.Contains(Slot.Final, l.Phonology.SyllableRule.Template.Slots);
            Assert.Equal(CodaRestriction.Any, l.Phonology.SyllableRule.Codas);
            Assert.True(l.Phonology.Sandhi.FinalConsonantLoss);
        });
    }

    [Fact]
    public void Generate_FreeLanguages_MakeOnlyTheirOwnChangesAcrossWords()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.All(l.Phonology.Sandhi.AcrossWords, c => Assert.Contains(c, l.Phonology.SoundChanges)));
    }

    [Fact]
    public void Generate_ChangeFixedOff_KeepsTheSeedsLanguageWhereItHadNone()
    {
        var spec = LanguageSpec.Free.Fix(Settings.Lenition, false);
        var pairs = Enumerable.Range(0, 30).Select(seed => (Free: Generator.Generate(seed, LanguageSpec.Free), Fixed: Generator.Generate(seed, spec))).ToList();

        Assert.All(pairs, p => Assert.DoesNotContain(SoundChange.Lenition, p.Fixed.Phonology.SoundChanges));
        Assert.Contains(pairs, p => !p.Free.Phonology.SoundChanges.Contains(SoundChange.Lenition));
        Assert.All(pairs.Where(p => !p.Free.Phonology.SoundChanges.Contains(SoundChange.Lenition)),
            p => Assert.Equal(p.Free.Phonology.SoundChanges, p.Fixed.Phonology.SoundChanges));
    }

    [Fact]
    public void Generate_ExactVowelsWithoutSchwa_NeverReduce()
    {
        //the rules narrow the free setting to what these vowels allow, which is what a draw gives anyway
        var spec = SpecFile.Parse("""{ "vowels": ["a", "e", "i", "o", "u"] }""");

        Assert.All(Generate(spec, 20), l => Assert.NotEqual(UnstressedVowels.Reduction, Settings.UnstressedVowels.Get(l.Phonology.SyllableChanges)));
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "final-devoicing": true, "syllable-template": "CV" }""", "final-devoicing needs a syllable template with a coda" },
        { """{ "coda-weakening": true, "coda-restriction": "Nasals" }""", "coda-weakening needs codas that can be any consonant" },
        { """{ "syllable-contact": "NoRise", "syllable-template": ["CV", "CLV"] }""", "A syllable contact restriction needs" },
        { """{ "contact-repair": "CodaNasalization", "syllable-contact": "Any" }""", "A contact repair needs" },
        { """{ "unstressed-vowels": "Reduction", "vowels": ["a", "i", "u"] }""", "Unstressed reduction needs ə" },
        { """{ "compensatory-lengthening": true, "vowels": ["a", "i", "u"] }""", "compensatory-lengthening needs long vowels" },
        { """{ "vowel-harmony": "TongueRoot", "vowels": ["a", "e", "i", "o", "u"] }""", "Vowel harmony needs a pair" },
        { """{ "glide-formation": true, "consonants": ["p", "t", "k", "m", "n", "s"] }""", "glide-formation needs j or w" },
        { """{ "hiatus-resolution": "GlottalInsertion", "consonants": ["p", "t", "k", "m", "n", "s"] }""", "glottal insertion ʔ or h" },
        { """{ "lenition-across-words": true, "lenition": false }""", "lenition-across-words needs the change within words too" },
        { """{ "final-consonant-loss": true, "syllable-template": "CV" }""", "final-consonant-loss needs a syllable template with a coda" },
        { """{ "final-consonant-loss": true, "coda-restriction": "Nasals" }""", "final-consonant-loss needs codas that can be any consonant" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }
}
