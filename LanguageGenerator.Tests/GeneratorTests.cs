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
