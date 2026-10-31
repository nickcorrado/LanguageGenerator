namespace LanguageGenerator.Tests;

public class GeneratorTests
{
    //everything a run prints, so two languages with the same fingerprint are indistinguishable
    private static string Fingerprint(Language l) => string.Join("|",
        l.Phonology.SyllableRule, string.Join(",", l.Phonology.SandhiRules), l.Phonology.ClusterRepair,
        string.Join(",", GeneratedLanguages.AllWords(l).Select(w => $"{l.Phonology.Realize(w).Segmented}={w.Gloss}")),
        l.Morphology, l.WordOrder.ConstituentWordOrder, l.WordOrder.HeadDirectionality, l.WordOrder.VerbPhraseDirectionality,
        l.WordOrder.NounPhraseDirectionality, l.WordOrder.AdjectivePhraseDirectionality, l.WordOrder.AdpositionalPhraseDirectionality,
        l.HasArticles, l.MorphosyntacticAlignment, string.Join(",", l.Cases.Select(c => c.Gloss)),
        string.Join(",", l.RoleMarkers.Select(kv => $"{kv.Key}={kv.Value}")));

    [Fact]
    public void Generate_SameSeed_GivesTheSameLanguageEvenAfterGeneratingOthers()
    {
        var first = Fingerprint(Generator.Generate(new Random(7)));
        foreach (var seed in Enumerable.Range(100, 20))
        {
            Generator.Generate(new Random(seed));
        }

        Assert.Equal(first, Fingerprint(Generator.Generate(new Random(7))));
    }

    [Fact]
    public void Generate_DifferentSeeds_GiveDifferentLanguages()
    {
        var fingerprints = GeneratedLanguages.Sample.Take(50).Select(Fingerprint);

        Assert.Equal(50, fingerprints.Distinct().Count());
    }

