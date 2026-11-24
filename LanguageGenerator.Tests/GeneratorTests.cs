namespace LanguageGenerator.Tests;

public class GeneratorTests
{
    //everything a run prints, so two languages with the same fingerprint are indistinguishable
    private static string Fingerprint(Language l) => string.Join("|",
        l.Phonology.SyllableRule, string.Join(",", l.Phonology.SoundChanges), l.Phonology.ClusterRepair,
        string.Join(",", l.Orthography.Spellings.Select(s => $"{s.Key.IpaSymbol}={s.Value}")),
        string.Join(",", GeneratedLanguages.AllWords(l).Select(w => $"{l.Phonology.Realize(w).Segmented(l.Orthography)}={w.Gloss}")),
        l.MorphologicalType, l.WordOrder.ConstituentWordOrder, l.WordOrder.HeadDirectionality, l.WordOrder.VerbPhraseDirectionality,
        l.WordOrder.NounPhraseDirectionality, l.WordOrder.AdjectivePhraseDirectionality, l.WordOrder.AdpositionalPhraseDirectionality,
        ArticleForms(l, l.Articles.Definite), l.Articles.Definite?.Position,
        ArticleForms(l, l.Articles.Indefinite), l.Articles.Indefinite?.Position, l.MorphosyntacticAlignment, string.Join(",", l.Cases.Select(c => c.Gloss)),
        string.Join(",", l.RoleMarkers.Select(kv => $"{kv.Key}={kv.Value}")));

    private static string ArticleForms(Language l, Article? article) =>
        article is null ? "" : string.Join(",", article.Forms.Select(f => $"{f.Key}={f.Value.Spell(l.Orthography)}"));

    [Fact]
    public void Generate_SameSeed_GivesTheSameLanguageEvenAfterGeneratingOthers()
    {
        var first = Fingerprint(Generator.Generate(7, LanguageSpec.Free));
        foreach (var seed in Enumerable.Range(100, 20))
        {
            Generator.Generate(seed, LanguageSpec.Free);
        }

        Assert.Equal(first, Fingerprint(Generator.Generate(7, LanguageSpec.Free)));
    }

    [Fact]
    public void Generate_DifferentSeeds_GiveDifferentLanguages()
    {
        var fingerprints = GeneratedLanguages.Sample.Take(50).Select(Fingerprint);

        Assert.Equal(50, fingerprints.Distinct().Count());
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
        Assert.Equal(expected, WordOrder.ObjectPrecedesVerb(order));
    }

