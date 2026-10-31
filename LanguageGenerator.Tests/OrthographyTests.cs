namespace LanguageGenerator.Tests;

public class OrthographyTests
{
    [Theory]
    [InlineData(new[] { "a", "e", "ae" }, false)]
    [InlineData(new[] { "s", "h", "sh" }, false)]
    [InlineData(new[] { "i", "ii" }, false)]
    [InlineData(new[] { "e", "e" }, false)]
    //a chain of dangling suffixes: "a" + "bc" and "ab" + "c" both spell "abc"
    [InlineData(new[] { "a", "ab", "bc", "c" }, false)]
    [InlineData(new[] { "s", "sh" }, true)]
    [InlineData(new[] { "a", "ä", "e", "ë" }, true)]
    [InlineData(new[] { "k", "kh", "h" }, false)]
    public void IsUnambiguous_DetectsSpellingsThatSplitMoreThanOneWay(string[] spellings, bool unambiguous)
    {
        Assert.Equal(unambiguous, Orthography.IsUnambiguous(spellings));
    }

    [Fact]
    public void Ipa_SpellsEveryPhonemeWithItsIpaSymbol()
    {
        Assert.Equal("ʃiːk", Orthography.Ipa.Spell([Consonant.ʃ, Vowel.iː, Consonant.k]));
        Assert.True(Orthography.IsUnambiguous(Orthography.Ipa.Spellings.Values));
    }

    [Fact]
    public void Constructor_AmbiguousSpellings_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Orthography(new Dictionary<Phoneme, string> { [Vowel.a] = "a", [Vowel.ɛ] = "e", [Vowel.æ] = "ae" }));
    }

    [Fact]
    public void Spell_PhonemeOutsideTheOrthography_Throws()
    {
        var orthography = new Orthography(new Dictionary<Phoneme, string> { [Vowel.a] = "a" });

        Assert.Throws<ArgumentException>(() => orthography.Spell(Vowel.i));
    }

    [Fact]
    public void Options_CoverEveryPhonemeInTheInventory()
    {
        Assert.All(PhonemeManager.Vowels.Concat<Phoneme>(PhonemeManager.Consonants), p => Assert.Contains(p, Orthography.Options.Keys));
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_SpellTheirWholeInventoryUnambiguously()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Assert.True(Orthography.IsUnambiguous(l.Orthography.Spellings.Values));
            Assert.All(l.Phonology.Vowels.Concat<Phoneme>(l.Phonology.Consonants), p => Assert.Contains(p, l.Orthography.Spellings.Keys));
        });
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_NeverSpellTwoDifferentWordsTheSame()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var spelled = new Dictionary<string, string>();
            foreach (var surface in GeneratedLanguages.AllWords(language).Select(language.Phonology.Realize))
            {
                var ipa = string.Join(" ", surface.Phonemes.Select(p => p.IpaSymbol));
                Assert.Equal(ipa, spelled.TryAdd(surface.Spell(language.Orthography), ipa) ? ipa : spelled[surface.Spell(language.Orthography)]);
            }
        }
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_SpellTheSameSoundDifferently()
    {
        var shSpellings = GeneratedLanguages.Sample.Where(l => l.Orthography.Spellings.ContainsKey(Consonant.ʃ))
            .Select(l => l.Orthography.Spell(Consonant.ʃ)).Distinct().ToList();

        Assert.True(shSpellings.Count >= 3, $"ʃ was only ever spelled {string.Join(", ", shSpellings)}");
    }
}
