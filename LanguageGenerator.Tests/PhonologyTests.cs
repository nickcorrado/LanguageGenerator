namespace LanguageGenerator.Tests;

public class PhonologyTests
{
    private static readonly Phoneme[] Kabak = [Consonant.k, Vowel.a, Consonant.b, Vowel.a, Consonant.k];
    private static readonly Phoneme[] Kaba = [Consonant.k, Vowel.a, Consonant.b, Vowel.a];
    private static readonly Phoneme[] S = [Consonant.s];

    [Fact]
    public void Realize_IllegalFinalClusterWithEpenthesis_BreaksItWithAVowel()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis).Realize(TestData.Word(Kabak, S));

        Assert.Equal("kabak-əs", surface.Segmented(Orthography.Ipa));
        Assert.Equal("ka.ba.kəs", surface.Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_IllegalFinalClusterWithDeletion_DropsTheFirstConsonant()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Deletion).Realize(TestData.Word(Kabak, S));

        Assert.Equal("kaba-s", surface.Segmented(Orthography.Ipa));
        Assert.Equal("ka.bas", surface.Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_FinalConsonantInLanguageWithoutCodas_AddsAVowelAfterIt()
    {
        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis).Realize(TestData.Word(Kaba, S));

        Assert.Equal("kaba-sə", surface.Segmented(Orthography.Ipa));
        Assert.Equal("ka.ba.sə", surface.Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_FinalConsonantOutsideTheCodaRestriction_IsRepaired()
    {
        var phonology = new Phonology
        {
            Vowels = TestData.Vowels,
            Consonants = TestData.Consonants,
            SyllableRule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants, CodaRestriction.Nasals),
            SandhiRules = [],
            ClusterRepair = ClusterRepair.Epenthesis,
            EpentheticVowel = Vowel.ə,
        };

        //a nasal can close a syllable, but a stop needs a vowel after it, as with Japanese batto 'bat'
        Assert.Equal("kaba-n", phonology.Realize(TestData.Word(Kaba, [Consonant.n])).Segmented(Orthography.Ipa));
        Assert.Equal("ka.ba.tə", phonology.Realize(TestData.Word(Kaba, [Consonant.t])).Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_ConsonantLegalNowhere_IsDeletedEvenWithEpenthesis()
    {
        //VF has no onsets, so with only nasal codas a t can't go anywhere, and no inserted vowel helps
        var phonology = new Phonology
        {
            Vowels = TestData.Vowels,
            Consonants = TestData.Consonants,
            SyllableRule = new SyllableRule(Templates.Onsetless, TestData.Vowels, TestData.Consonants, CodaRestriction.Nasals),
            SandhiRules = [],
            ClusterRepair = ClusterRepair.Epenthesis,
            EpentheticVowel = Vowel.ə,
        };

        Assert.Equal("a.an", phonology.Realize(TestData.Word([Vowel.a, Consonant.t, Vowel.a, Consonant.n])).Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_AffixDeletedEntirely_ShowsAsEmpty()
    {
        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Deletion).Realize(TestData.Word(Kaba, S));

        Assert.Equal("kaba-∅", surface.Segmented(Orthography.Ipa));
        Assert.Equal("dog-PL", surface.Gloss);
    }

    [Fact]
    public void Realize_InitialConsonantInLanguageWithoutOnsets_AddsAVowelBeforeIt()
    {
        var surface = TestData.Phonology(Templates.Onsetless, ClusterRepair.Epenthesis).Realize(TestData.Word([Consonant.t, Vowel.a]));

        Assert.Equal("ət.a", surface.Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_LegalCodaAndOnset_NeedsNoRepair()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis).Realize(TestData.Word(Kabak, [Consonant.s, Vowel.a]));

        Assert.Equal("kabak-sa", surface.Segmented(Orthography.Ipa));
        Assert.Equal("ka.bak.sa", surface.Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_ConsonantsBetweenVowels_GiveTheOnsetAllItCanTake()
    {
        var surface = TestData.Phonology(SyllableTemplates.CLVF, ClusterRepair.Epenthesis).Realize(TestData.Word([Consonant.k, Vowel.a, Consonant.p, Consonant.l, Vowel.a]));

        Assert.Equal("ka.pla", surface.Syllabified(Orthography.Ipa));
    }

    [Fact]
    public void Realize_GeminateWithoutDegemination_IsRepaired()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis).Realize(TestData.Word([Consonant.k, Vowel.a, Consonant.t], [Consonant.t]));

        Assert.Equal("kat-ət", surface.Segmented(Orthography.Ipa));
    }

    [Fact]
    public void Realize_GeminateWithDegemination_MergesIntoTheFirstMorpheme()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis, new Degemination()).Realize(TestData.Word([Consonant.k, Vowel.a, Consonant.t], [Consonant.t]));

        Assert.Equal("kat-∅", surface.Segmented(Orthography.Ipa));
    }

    [Fact]
    public void Realize_NasalBeforeStopWithAssimilation_TakesTheStopsPlace()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis, new NasalAssimilation()).Realize(TestData.Word([Consonant.k, Vowel.a, Consonant.n], [Consonant.p, Vowel.a]));

        Assert.Equal("kam-pa", surface.Segmented(Orthography.Ipa));
    }

    [Fact]
    public void Realize_UnvoicedBetweenVowelsAcrossABoundary_IsVoiced()
    {
        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, new IntervocalicVoicing()).Realize(TestData.Word(Kaba, [Consonant.t, Vowel.a]));

        Assert.Equal("kaba-da", surface.Segmented(Orthography.Ipa));
    }

    [Fact]
    public void Realize_VoicingWithNoVoicedCounterpartInInventory_LeavesTheConsonant()
    {
        var noD = TestData.Consonants.Where(c => c != Consonant.d).ToList();

        var surface = TestData.Phonology(SyllableTemplates.CV, ClusterRepair.Epenthesis, noD, new IntervocalicVoicing()).Realize(TestData.Word(Kaba, [Consonant.t, Vowel.a]));

        Assert.Equal("kaba-ta", surface.Segmented(Orthography.Ipa));
    }

    [Fact]
    public void Realize_EpenthesisCreatingAVowelContext_DoesNotFeedVoicing()
    {
        var surface = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis, new IntervocalicVoicing()).Realize(TestData.Word([Consonant.k, Vowel.a, Consonant.b, Vowel.ə, Consonant.k], S));

        //sandhi runs before repair, so the k wasn't between vowels yet when voicing applied
        Assert.Equal("kabək-əs", surface.Segmented(Orthography.Ipa));
    }

    [Fact]
    public void ElideDuplicates_DoubledOnsetConsonant_Collapses()
    {
        var syllable = Generator.ElideDuplicates(new Syllable(new ConsonantCluster([Consonant.s, Consonant.s, Consonant.t]), Vowel.a, Consonant.k));

        Assert.Equal("stak", syllable.Spell(Orthography.Ipa));
    }

    [Fact]
    public void ElideDuplicates_DistinctOnsetConsonants_AreKept()
    {
        var syllable = Generator.ElideDuplicates(new Syllable(new ConsonantCluster([Consonant.ʃ, Consonant.s]), Vowel.a, null));

        Assert.Equal("ʃsa", syllable.Spell(Orthography.Ipa));
    }

    [Fact]
    public void Realize_GeneratedLanguages_ProducesOnlySyllablesTheirRulesAllow()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var rule = language.Phonology.SyllableRule;
            foreach (var word in GeneratedLanguages.AllWords(language))
            {
                var surface = language.Phonology.Realize(word);
                Assert.All(surface.Syllables, s =>
                {
                    Assert.True(rule.IsLegalOnset([.. s.Onset]), $"{surface.Syllabified(Orthography.Ipa)} has an illegal onset under {rule}");
                    Assert.True(rule.IsLegalCoda(s.Coda is null ? [] : [s.Coda]), $"{surface.Syllabified(Orthography.Ipa)} has an illegal coda under {rule}");
                });
            }
        }
    }

    [Fact]
    public void Realize_GeneratedLanguages_SegmentsAndSyllablesRejoinToTheSpelling()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var orthography = language.Orthography;
            foreach (var surface in GeneratedLanguages.AllWords(language).Select(language.Phonology.Realize))
            {
                Assert.Equal(surface.Spell(orthography), surface.Segmented(orthography).Replace("-", "").Replace("∅", ""));
                Assert.Equal(surface.Spell(orthography), surface.Syllabified(orthography).Replace(".", ""));
                Assert.Equal(surface.Phonemes, surface.Syllables.SelectMany(s => s.Phonemes));
            }
        }
    }

    [Fact]
    public void Realize_RootsInLanguagesWithoutSandhi_AreUnchanged()
    {
        //roots are built from legal syllables, so only morpheme boundaries can need repair
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Phonology.SandhiRules.Count == 0))
        {
            foreach (var lexeme in language.Lexicon)
            {
                var root = new Word([lexeme.Root]);
                Assert.Equal(lexeme.Root.Form, language.Phonology.Realize(root).Phonemes);
            }
        }
    }

    [Fact]
    public void Realize_GeneratedLanguages_ChangesSomeWordsUnderEachRepairStrategy()
    {
        var changedBy = GeneratedLanguages.Sample
            .SelectMany(l => GeneratedLanguages.AllWords(l).Select(w => (l.Phonology.ClusterRepair, Changed: !l.Phonology.Realize(w).Phonemes.SequenceEqual(w.Morphemes.SelectMany(m => m.Form)))))
            .Where(x => x.Changed)
            .Select(x => x.ClusterRepair)
            .Distinct();

        Assert.Equal([ClusterRepair.Epenthesis, ClusterRepair.Deletion], changedBy.Order());
    }
}