    [Fact]
    public void SelectWordOrder_ManyDraws_MatchTheTypologicalFrequencies()
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectWordOrder(rand).ConstituentWordOrder).ToList();

        double Share(ConstituentWordOrder order) => draws.Count(d => d == order) / 100000.0;
        Assert.InRange(Share(ConstituentWordOrder.SOV), 0.44, 0.46);
        Assert.InRange(Share(ConstituentWordOrder.SVO), 0.39, 0.41);
        Assert.InRange(Share(ConstituentWordOrder.VSO), 0.095, 0.105);
        Assert.InRange(Share(ConstituentWordOrder.VOS), 0.027, 0.033);
        Assert.InRange(Share(ConstituentWordOrder.OSV), 0.008, 0.012);
        Assert.InRange(Share(ConstituentWordOrder.OVS), 0.008, 0.012);
    }

    [Fact]
    public void SelectMorphologicalProfile_ManyDraws_MatchTheWalsCounts()
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectMorphologicalProfile(rand, ConstituentWordOrder.SOV)).ToList();

        double Share(Func<MorphologicalProfile, bool> which) => draws.Count(which) / 100000.0;
        //WALS 20A, out of the 162 languages kept
        Assert.InRange(Share(p => p.Fusion == Fusion.Concatenative), 132 / 162.0 - 0.005, 132 / 162.0 + 0.005);
        Assert.InRange(Share(p => p.Fusion == Fusion.Mixed), 13 / 162.0 - 0.005, 13 / 162.0 + 0.005);
        Assert.InRange(Share(p => p.Fusion == Fusion.Isolating), 17 / 162.0 - 0.005, 17 / 162.0 + 0.005);
        //WALS 21A and 21B
        Assert.InRange(Share(p => p.Exponence.CaseWithNumber), 8 / 87.0 - 0.005, 8 / 87.0 + 0.005);
        Assert.InRange(Share(p => p.Exponence.TamWithAgreement), 24 / 156.0 - 0.005, 24 / 156.0 + 0.005);
    }

    //WALS 26A crossed with 83A: the affixing languages in each column, then the strongly suffixing
    //and strongly prefixing counts in it
    [Theory]
    [InlineData(ConstituentWordOrder.SOV, 417, 269, 6)]
    [InlineData(ConstituentWordOrder.OSV, 417, 269, 6)]
    [InlineData(ConstituentWordOrder.SVO, 326, 93, 51)]
    [InlineData(ConstituentWordOrder.VSO, 326, 93, 51)]
    public void SelectMorphologicalProfile_ManyDraws_MatchTheWalsCountsForTheirWordOrder(ConstituentWordOrder order, int languages, int stronglySuffixing, int stronglyPrefixing)
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectMorphologicalProfile(rand, order).Affixation).ToList();

        Assert.InRange(draws.Count(a => a == Affixation.StronglySuffixing) / 100000.0, stronglySuffixing / (double)languages - 0.006, stronglySuffixing / (double)languages + 0.006);
        Assert.InRange(draws.Count(a => a == Affixation.StronglyPrefixing) / 100000.0, stronglyPrefixing / (double)languages - 0.006, stronglyPrefixing / (double)languages + 0.006);
    }

    [Fact]
    public void GenerateParadigm_AffixSlots_PrefixLessInObjectVerbLanguages()
    {
        //weighting each 26A band's prefix share by its count gives about 0.23 for OV and 0.46 for VO
        double PrefixShare(bool objectFirst)
        {
            var affixes = GeneratedLanguages.Sample.Where(l => Generator.ObjectPrecedesVerb(l.WordOrder.ConstituentWordOrder) == objectFirst)
                .SelectMany(l => l.Paradigms.Values.SelectMany(p => p.Slots))
                .Where(s => s.Position is ExponentPosition.Prefix or ExponentPosition.Suffix).ToList();
            return affixes.Count(s => s.Position == ExponentPosition.Prefix) / (double)affixes.Count;
        }

        Assert.InRange(PrefixShare(objectFirst: true), 0.15, 0.31);
        Assert.InRange(PrefixShare(objectFirst: false), 0.38, 0.54);
    }

    [Theory]
    [InlineData(ConstituentWordOrder.SOV, true)]
    [InlineData(ConstituentWordOrder.OSV, true)]
    [InlineData(ConstituentWordOrder.OVS, true)]
    [InlineData(ConstituentWordOrder.SVO, false)]
    [InlineData(ConstituentWordOrder.VSO, false)]
    [InlineData(ConstituentWordOrder.VOS, false)]
    public void ObjectPrecedesVerb_ReadsTheConstituentOrder(ConstituentWordOrder order, bool expected)
    {
        Assert.Equal(expected, Generator.ObjectPrecedesVerb(order));
    }

    [Fact]
    public void SelectWordOrder_HeadDirectionality_VariesIndependentlyOfConstituentOrder()
    {
        var sov = GeneratedLanguages.Sample.Where(l => l.WordOrder.ConstituentWordOrder == ConstituentWordOrder.SOV);

        Assert.Equal(2, sov.Select(l => l.WordOrder.AdpositionalPhraseDirectionality).Distinct().Count());
        Assert.InRange(GeneratedLanguages.Sample.Count(l => l.WordOrder.HeadDirectionality == HeadDirectionality.Initial) / 300.0, 0.4, 0.6);
    }

    [Fact]
    public void Generate_Articles_AppearInAboutSeventyPercentOfLanguages()
    {
        Assert.InRange(GeneratedLanguages.Sample.Count(l => l.HasArticles) / 300.0, 0.62, 0.78);
    }

    [Fact]
    public void Generate_AnyLanguage_HasTheWholeLexiconAndAMarkerForEveryRole()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Assert.Equal(11, l.Lexicon.Count(x => x.PartOfSpeech == PartOfSpeech.Noun));
            Assert.Equal(8, l.Lexicon.Count(x => x.PartOfSpeech == PartOfSpeech.Verb));
            Assert.Equal(Enum.GetValues<CaseUse>().Length, l.RoleMarkers.Count);
        });
    }

    [Theory]
    [InlineData(true, true, "ə")]
    [InlineData(false, true, "i")]
    [InlineData(false, false, "a")]
    public void SelectEpentheticVowel_PrefersSchwaThenI(bool hasSchwa, bool hasI, string expected)
    {
        var vowels = PhonemeManager.Vowels.Where(v => (hasSchwa || v != Vowel.ə) && (hasI || v != Vowel.i)).ToList();

        Assert.Equal(expected, Generator.SelectEpentheticVowel(vowels).IpaSymbol);
    }
}