    [Fact]
    public void SelectWordOrder_VerbPhrase_FollowsTheOrderOfObjectAndVerb()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.Equal(
            WordOrder.ObjectPrecedesVerb(l.WordOrder.ConstituentWordOrder) ? HeadDirectionality.Final : HeadDirectionality.Initial,
            l.WordOrder.VerbPhraseDirectionality));
    }

    [Theory]
    [InlineData(ConstituentWordOrder.SVO, HeadDirectionality.Initial, HeadDirectionality.Initial, HeadDirectionality.Initial, HeadDirectionality.Initial)]
    [InlineData(ConstituentWordOrder.SOV, HeadDirectionality.Final, HeadDirectionality.Initial, HeadDirectionality.Final, HeadDirectionality.Final)]
    //two phrases each way: the verb phrase decides
    [InlineData(ConstituentWordOrder.SOV, HeadDirectionality.Initial, HeadDirectionality.Initial, HeadDirectionality.Final, HeadDirectionality.Final)]
    [InlineData(ConstituentWordOrder.SVO, HeadDirectionality.Final, HeadDirectionality.Final, HeadDirectionality.Initial, HeadDirectionality.Initial)]
    public void HeadDirectionality_IsWhicheverWayMostPhrasesGo(ConstituentWordOrder order, HeadDirectionality noun, HeadDirectionality adjective, HeadDirectionality adposition, HeadDirectionality expected)
    {
        var wordOrder = new WordOrder { ConstituentWordOrder = order, NounPhraseDirectionality = noun, AdjectivePhraseDirectionality = adjective, AdpositionalPhraseDirectionality = adposition, ObliquePosition = ObliquePosition.AfterVerbAndObject, MirrorsAdjectives = true };

        Assert.Equal(expected, wordOrder.HeadDirectionality);
    }

    [Fact]
    public void Generate_AnyLanguage_HasTheWholeLexiconAndAMarkerForEveryRole()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            //with a noun for each configuration marked by one, and 'thing' and 'place' for indefinites
            //made from nouns, 'person' being among the 11 already
            var relational = l.Configurations.Markers.Values.Select(m => m.Value).OfType<RelationalNoun>().Select(n => n.Meaning).Distinct().ToList();
            var kinds = Indefinites.Kinds.Select(Indefinites.NounFor).Except(["person"]).Except(relational).Count();
            Assert.Equal(11 + relational.Count + kinds, l.Lexicon.Count(x => x.PartOfSpeech == PartOfSpeech.Noun));
            //with the verb an aspect's auxiliary is, where it's 'have', 'finish' or 'sit'
            var basic = l.Lexicon.Where(x => x.PartOfSpeech == PartOfSpeech.Verb).Select(x => x.Root.Meaning).ToList();
            var auxiliaries = ((AspectKind[])[l.Aspects.Perfect.Kind, l.Aspects.Progressive.Kind]).Select(Language.AuxiliaryMeaning).OfType<string>().Distinct().ToList();
            Assert.Equal(8 + auxiliaries.Count, basic.Count);
            Assert.All(auxiliaries, a => Assert.Contains(a, basic));
            //every role but the similative, which has a marker of its own
            Assert.Equal(Enum.GetValues<Role>().Length - 1, l.RoleMarkers.Count);
        });
    }

    [Fact]
    public void Generate_AnyLanguage_HasRootsOfOneToMaxSyllables()
    {
        //every syllable has exactly one vowel, so a root has as many syllables as vowels
        Assert.All(GeneratedLanguages.Sample.SelectMany(l => l.Lexicon), x =>
            Assert.InRange(x.Root.Form.Count(p => p is Vowel), 1, Generator.MaxRootSyllables));
    }

    [Fact]
    public void GenerateLexicon_GeneratedLanguages_NeverPutTwoVowelsTogetherInsideARoot()
    {
        Assert.All(GeneratedLanguages.Sample.SelectMany(l => l.Lexicon), lexeme =>
            Assert.DoesNotContain(lexeme.Root.Form.Zip(lexeme.Root.Form.Skip(1)), pair => pair.First is Vowel && pair.Second is Vowel));
    }

    [Theory]
    [InlineData("", "ə")]
    [InlineData("ə", "ɨ")]
    [InlineData("ə ɨ", "i")]
    [InlineData("ə ɨ i", "u")]
    [InlineData("ə ɨ i u", "a")]
    public void SelectEpentheticVowel_PrefersSchwaThenCloseVowelsThenTheCommonest(string missing, string expected)
    {
        var absent = missing.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var vowels = Vowel.All.Where(v => !absent.Contains(v.IpaSymbol)).ToList();

        Assert.Equal(expected, Generator.SelectEpentheticVowel(vowels).IpaSymbol);
    }

    [Fact]
    public void GenerateVowels_EveryLanguage_HasTheShortVowelOfEachLongOne()
    {
        Assert.All(GeneratedLanguages.Sample.Select(l => l.Phonology.Vowels), vowels =>
            Assert.All(vowels.Where(v => v.Long), v => Assert.Contains(vowels, s => s.Lengthened == v)));
    }

    [Fact]
    public void GenerateVowels_EveryLanguage_HasAAndAContrastingVowel()
    {
        //dispersion: every system has an open vowel and something to set against it
        Assert.All(GeneratedLanguages.Sample.Select(l => l.Phonology.Vowels), vowels =>
        {
            Assert.Contains(Vowel.a, vowels);
            Assert.True(vowels.Contains(Vowel.i) || vowels.Contains(Vowel.ə));
        });
    }

    private static readonly Lazy<List<List<Consonant>>> ConsonantDraws = new(() =>
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, 20000).Select(_ => Generator.GenerateConsonants(rand))];
    });

    [Theory]
    [InlineData("z", "s")]
    [InlineData("v", "f")]
    [InlineData("ʒ", "ʃ")]
    [InlineData("ð", "θ")]
    [InlineData("ʤ", "ʧ")]
    [InlineData("g", "k")]
    [InlineData("ŋ", "k")]
    [InlineData("ʃ", "s")]
    [InlineData("θ", "s")]
    [InlineData("pʰ", "p")]
    [InlineData("tʰ", "t")]
    [InlineData("kʰ", "k")]
    [InlineData("kʷ", "k")]
    [InlineData("gʷ", "g")]
    [InlineData("gʷ", "kʷ")]
    [InlineData("ʣ", "ʦ")]
    [InlineData("q", "k")]
    [InlineData("ʎ", "l")]
    public void GenerateConsonants_ManyDraws_NeverHaveAMarkedSoundWithoutTheOneItImplies(string marked, string implied)
    {
        Assert.All(ConsonantDraws.Value, c =>
        {
            if (c.Any(x => x.IpaSymbol == marked))
            {
                Assert.Contains(c, x => x.IpaSymbol == implied);
            }
        });
    }

    [Fact]
    public void GenerateConsonants_ManyDraws_UseEveryConsonant()
    {
        //an inventory spec fixes its sounds outright, so only a free draw shows each can come up
        Assert.Equal(Consonant.All.Count, ConsonantDraws.Value.SelectMany(c => c).Distinct().Count());
    }

    [Fact]
    public void GenerateConsonants_ɹ_NeverStandsBesideAnotherRhotic()
    {
        Assert.Contains(ConsonantDraws.Value, c => c.Contains(Consonant.ɹ));
        Assert.All(ConsonantDraws.Value.Where(c => c.Contains(Consonant.ɹ)), c => Assert.Equal(1, c.Count(x => x.IsRhotic)));
    }

    [Fact]
    public void GenerateConsonants_ManyDraws_PutUnmarkedConsonantsFirstMostOfTheTime()
    {
        var firsts = ConsonantDraws.Value.Select(c => c[0]).ToList();

        //at least 80% of inventories rank t, n or k first, failing once in a million at exactly 80%;
        //all but a few in ten thousand do now
        Assert.True(firsts.Count(c => c == Consonant.t || c == Consonant.n || c == Consonant.k) >= Binomial.Least(firsts.Count, 0.8, 1e-6));
    }

    [Fact]
    public void GenerateVowels_ManyDraws_PutUnmarkedVowelsFirstMostOfTheTime()
    {
        var rand = new Random(1);
        var firsts = Enumerable.Range(0, 10000).Select(_ => Generator.GenerateVowels(rand)[0]).ToList();

        //at least 80% of inventories rank a or i first, failing once in a million at exactly 80%; all do now
        Assert.True(firsts.Count(v => v == Vowel.a || v == Vowel.i) >= Binomial.Least(firsts.Count, 0.8, 1e-6));
        Assert.DoesNotContain(firsts, v => v.Long);
    }
}
