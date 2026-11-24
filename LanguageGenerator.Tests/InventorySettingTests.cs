namespace LanguageGenerator.Tests;

//inventories given as the sounds they require and forbid, with the rest drawn
public class InventorySettingTests
{
    private static IEnumerable<Language> Generate(string json, int count = 20)
    {
        var spec = SpecFile.Parse(json);
        return Enumerable.Range(0, count).Select(seed => Generator.Generate(seed, spec));
    }

    [Fact]
    public void Parse_RequiredAndForbiddenSounds_FixEachSound()
    {
        var spec = SpecFile.Parse("""{ "consonants": { "require": ["θ", "q"], "forbid": ["s"] }, "vowels": { "forbid": ["ə"] } }""");

        Assert.Equal([true], spec.Allowed(Settings.ConsonantSounds[Consonant.All.ToList().IndexOf(Consonant.θ)]));
        Assert.Equal([true], spec.Allowed(Settings.ConsonantSounds[Consonant.All.ToList().IndexOf(Consonant.q)]));
        Assert.Equal([false], spec.Allowed(Settings.ConsonantSounds[Consonant.All.ToList().IndexOf(Consonant.s)]));
        Assert.Equal([false], spec.Allowed(Settings.VowelSounds[Vowel.All.ToList().IndexOf(Vowel.ə)]));
        Assert.Equal(4, spec.Fixed.Count);
    }

    [Fact]
    public void Format_RequiredAndForbiddenSounds_ParsesBackTheSame()
    {
        var spec = SpecFile.Parse("""{ "consonants": { "require": ["θ"], "forbid": ["s", "h"] }, "stress": "Initial" }""");

        var again = SpecFile.Parse(SpecFile.Format(spec));

        Assert.Equal(spec.Fixed.Count, again.Fixed.Count);
        Assert.All(spec.Fixed, kv => Assert.Equal(kv.Value, again.Allowed(kv.Key)));
    }

