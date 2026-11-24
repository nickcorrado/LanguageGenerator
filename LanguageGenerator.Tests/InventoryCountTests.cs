namespace LanguageGenerator.Tests;

//how many vowels and consonants an inventory has, alone and beside the sounds a spec requires,
//forbids and its sound changes need
public class InventoryCountTests
{
    private static IEnumerable<Language> Generate(string json, int count = 5)
    {
        var spec = SpecFile.Parse(json);
        return Enumerable.Range(0, count).Select(seed => Generator.Generate(seed, spec));
    }

    [Theory]
    [InlineData("""{ "vowel-count": 5 }""", new[] { 5 })]
    [InlineData("""{ "vowel-count": [3, 7] }""", new[] { 3, 7 })]
    [InlineData("""{ "vowel-count": { "min": 5, "max": 7 } }""", new[] { 5, 6, 7 })]
    [InlineData("""{ "vowel-count": { "min": 19 } }""", new[] { 19, 20, 21 })]
    public void Parse_Count_AllowsThoseNumbers(string json, int[] counts) =>
        Assert.Equal(counts.Cast<object>(), SpecFile.Parse(json).Allowed(Settings.VowelCount)!);

    [Fact]
    public void Format_RangeOfCounts_ParsesBackTheSame()
    {
        var spec = SpecFile.Parse("""{ "consonant-count": { "min": 18, "max": 24 }, "vowel-count": [3, 5] }""");

        var again = SpecFile.Parse(SpecFile.Format(spec));

        Assert.Equal(spec.Allowed(Settings.ConsonantCount), again.Allowed(Settings.ConsonantCount));
        Assert.Equal(spec.Allowed(Settings.VowelCount), again.Allowed(Settings.VowelCount));
    }

    [Theory]
    [InlineData("""{ "vowel-count": 4.5 }""", "is a whole number")]
    [InlineData("""{ "vowel-count": { "min": 9, "max": 4 } }""", "allows nothing")]
    [InlineData("""{ "vowel-count": { "least": 4 } }""", "takes \"min\" and \"max\"")]
    public void Parse_BadCount_SaysWhy(string json, string message) =>
        Assert.Contains(message, Assert.Throws<SpecException>(() => SpecFile.Parse(json)).Message);

    [Fact]
    public void Problems_CountOutOfRange_SaysWhy() =>
        Assert.Contains(SpecFile.Parse("""{ "vowel-count": 22 }""").Problems(), p => p.Contains("isn't from 1 to 21"));

    [Fact]
    public void Generate_CountsFixed_HaveThatMany()
    {
        Assert.All(Generate("""{ "vowel-count": 5, "consonant-count": { "min": 14, "max": 16 } }""", 20), l =>
        {
            Assert.Equal(5, l.Phonology.Vowels.Count);
            Assert.InRange(l.Phonology.Consonants.Count, 14, 16);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_CountTheSeedAlreadyHas_KeepsItsInventory()
    {
        var free = Generator.Generate(3, LanguageSpec.Free);
        var spec = LanguageSpec.Free.Fix(Settings.ConsonantCount, free.Phonology.Consonants.Count);

        Assert.Equal(free.Phonology.Consonants, Generator.Generate(3, spec).Phonology.Consonants);
    }

    [Fact]
    public void Generate_CountsNoDrawGives_AreWrittenIn()
    {
        //nothing drawn has more than a few dozen consonants or a single vowel, so the consonants have
        //the commonest of the rest added, and the vowels are cut to the commonest
        Assert.All(Generate("""{ "consonant-count": 38, "vowel-count": 1 }"""), l =>
        {
            Assert.Equal(38, l.Phonology.Consonants.Count);
            Assert.Single(l.Phonology.Vowels);
        });
    }

    [Fact]
    public void Generate_FewSoundsWithSomeRequired_KeepsTheRequiredOnes()
    {
        Assert.All(Generate("""{ "consonant-count": 3, "consonants": { "require": ["θ", "q"] }, "vowel-count": 2, "vowels": { "forbid": ["a"] } }"""), l =>
        {
            Assert.Equal(3, l.Phonology.Consonants.Count);
            Assert.Contains(Consonant.θ, l.Phonology.Consonants);
            Assert.Contains(Consonant.q, l.Phonology.Consonants);
            Assert.Equal(2, l.Phonology.Vowels.Count);
            Assert.DoesNotContain(Vowel.a, l.Phonology.Vowels);
        });
    }

    [Fact]
    public void Generate_OneVowelAndAChangeOfTwoKindsNeedingSounds_HasTheOneThatMeetsIt()
    {
        //of reduction, needing ə, and raising, needing a mid vowel with i and u, only reduction fits
        //in one vowel
        Assert.All(Generate("""{ "vowel-count": 1, "unstressed-vowels": ["Reduction", "Raising"] }"""), l =>
        {
            Assert.Equal([Vowel.ə], l.Phonology.Vowels);
            Assert.Equal(UnstressedVowels.Reduction, Settings.UnstressedVowels.Get(l.Phonology.SyllableChanges));
        });
    }

    [Fact]
    public void Generate_CountOnlySomeOfWhichFit_HasOneThatDoes()
    {
        //raising needs three vowels, so of one or four, four
        Assert.All(Generate("""{ "vowel-count": [1, 4], "unstressed-vowels": "Raising" }"""), l =>
        {
            Assert.Equal(4, l.Phonology.Vowels.Count);
            Assert.Equal(UnstressedVowels.Raising, Settings.UnstressedVowels.Get(l.Phonology.SyllableChanges));
        });
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "vowel-count": 4, "vowels": ["a", "i", "u"] }""", "An exact list of vowels has to have as many as vowel-count allows" },
        { """{ "consonant-count": 1, "consonants": { "require": ["p", "t"] } }""", "consonant-count has to leave room" },
        { """{ "vowel-count": 20, "vowels": { "forbid": ["a", "e"] } }""", "can't be more than it allows" },
        { """{ "vowel-count": 2, "unstressed-vowels": "Raising" }""", "vowel-count has to leave room" },
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
