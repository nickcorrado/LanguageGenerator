namespace LanguageGenerator.Tests;

public class SyllableContactTests
{
    private static Consonant C(string symbol) => Consonant.All.Single(c => c.IpaSymbol == symbol);

    [Theory]
    [InlineData(SyllableContact.Any, "t", "l", true)]
    [InlineData(SyllableContact.Any, "t", "j", true)]
    [InlineData(SyllableContact.NoSteepRise, "s", "n", true)]
    [InlineData(SyllableContact.NoSteepRise, "n", "l", true)]
    [InlineData(SyllableContact.NoSteepRise, "l", "t", true)]
    [InlineData(SyllableContact.NoSteepRise, "t", "l", false)]
    [InlineData(SyllableContact.NoSteepRise, "s", "j", false)]
    [InlineData(SyllableContact.NoRise, "l", "t", true)]
    [InlineData(SyllableContact.NoRise, "n", "m", true)]
    [InlineData(SyllableContact.NoRise, "t", "k", true)]
    [InlineData(SyllableContact.NoRise, "n", "l", false)]
    [InlineData(SyllableContact.NoRise, "t", "n", false)]
    public void Allows_FollowsTheSonorityRise(SyllableContact contact, string coda, string onset, bool allowed) =>
        Assert.Equal(allowed, contact.Allows(C(coda), C(onset)));

    private static string Surface(SyllableContact contact, SoundChange change, params Phoneme[][] morphemes) => string.Concat(
        (TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { SyllableContact = contact, SyllableChanges = [change] })
            .Realize(TestData.Word(morphemes)).Phonemes.Select(p => p.IpaSymbol));

    private static readonly Phoneme[] La = [Consonant.l, Vowel.a];

    [Theory]
    [InlineData(SyllableContact.NoRise, "alla")]
    [InlineData(SyllableContact.NoSteepRise, "anla")]
    [InlineData(SyllableContact.Any, "anla")]
    public void CodaAssimilation_DoublesTheOnset_OnlyWhereTheContactIsBanned(SyllableContact contact, string expected) =>
        Assert.Equal(expected, Surface(contact, SoundChange.CodaAssimilation, [Vowel.a, Consonant.n], La));

    [Fact]
    public void CodaAssimilation_KeepsTheCoda_WhenTheOnsetCantCloseASyllable() =>
        Assert.Equal("atkʷa", Surface(SyllableContact.NoRise, SoundChange.CodaAssimilation, [Vowel.a, Consonant.t], [Consonant.kʷ, Vowel.a]));

    [Theory]
    [InlineData("n", "anda")]
    [InlineData("t", "atta")]
    [InlineData("k", "akta")]
    public void OnsetHardening_TurnsTheSonorantIntoAStopWithTheCodasVoicing(string coda, string expected) =>
        Assert.Equal(expected, Surface(SyllableContact.NoRise, SoundChange.OnsetHardening, [Vowel.a, C(coda)], La));

    [Fact]
    public void OnsetHardening_LeavesAnAllowedContact() =>
        Assert.Equal("asna", Surface(SyllableContact.NoSteepRise, SoundChange.OnsetHardening, [Vowel.a, Consonant.s], [Consonant.n, Vowel.a]));

    [Theory]
    [InlineData("k", "m", "aŋma")]
    [InlineData("p", "n", "amna")]
    public void CodaNasalization_TurnsTheObstruentIntoANasalAtItsPlace(string coda, string onset, string expected) =>
        Assert.Equal(expected, Surface(SyllableContact.NoRise, SoundChange.CodaNasalization, [Vowel.a, C(coda)], [C(onset), Vowel.a]));

    [Fact]
    public void CodaNasalization_LeavesAnAllowedContact() =>
        Assert.Equal("asma", Surface(SyllableContact.NoSteepRise, SoundChange.CodaNasalization, [Vowel.a, Consonant.s], [Consonant.m, Vowel.a]));

    [Theory]
    [InlineData(SyllableContact.NoSteepRise)]
    [InlineData(SyllableContact.NoRise)]
    public void GenerateForm_KeepsRootsToTheContact(SyllableContact contact)
    {
        var phonology = TestData.Phonology(SyllableTemplates.CVF, ClusterRepair.Epenthesis) with { SyllableContact = contact };
        var rand = new Random(1);
        for (int i = 0; i < 500; i++)
        {
            var syllables = phonology.Realize(new Word([new Root(Generator.GenerateForm(rand, phonology, 0.15, 2), "dog")])).Syllables;
            foreach (var (first, second) in syllables.Zip(syllables.Skip(1)))
            {
                if (first.Coda is { } coda && second.Onset.Count > 0)
                {
                    Assert.True(contact.Allows(coda, second.Onset[0]), $"{coda.IpaSymbol}.{second.Onset[0].IpaSymbol}");
                }
            }
        }
    }

    [Fact]
    public void SelectSyllableContact_WithoutCodas_IsAny()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CLV, TestData.Vowels, TestData.Consonants);

        Assert.All(Enumerable.Range(0, 200).Select(_ => Generator.SelectSyllableContact(rand, rule)), c => Assert.Equal(SyllableContact.Any, c));
    }

    [Fact]
    public void SelectSyllableContact_WithCodas_DrawsEach()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);
        var drawn = Enumerable.Range(0, 2000).Select(_ => Generator.SelectSyllableContact(rand, rule)).ToList();

        Assert.InRange(drawn.Count(c => c == SyllableContact.Any) / 2000.0, 0.36, 0.44);
        Assert.InRange(drawn.Count(c => c == SyllableContact.NoSteepRise) / 2000.0, 0.31, 0.39);
        Assert.InRange(drawn.Count(c => c == SyllableContact.NoRise) / 2000.0, 0.21, 0.29);
    }

    private static readonly SoundChange[] Repairs = [SoundChange.CodaAssimilation, SoundChange.OnsetHardening, SoundChange.CodaNasalization];

    [Fact]
    public void SelectSyllableChanges_WithAnyContact_DrawsNoRepair()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);

        Assert.DoesNotContain(Enumerable.Range(0, 500).SelectMany(_ => Generator.SelectSyllableChanges(rand, rule, TestData.Vowels, SyllableContact.Any)), Repairs.Contains);
    }

    [Fact]
    public void SelectSyllableChanges_WithARestrictedContact_DrawsAtMostOneRepair_AndEachComesUp()
    {
        var rand = new Random(1);
        var rule = new SyllableRule(SyllableTemplates.CVF, TestData.Vowels, TestData.Consonants);
        var draws = Enumerable.Range(0, 2000).Select(_ => Generator.SelectSyllableChanges(rand, rule, TestData.Vowels, SyllableContact.NoRise)).ToList();

        Assert.All(draws, changes => Assert.True(changes.Count(Repairs.Contains) <= 1));
        Assert.InRange(draws.Count(d => d.Contains(SoundChange.CodaAssimilation)) / 2000.0, 0.21, 0.29);
        Assert.InRange(draws.Count(d => d.Contains(SoundChange.OnsetHardening)) / 2000.0, 0.16, 0.24);
        Assert.InRange(draws.Count(d => d.Contains(SoundChange.CodaNasalization)) / 2000.0, 0.12, 0.18);
    }
}