    [Fact]
    public void Generate_SoundsRequiredAndForbidden_HasThemAndNotThose()
    {
        //no draw lacks both t and k, so the consonants give up on drawing and have what's missing
        //written in, which takes the whole limit of draws: a few seeds are enough
        Assert.All(Generate("""{ "consonants": { "require": ["q", "x"], "forbid": ["t", "k"] }, "vowels": { "require": ["ɨ"], "forbid": ["e"] } }""", 5), l =>
        {
            Assert.Contains(Consonant.q, l.Phonology.Consonants);
            Assert.Contains(Consonant.x, l.Phonology.Consonants);
            Assert.DoesNotContain(Consonant.t, l.Phonology.Consonants);
            Assert.DoesNotContain(Consonant.k, l.Phonology.Consonants);
            Assert.Contains(Vowel.ɨ, l.Phonology.Vowels);
            Assert.DoesNotContain(Vowel.e, l.Phonology.Vowels);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_SoundTheSeedAlreadyHas_KeepsItsInventory()
    {
        var spec = SpecFile.Parse("""{ "consonants": { "require": ["ʃ"] } }""");
        var having = Enumerable.Range(0, 30).Select(seed => (Seed: seed, Free: Generator.Generate(seed, LanguageSpec.Free)))
            .Where(p => p.Free.Phonology.Consonants.Contains(Consonant.ʃ)).ToList();

        Assert.NotEmpty(having);
        Assert.All(having, p => Assert.Equal(p.Free.Phonology.Consonants, Generator.Generate(p.Seed, spec).Phonology.Consonants));
    }

    [Fact]
    public void Generate_SoundsNoDrawGivesTogether_AreWrittenIn()
    {
        //θ comes only with s, and every drawn vowel system has a, so these are written in
        Assert.All(Generate("""{ "consonants": { "require": ["θ"], "forbid": ["s"] }, "vowels": { "forbid": ["a"] } }""", 5), l =>
        {
            Assert.Contains(Consonant.θ, l.Phonology.Consonants);
            Assert.DoesNotContain(Consonant.s, l.Phonology.Consonants);
            Assert.DoesNotContain(Vowel.a, l.Phonology.Vowels);
            Assert.NotEmpty(l.Phonology.Vowels);
        });
    }

    [Fact]
    public void Generate_EveryConsonantForbiddenButOne_HasJustThatOne()
    {
        var forbidden = Consonant.All.Where(c => c != Consonant.q).Select(c => $"\"{c.IpaSymbol}\"");

        Assert.All(Generate($$"""{ "consonants": { "forbid": [{{string.Join(", ", forbidden)}}] } }""", 3),
            l => Assert.Equal([Consonant.q], l.Phonology.Consonants));
    }

    [Fact]
    public void Write_RequiredSound_GoesWhereItsRankPutsIt()
    {
        IReadOnlyList<Consonant> drawn = [Consonant.t, Consonant.n, Consonant.k, Consonant.ʔ];

        var written = Settings.ConsonantSounds[Consonant.All.ToList().IndexOf(Consonant.s)].Write(drawn, [true]);

        Assert.Equal([Consonant.t, Consonant.n, Consonant.k, Consonant.s, Consonant.ʔ], written);
    }

    [Fact]
    public void Generate_ChangeNeedingASoundAndSoundsForbidden_DrawsAnotherThatAllowsIt()
    {
        //glide formation needs j or w, and j is forbidden, so there's w
        Assert.All(Generate("""{ "glide-formation": true, "consonants": { "forbid": ["j"] } }"""), l =>
        {
            Assert.Contains(Consonant.w, l.Phonology.Consonants);
            Assert.Contains(SoundChange.GlideFormation, l.Phonology.SoundChanges);
        });
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "vowels": { "forbid": ["a", "e", "i", "æ", "ɛ", "ə", "ɨ", "ɔ", "o", "iː", "oː", "aː", "eː", "u", "uː", "ai", "au", "ei", "ou", "oi", "ui"] } }""", "not every vowel can be forbidden" },
        { """{ "unstressed-vowels": "Reduction", "vowels": { "forbid": ["ə"] } }""", "Unstressed reduction needs ə" },
        { """{ "glide-formation": true, "consonants": { "forbid": ["j", "w"] } }""", "glide-formation needs j or w" },
        { """{ "diphthong-formation": true, "vowels": ["a", "i", "u"] }""", "diphthong-formation needs a diphthong" },
        { """{ "unstressed-monophthongization": true, "vowels": { "forbid": ["ai", "au", "ei", "ou", "oi", "ui"] } }""", "unstressed-monophthongization needs a diphthong" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }

    [Fact]
    public void Problems_ExactListAgainstARequiredSound_SaysWhy()
    {
        //an exact list and required sounds can only come together from code, since a spec file
        //gives an inventory one way or the other
        var spec = SpecFile.Parse("""{ "vowels": ["a", "i", "u"] }""").Fix(Settings.VowelSounds[Vowel.All.ToList().IndexOf(Vowel.e)], true);

        Assert.Contains(spec.Problems(), p => p.Contains("An exact list of vowels can't lack a sound the spec requires"));
    }

    [Theory]
    [InlineData("""{ "consonants": { "allow": ["p"] } }""", "takes \"require\" and \"forbid\"")]
    [InlineData("""{ "consonants": { "require": ["p"], "forbid": ["p"] } }""", "can't both require and forbid p")]
    [InlineData("""{ "consonants": { "require": "p" } }""", "is an array of IPA symbols")]
    [InlineData("""{ "consonants": { "require": ["ts"] } }""", "can't have 'ts'")]
    [InlineData("""{ "consonants:p": true }""", "isn't a setting")]
    public void Parse_BadSounds_SaysWhy(string json, string message) =>
        Assert.Contains(message, Assert.Throws<SpecException>(() => SpecFile.Parse(json)).Message);
}
