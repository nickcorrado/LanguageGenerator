namespace LanguageGenerator.Tests;

public class SpecTests
{
    //what a language is as far as its settings go, with a root or two to show the rest followed
    private static string Fingerprint(Language language) =>
        string.Join("; ", Settings.All.Select(s => $"{s.Name}={SpecFile.Format(LanguageSpec.Lock(language, [s]))}"))
        + $"; dog={string.Concat(language.Lexicon.Noun("dog").Root.Form.Select(p => p.IpaSymbol))}"
        + $"; see={string.Concat(language.Lexicon.Verb("see").Root.Form.Select(p => p.IpaSymbol))}";

    internal static void AssertMeets(Language language, LanguageSpec spec) =>
        Assert.All(spec.Fixed, kv => Assert.Contains(kv.Value, allowed => Same(kv.Key, allowed, kv.Key.Read(language))));

    private static bool Same(ISetting setting, object a, object b) => a is System.Collections.IEnumerable first && b is System.Collections.IEnumerable second
        ? first.Cast<object>().SequenceEqual(second.Cast<object>())
        : a.Equals(b);

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(123)]
    public void Generate_EverySettingLocked_GivesTheSameLanguageBack(int seed)
    {
        var language = Generator.Generate(seed, LanguageSpec.Free);

        Assert.Equal(Fingerprint(language), Fingerprint(Generator.Generate(seed, LanguageSpec.Lock(language, Settings.All))));
    }

    [Fact]
    public void Generate_ASettingFixedToSeveralValues_HasOneOfThem()
    {
        var spec = LanguageSpec.Free.Fix(Settings.ConstituentOrder, ConstituentWordOrder.VOS, ConstituentWordOrder.OSV);

        Assert.All(Enumerable.Range(0, 20), seed => AssertMeets(Generator.Generate(seed, spec), spec));
    }

    [Fact]
    public void Generate_FixingOneSetting_DrawsTheRestOfItsStageGivenIt()
    {
        //adpositions follow the verb almost without exception, so fixing OV makes postpositions
        //nearly certain, as redrawing the whole stage keeps them
        var spec = LanguageSpec.Free.Fix(Settings.ConstituentOrder, ConstituentWordOrder.SOV);
        var postpositional = Enumerable.Range(0, 100).Count(seed => Generator.Generate(seed, spec).WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Final);

        //472 of 486 OV languages are postpositional, so this fails about once in ten thousand
        Assert.True(postpositional >= Binomial.Least(100, 472 / 486.0, 1e-4));
    }

    [Fact]
    public void Generate_ManySettingsOfAStageFixed_MeetsThemAllEvenWhereNoDrawWould()
    {
        //rare enough together that no draw within the limit is likely to give them all
        var spec = LanguageSpec.Free
            .Fix(Settings.Fusion, Fusion.Isolating)
            .Fix(Settings.Affixation, Affixation.StronglyPrefixing)
            .Fix(Settings.CaseWithNumber, true)
            .Fix(Settings.TamWithAgreement, true)
            .Fix(Settings.TenseWithAspect, true)
            .Fix(Settings.ZeroSingular, false)
            .Fix(Settings.ZeroCoreCase, false)
            .Fix(Settings.ZeroPresent, false)
            .Fix(Settings.ZeroThirdPerson, ThirdPersonZero.All)
            .Fix(Settings.ConstituentOrder, ConstituentWordOrder.SOV);

        AssertMeets(Generator.Generate(5, spec), spec);
    }

    [Fact]
    public void Generate_FixedInventory_IsUsedInItsOrderEvenAgainstTheUsualImplications()
    {
        //z without s, and g without k, which a drawn inventory never has
        List<Consonant> consonants = [Consonant.z, Consonant.m, Consonant.g, Consonant.l];
        List<Vowel> vowels = [Vowel.o, Vowel.a];
        var spec = LanguageSpec.Free.Fix(Settings.Consonants, consonants).Fix(Settings.Vowels, vowels);

        Assert.All(Enumerable.Range(0, 10), seed =>
        {
            var language = Generator.Generate(seed, spec);
            Assert.Equal(consonants, language.Phonology.Consonants);
            Assert.Equal(vowels, language.Phonology.Vowels);
            Assert.All(GeneratedLanguages.AllWords(language).SelectMany(w => language.Phonology.Realize(w).Phonemes),
                p => Assert.True(p is Consonant c ? consonants.Contains(c) : vowels.Contains((Vowel)p), $"{p.IpaSymbol} isn't in the inventory."));
        });
    }

    [Fact]
    public void Generate_FirstSoundInTheInventory_IsTheCommonest()
    {
        /*
          with no codas, which a coda restriction can fill with one sound, and a steep consonant dropoff, since
          at the usual one each sound is nearly as likely as the one before, too close to tell
          apart in a small lexicon
         */
        var language = Generator.Generate(3, LanguageSpec.Free
            .Fix(Settings.Consonants, [Consonant.ʃ, Consonant.t, Consonant.k, Consonant.n, Consonant.m])
            .Fix(Settings.SyllableTemplate, SyllableTemplates.CV)
            .Fix(Settings.ConsonantDropoff, 0.5));
        var counts = language.Lexicon.SelectMany(l => l.Root.Form).OfType<Consonant>().GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(Consonant.ʃ, counts.MaxBy(kv => kv.Value).Key);
    }

    [Fact]
    public void Generate_FixedDropoff_IsTheLanguages()
    {
        Assert.Equal(new Dropoff(0.4, 0.2), Generator.Generate(1, LanguageSpec.Free.Fix(Settings.ConsonantDropoff, 0.4).Fix(Settings.VowelDropoff, 0.2)).Phonology.Dropoff);
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "syllable-template": "CV", "coda-restriction": "Nasals" }""", "coda restriction needs" },
        { """{ "syllable-template": ["CV", "CLV"], "coda-restriction": ["Nasals", "Sonorants"] }""", "coda restriction needs" },
        { """{ "constituent-order": "SVO", "oblique-position": "BetweenObjectAndVerb" }""", "between the object and the verb" },
        { """{ "consonants": [] }""", "at least one sound" },
        { """{ "vowels": ["a", "i", "a"] }""", "more than once" },
        { """{ "vowel-dropoff": 1.5 }""", "isn't between" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ImpossibleSpec_SaysWhyAndGenerateRefusesIt(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Contains(problem, Assert.Throws<SpecException>(() => Generator.Generate(1, spec)).Message);
    }

    [Theory]
    [InlineData("""{ "syllable-template": "CVF", "coda-restriction": "Nasals" }""")]
    [InlineData("""{ "coda-restriction": "Nasals" }""")]
    [InlineData("""{ "oblique-position": "BetweenObjectAndVerb" }""")]
    //affixation does nothing in an isolating language, but isn't wrong
    [InlineData("""{ "fusion": "Isolating", "affixation": "StronglySuffixing" }""")]
    public void Problems_PossibleSpec_HasNone(string json) =>
        Assert.Empty(SpecFile.Parse(json).Problems());

    [Theory]
    [InlineData("""{ "word-order": "SOV" }""", "isn't a setting")]
    [InlineData("""{ "constituent-order": "SXV" }""", "takes one of")]
    [InlineData("""{ "constituent-order": [] }""", "allows nothing")]
    [InlineData("""{ "consonants": ["p", "ts"] }""", "can't have 'ts'")]
    [InlineData("""{ "consonants": "p t k" }""", "array of IPA symbols")]
    [InlineData("""{ "consonant-dropoff": "steep" }""", "is a number")]
    [InlineData("""[ "SOV" ]""", "JSON object")]
    [InlineData("""{ "constituent-order": "SOV" """, "isn't valid JSON")]
    public void Parse_BadSpec_SaysWhy(string json, string message) =>
        Assert.Contains(message, Assert.Throws<SpecException>(() => SpecFile.Parse(json)).Message);

    [Fact]
    public void Parse_Spec_ReadsEachKindOfSetting()
    {
        var spec = SpecFile.Parse("""
            {
              // comments are allowed, for notes on why
              "constituent-order": "sov",
              "adpositions": ["Prepositions", "Postpositions"],
              "consonants": ["t", "k", "n", "s", "m"],
              "mirrors-adjectives": true,
              "consonant-dropoff": 0.3,
            }
            """);

        Assert.Equal<object>([ConstituentWordOrder.SOV], spec.Allowed(Settings.ConstituentOrder)!);
        Assert.Equal<object>([HeadDirectionality.Initial, HeadDirectionality.Final], spec.Allowed(Settings.AdpositionOrder)!);
        Assert.Equal<Consonant>([Consonant.t, Consonant.k, Consonant.n, Consonant.s, Consonant.m], (IReadOnlyList<Consonant>)Assert.Single(spec.Allowed(Settings.Consonants)!));
        Assert.Equal<object>([true], spec.Allowed(Settings.MirrorsAdjectives)!);
        Assert.Equal<object>([0.3], spec.Allowed(Settings.ConsonantDropoff)!);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(88)]
    public void Format_LockedSpec_ParsesBackToTheSameLanguage(int seed)
    {
        var language = Generator.Generate(seed, LanguageSpec.Free);
        var json = SpecFile.Format(LanguageSpec.Lock(language, Settings.All));

        Assert.Equal(Fingerprint(language), Fingerprint(Generator.Generate(seed, SpecFile.Parse(json))));
    }
}
