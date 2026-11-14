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
        //WALS 21A and 21B
        Assert.InRange(Share(p => p.Exponence.CaseWithNumber), 8 / 87.0 - 0.005, 8 / 87.0 + 0.005);
        Assert.InRange(Share(p => p.Exponence.TamWithAgreement), 24 / 156.0 - 0.005, 24 / 156.0 + 0.005);
    }

    [Fact]
    public void SelectMorphologicalProfile_ManyDraws_MatchTheWalsThirdPersonZeroCounts()
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectMorphologicalProfile(rand, ConstituentWordOrder.SOV).ZeroMarking.ThirdPerson).ToList();

        //WALS 103A, out of the 262 languages kept
        double Share(ThirdPersonZero value) => draws.Count(d => d == value) / 100000.0;
        Assert.InRange(Share(ThirdPersonZero.None), 181 / 262.0 - 0.006, 181 / 262.0 + 0.006);
        Assert.InRange(Share(ThirdPersonZero.Singular), 45 / 262.0 - 0.006, 45 / 262.0 + 0.006);
        Assert.InRange(Share(ThirdPersonZero.All), 36 / 262.0 - 0.006, 36 / 262.0 + 0.006);
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

    //WALS 20A crossed with 83A, folded as in the generator: the languages in each column, then the
    //concatenative, mixed and isolating counts in it
    [Theory]
    [InlineData(ConstituentWordOrder.SOV, 79, 71, 5, 3)]
    [InlineData(ConstituentWordOrder.SVO, 63, 43, 7, 13)]
    public void SelectMorphologicalProfile_ManyDraws_MatchTheWalsFusionCountsForTheirWordOrder(ConstituentWordOrder order, int languages, int concatenative, int mixed, int isolating)
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectMorphologicalProfile(rand, order).Fusion).ToList();

        double Share(Fusion fusion) => draws.Count(f => f == fusion) / 100000.0;
        Assert.InRange(Share(Fusion.Concatenative), concatenative / (double)languages - 0.006, concatenative / (double)languages + 0.006);
        Assert.InRange(Share(Fusion.Mixed), mixed / (double)languages - 0.006, mixed / (double)languages + 0.006);
        Assert.InRange(Share(Fusion.Isolating), isolating / (double)languages - 0.006, isolating / (double)languages + 0.006);
    }

    [Fact]
    public void Generate_IsolatingMorphology_IsCommonerInVerbObjectLanguages()
    {
        double IsolatingShare(bool objectFirst)
        {
            var group = GeneratedLanguages.Sample.Where(l => WordOrder.ObjectPrecedesVerb(l.WordOrder.ConstituentWordOrder) == objectFirst).ToList();
            return group.Count(l => l.MorphologicalProfile.Fusion == Fusion.Isolating) / (double)group.Count;
        }

        //WALS gives about 4% for OV and 21% for VO
        Assert.InRange(IsolatingShare(objectFirst: true), 0, 0.10);
        Assert.InRange(IsolatingShare(objectFirst: false), 0.12, 0.30);
    }

    [Fact]
    public void GenerateParadigm_AffixSlots_PrefixLessInObjectVerbLanguages()
    {
        //weighting each 26A band's prefix share by its count gives about 0.23 for OV and 0.46 for VO
        double PrefixShare(bool objectFirst)
        {
            var affixes = GeneratedLanguages.Sample.Where(l => WordOrder.ObjectPrecedesVerb(l.WordOrder.ConstituentWordOrder) == objectFirst)
                .SelectMany(l => l.Paradigms.All.SelectMany(p => p.Slots))
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
        Assert.Equal(expected, WordOrder.ObjectPrecedesVerb(order));
    }

    [Fact]
    public void SelectWordOrder_VerbPhrase_FollowsTheOrderOfObjectAndVerb()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.Equal(
            WordOrder.ObjectPrecedesVerb(l.WordOrder.ConstituentWordOrder) ? HeadDirectionality.Final : HeadDirectionality.Initial,
            l.WordOrder.VerbPhraseDirectionality));
    }

    //WALS's head-initial and total counts for each phrase in OV and in VO languages
    [Theory]
    [InlineData("Adpositional", 14, 486, 456, 498)]
    [InlineData("NounPhrase", 332, 548, 456, 570)]
    [InlineData("AdjectivePhrase", 72, 200, 107, 195)]
    public void SelectWordOrder_ManyDraws_MatchTheWalsCountsForEachPhrase(string phrase, int objectVerbInitial, int objectVerbLanguages, int verbObjectInitial, int verbObjectLanguages)
    {
        var rand = new Random(1);
        var draws = Enumerable.Range(0, 100000).Select(_ => Generator.SelectWordOrder(rand)).ToList();
        HeadDirectionality Of(WordOrder w) => phrase switch
        {
            "Adpositional" => w.AdpositionalPhraseDirectionality,
            "NounPhrase" => w.NounPhraseDirectionality,
            _ => w.AdjectivePhraseDirectionality,
        };
        double InitialShare(bool objectFirst)
        {
            var group = draws.Where(w => WordOrder.ObjectPrecedesVerb(w.ConstituentWordOrder) == objectFirst).ToList();
            return group.Count(w => Of(w) == HeadDirectionality.Initial) / (double)group.Count;
        }

        Assert.InRange(InitialShare(objectFirst: true), objectVerbInitial / (double)objectVerbLanguages - 0.01, objectVerbInitial / (double)objectVerbLanguages + 0.01);
        Assert.InRange(InitialShare(objectFirst: false), verbObjectInitial / (double)verbObjectLanguages - 0.01, verbObjectInitial / (double)verbObjectLanguages + 0.01);
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
            //with a noun for each configuration marked by one
            var relational = l.Configurations.Markers.Values.Select(m => m.Value).OfType<RelationalNoun>().Distinct().Count();
            Assert.Equal(11 + relational, l.Lexicon.Count(x => x.PartOfSpeech == PartOfSpeech.Noun));
            Assert.Equal(8, l.Lexicon.Count(x => x.PartOfSpeech == PartOfSpeech.Verb));
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
    public void GenerateVowels_ManyDraws_MatchWalsVowelQualityCounts()
    {
        var rand = new Random(1);
        var sizes = Enumerable.Range(0, 100000).Select(_ => Generator.GenerateVowels(rand).Count(v => !v.Long)).ToList();

        //WALS 2A: 93 small (2 to 4), 287 average (5 or 6) and 184 large (7 or more) of 564
        double Share(Func<int, bool> which) => sizes.Count(which) / 100000.0;
        Assert.InRange(Share(n => n <= 4), 93 / 564.0 - 0.005, 93 / 564.0 + 0.005);
        Assert.InRange(Share(n => n is 5 or 6), 287 / 564.0 - 0.005, 287 / 564.0 + 0.005);
        Assert.InRange(Share(n => n >= 7), 184 / 564.0 - 0.005, 184 / 564.0 + 0.005);
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

    [Fact]
    public void GenerateConsonants_ManyDraws_MatchWalsVoicingCounts()
    {
        var draws = ConsonantDraws.Value;
        double Share(Func<List<Consonant>, bool> which) => draws.Count(which) / (double)draws.Count;
        bool VoicedStops(List<Consonant> c) => c.Any(x => x.Manner is ConsonantManner.Plosive && x.Voiced);

        //WALS 4A: 189 + 158 of 567 contrast voicing in stops. Fricatives can fall short of their
        //38 + 158, since a language needs a voiceless fricative to voice, so only stops are checked
        Assert.InRange(Share(VoicedStops), 347 / 567.0 - 0.015, 347 / 567.0 + 0.015);
    }

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
    public void GenerateConsonants_ManyDraws_KeepCommonSoundsCommonAndRareOnesRare()
    {
        var draws = ConsonantDraws.Value;
        double Share(Consonant consonant) => draws.Count(c => c.Contains(consonant)) / (double)draws.Count;

        Assert.True(draws.Count(c => c.Contains(Consonant.m) || c.Contains(Consonant.p) || c.Contains(Consonant.b)) > 0.99 * draws.Count);
        Assert.True(Share(Consonant.t) > 0.95 && Share(Consonant.n) > 0.95);
        Assert.InRange(Share(Consonant.θ), 0.02, 0.08);
        //every consonant turns up in some language
        Assert.Equal(Consonant.All.Count, draws.SelectMany(c => c).Distinct().Count());
    }

    [Fact]
    public void GenerateConsonants_ManyDraws_HaveOneRhoticMostlyAndNeverɹBesideAnother()
    {
        var rhoticCounts = ConsonantDraws.Value.Select(c => c.Count(x => x.IsRhotic)).ToList();

        Assert.All(ConsonantDraws.Value.Where(c => c.Contains(Consonant.ɹ)), c => Assert.Equal(1, c.Count(x => x.IsRhotic)));
        Assert.True(rhoticCounts.Count(n => n == 2) < 0.1 * rhoticCounts.Count);
        Assert.True(rhoticCounts.Count(n => n == 1) > 0.6 * rhoticCounts.Count);
    }

    [Fact]
    public void GenerateConsonants_ManyDraws_AspirateAWholeSeriesAtOnce()
    {
        var aspirating = ConsonantDraws.Value.Where(c => c.Any(x => x.Aspirated)).ToList();

        Assert.InRange(aspirating.Count / (double)ConsonantDraws.Value.Count, 0.18, 0.32);
        //nearly every plain stop beside an aspirate has its own aspirate too
        Assert.True(aspirating.Count(c => c.Contains(Consonant.tʰ) && c.Contains(Consonant.kʰ)) > 0.85 * aspirating.Count);
    }

    [Fact]
    public void GenerateConsonants_ManyDraws_PutUnmarkedConsonantsFirstMostOfTheTime()
    {
        var firsts = ConsonantDraws.Value.Select(c => c[0]).ToList();

        Assert.True(firsts.Count(c => c == Consonant.t || c == Consonant.n || c == Consonant.k) > 0.8 * firsts.Count);
    }

    [Fact]
    public void GenerateVowels_ManyDraws_PutUnmarkedVowelsFirstMostOfTheTime()
    {
        var rand = new Random(1);
        var firsts = Enumerable.Range(0, 10000).Select(_ => Generator.GenerateVowels(rand)[0]).ToList();

        Assert.True(firsts.Count(v => v == Vowel.a || v == Vowel.i) > 0.8 * firsts.Count);
        Assert.DoesNotContain(firsts, v => v.Long);
    }
}
