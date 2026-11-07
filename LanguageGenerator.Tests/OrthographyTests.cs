namespace LanguageGenerator.Tests;

public class OrthographyTests
{
    [Theory]
    [InlineData(new[] { "e", "e" }, false)]
    [InlineData(new[] { "a", "e", "a" }, false)]
    [InlineData(new[] { "s", "sh" }, true)]
    [InlineData(new[] { "a", "ä", "e", "ë" }, true)]
    //spellings may run together, so long as each is its own
    [InlineData(new[] { "s", "h", "sh" }, true)]
    [InlineData(new[] { "k", "kh", "h" }, true)]
    public void AreDistinct_RejectsOnlyRepeatedSpellings(string[] spellings, bool distinct)
    {
        Assert.Equal(distinct, Orthography.AreDistinct(spellings));
    }

    [Fact]
    public void Ipa_SpellsEveryPhonemeWithItsIpaSymbol()
    {
        Assert.Equal("ʃiːk", Orthography.Ipa.Spell([Consonant.ʃ, Vowel.iː, Consonant.k]));
        Assert.True(Orthography.AreDistinct(Orthography.Ipa.Spellings.Values));
    }

    [Fact]
    public void Constructor_TwoPhonemesSpelledAlike_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Orthography(new Dictionary<Phoneme, string> { [Vowel.a] = "a", [Vowel.æ] = "a" }));
    }

    [Fact]
    public void Constructor_SpellingsThatRunTogether_AreAllowed()
    {
        var orthography = new Orthography(new Dictionary<Phoneme, string> { [Consonant.s] = "s", [Consonant.h] = "h", [Consonant.ʃ] = "sh" });

        Assert.Equal(orthography.Spell([Consonant.s, Consonant.h]), orthography.Spell([Consonant.ʃ]));
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
        from dentalFricatives in Enum.GetValues<DentalFricativeSpelling>()
        from velarNasal in OrthographicStyle.VelarNasalOptions
        from velarFricative in OrthographicStyle.VelarFricativeOptions
        from dentalAffricates in Enum.GetValues<DentalAffricateSpelling>()
        from aspiration in Enum.GetValues<AspirationSpelling>()
        from glottalStop in OrthographicStyle.GlottalStopOptions
        from palatalNasal in OrthographicStyle.PalatalNasalOptions
        select new OrthographicStyle(postalveolars, longVowels, midVowels, schwa, nearOpenFront, velarStop, palatalGlide, dentalFricatives, velarNasal, velarFricative,
            dentalAffricates, aspiration, glottalStop, palatalNasal);

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
        Assert.Equal(["ë", "ä", "k", "y"], [spellings[Vowel.ə], spellings[Vowel.æ], spellings[Consonant.k], spellings[Consonant.j]]);
        Assert.Equal(["ph", "th", "kh", "kw", "gw"], [spellings[Consonant.pʰ], spellings[Consonant.tʰ], spellings[Consonant.kʰ], spellings[Consonant.kʷ], spellings[Consonant.gʷ]]);
        Assert.Equal(["ts", "dz", "'", "ñ"], [spellings[Consonant.ʦ], spellings[Consonant.ʣ], spellings[Consonant.ʔ], spellings[Consonant.ɲ]]);
        Assert.True(Orthography.AreDistinct(spellings.Values));
    }

    [Theory]
    [InlineData("r", "r")]
    [InlineData("ɾ", "r")]
    [InlineData("ɹ", "r")]
    [InlineData("r ɾ", "rr r")]
    public void Spell_Rhotics_AreROnTheirOwnAndRrForATrillBesideATap(string rhotics, string expected)
    {
        var style = new OrthographicStyle(PostalveolarSpelling.Digraph, LongVowelSpelling.Macron, MidVowelSpelling.GraveOnOpen, "ë", "ä", "k", "y");
        var present = rhotics.Split(' ').Select(ipa => Consonant.All.Single(c => c.IpaSymbol == ipa)).ToList();

        var spellings = style.Spell([Vowel.a, Consonant.t, .. present]);

        Assert.Equal(expected, string.Join(" ", present.Select(r => spellings[r])));
    }

    [Theory]
    [InlineData(AspirationSpelling.H, "p ph t th c ch")]
    [InlineData(AspirationSpelling.Apostrophe, "p p' t t' c c'")]
    [InlineData(AspirationSpelling.VoicedLetters, "b p d t g c")]
    public void Spell_Aspirates_FollowTheConventionAndVoicedLettersOnlyWorkWithoutVoicedStops(AspirationSpelling aspiration, string expected)
    {
        var style = new OrthographicStyle(PostalveolarSpelling.Digraph, LongVowelSpelling.Macron, MidVowelSpelling.GraveOnOpen, "ë", "ä", "c", "y", Aspiration: aspiration);
        List<Phoneme> stops = [Consonant.p, Consonant.pʰ, Consonant.t, Consonant.tʰ, Consonant.k, Consonant.kʰ];

        var plain = style.Spell([Vowel.a, .. stops]);
        var withVoiced = style.Spell([Vowel.a, .. stops, Consonant.b, Consonant.d, Consonant.g]);

        Assert.Equal(expected, string.Join(" ", stops.Select(s => plain[s])));
        Assert.True(Orthography.AreDistinct(plain.Values));
        Assert.Equal(aspiration != AspirationSpelling.VoicedLetters, Orthography.AreDistinct(withVoiced.Values));
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
    public void Spell_ClashingConventions_SpellTwoPhonemesAlike()
    {
        //acute long vowels and acute close vowels both write oː and o as ó
        var style = new OrthographicStyle(PostalveolarSpelling.Hacek, LongVowelSpelling.Acute, MidVowelSpelling.AcuteOnClose, "ë", "ä", "k", "y");

        Assert.False(Orthography.AreDistinct(style.Spell(FullInventory).Values));
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
    public void SelectOrthography_GeneratedLanguages_SpellTheirWholeInventoryDistinctly()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Assert.True(Orthography.AreDistinct(l.Orthography.Spellings.Values));
            Assert.All(l.Phonology.Vowels.Concat<Phoneme>(l.Phonology.Consonants), p => Assert.Contains(p, l.Orthography.Spellings.Keys));
        });
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_RarelySpellTwoDifferentWordsTheSame()
    {
        //spellings can run together, so s + h can meet sh, but it should take a rare coincidence of words
        bool HasHomographs(Language language) =>
            GeneratedLanguages.AllWords(language).Select(language.Phonology.Realize)
                .GroupBy(surface => surface.Spell(language.Orthography))
                .Any(forms => forms.Select(f => string.Join(" ", f.Phonemes.Select(p => p.IpaSymbol))).Distinct().Count() > 1);

        var count = GeneratedLanguages.Sample.Count(HasHomographs);

        Assert.True(count <= GeneratedLanguages.Sample.Count / 50, $"{count} of {GeneratedLanguages.Sample.Count} languages have homographs");
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_UseDigraphsBesideTheirOwnLetters()
    {
        Assert.Contains(GeneratedLanguages.Sample, l =>
            l.Orthography.Spellings.Values.Contains("sh") && l.Orthography.Spellings.ContainsKey(Consonant.s) && l.Orthography.Spellings.ContainsKey(Consonant.h));
    }

    [Fact]
    public void SelectOrthography_GeneratedLanguages_SpellTheSameSoundDifferently()
    {
        var shSpellings = GeneratedLanguages.Sample.Where(l => l.Orthography.Spellings.ContainsKey(Consonant.ʃ))
            .Select(l => l.Orthography.Spell(Consonant.ʃ)).Distinct().ToList();

        Assert.True(shSpellings.Count >= 3, $"ʃ was only ever spelled {string.Join(", ", shSpellings)}");
    }
}
