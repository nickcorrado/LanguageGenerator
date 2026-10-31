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

    private static readonly List<Phoneme> FullInventory = [.. Vowel.All.Concat<Phoneme>(Consonant.All)];

    private static IEnumerable<OrthographicStyle> EveryStyle() =>
        from postalveolars in Enum.GetValues<PostalveolarSpelling>()
        from longVowels in Enum.GetValues<LongVowelSpelling>()
        from midVowels in Enum.GetValues<MidVowelSpelling>()
        from schwa in OrthographicStyle.SchwaOptions
        from nearOpenFront in OrthographicStyle.NearOpenFrontOptions
        from velarStop in OrthographicStyle.VelarStopOptions
        from palatalGlide in OrthographicStyle.PalatalGlideOptions
        select new OrthographicStyle(postalveolars, longVowels, midVowels, schwa, nearOpenFront, velarStop, palatalGlide);

    [Fact]
    public void Spell_EveryStyle_SpellsTheWholeInventory()
    {
        Assert.All(EveryStyle(), style => Assert.Equal(FullInventory.Count, style.Spell(FullInventory).Count));
    }

    [Fact]
    public void Spell_HacekMacronGrave_AppliesEachConventionToAllItsSounds()
    {
        var style = new OrthographicStyle(PostalveolarSpelling.Hacek, LongVowelSpelling.Macron, MidVowelSpelling.GraveOnOpen, "ë", "ä", "k", "y");

        var spellings = style.Spell(FullInventory);

        Assert.Equal(["š", "ž"], [spellings[Consonant.ʃ], spellings[Consonant.ʒ]]);
        Assert.Equal(["ī", "ō"], [spellings[Vowel.iː], spellings[Vowel.oː]]);
        Assert.Equal(["e", "è", "o", "ò"], [spellings[Vowel.e], spellings[Vowel.ɛ], spellings[Vowel.o], spellings[Vowel.ɔ]]);
        Assert.Equal(["ë", "ä", "k", "y", "r"], [spellings[Vowel.ə], spellings[Vowel.æ], spellings[Consonant.k], spellings[Consonant.j], spellings[Consonant.ɹ]]);
        Assert.True(Orthography.IsUnambiguous(spellings.Values));
    }

    [Theory]
    [InlineData(MidVowelSpelling.GraveOnOpen)]
    [InlineData(MidVowelSpelling.AcuteOnClose)]
    [InlineData(MidVowelSpelling.CircumflexOnOpen)]
    public void Spell_MidVowelWithoutItsPartner_NeedsNoMark(MidVowelSpelling midVowels)
    {
        var style = new OrthographicStyle(PostalveolarSpelling.Digraph, LongVowelSpelling.Macron, midVowels, "ë", "ä", "k", "y");

        var spellings = style.Spell(FullInventory.Where(p => p != Vowel.e && p != Vowel.o).ToList());

        Assert.Equal("e", spellings[Vowel.ɛ]);
        Assert.Equal("o", spellings[Vowel.ɔ]);
    }

    [Fact]
    public void Spell_ClashingConventions_AreAmbiguous()
    {
        //acute long vowels and acute close vowels both write oː and o as ó
        var style = new OrthographicStyle(PostalveolarSpelling.Hacek, LongVowelSpelling.Acute, MidVowelSpelling.AcuteOnClose, "ë", "ä", "k", "y");

        Assert.False(Orthography.IsUnambiguous(style.Spell(FullInventory).Values));
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_ApplyEachConventionConsistently()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Orthography.Style is not null), l =>
        {
            var spellings = l.Orthography.Spellings;
            //both postalveolars, when present, follow the same convention
            if (spellings.TryGetValue(Consonant.ʃ, out var sh) && spellings.TryGetValue(Consonant.ʒ, out var zh))
            {
                Assert.Contains((sh, zh), new[] { ("š", "ž"), ("sh", "zh"), ("x", "j") });
            }
            //and so do both long vowels
            if (spellings.TryGetValue(Vowel.iː, out var ii) && spellings.TryGetValue(Vowel.oː, out var oo))
            {
                Assert.Contains((ii, oo), new[] { ("ī", "ō"), ("í", "ó"), ("ii", "oo") });
            }
            Assert.Equal(l.Orthography.Style!.Spell([.. spellings.Keys]), spellings);
        });
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_UseEveryPostalveolarAndSeveralLongVowelStyles()
    {
        var styles = GeneratedLanguages.Sample.Select(l => l.Orthography.Style).OfType<OrthographicStyle>().ToList();

        Assert.Equal(Enum.GetValues<PostalveolarSpelling>().Length, styles.Select(s => s.Postalveolars).Distinct().Count());
        Assert.True(styles.Select(s => s.LongVowels).Distinct().Count() >= 2);
        Assert.Equal(Enum.GetValues<MidVowelSpelling>().Length, styles.Select(s => s.MidVowels).Distinct().Count());
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
