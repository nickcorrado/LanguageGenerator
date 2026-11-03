namespace LanguageGenerator.Tests;

//these generate their own languages rather than use GeneratedLanguages.Sample, since coining
//words adds to a lexicon, and the sample is shared
public class LexiconTests
{
    private static Language Fresh(int seed) => Generator.Generate(new Random(seed));

    [Fact]
    public void Coin_SameMeaning_GivesTheSameRootWhateverWasCoinedFirst()
    {
        foreach (var seed in Enumerable.Range(0, 50))
        {
            var direct = Fresh(seed).Lexicon.Noun("dragon");
            var afterOthers = Fresh(seed);
            foreach (var meaning in new[] { "mountain", "river", "sword", "king" })
            {
                afterOthers.Lexicon.Noun(meaning);
            }

            Assert.Equal(direct.Root.Form, afterOthers.Lexicon.Noun("dragon").Root.Form);
        }
    }

    private static string Surface(Language l, Root root) => string.Concat(l.Phonology.Realize(new Word([root])).Phonemes.Select(p => p.IpaSymbol));

    //nouns don't sound like pronouns, adjectives like numerals, or verbs like copulas, whether
    //they're basic words or coined later
    [Fact]
    public void Coin_AnyRoot_DoesntSoundLikeAClosedClassWordInItsSlot()
    {
        foreach (var seed in Enumerable.Range(0, 100))
        {
            var language = Fresh(seed);
            foreach (var i in Enumerable.Range(0, 30))
            {
                language.Lexicon.Noun($"thing{i}");
                language.Lexicon.Adjective($"quality{i}");
                language.Lexicon.Verb($"doing{i}");
            }
            var numerals = language.Numerals.Words.Values.Append(language.Numerals.Many).OfType<Lexeme>();
            var copulas = new[] { language.Predication.Copula, language.Predication.LocationalCopula }.OfType<Lexeme>();
            (PartOfSpeech PartOfSpeech, IEnumerable<Lexeme> Avoided)[] slots =
                [(PartOfSpeech.Noun, language.Pronouns.Forms.Values), (PartOfSpeech.Adjective, numerals), (PartOfSpeech.Verb, copulas)];

            foreach (var (partOfSpeech, avoided) in slots)
            {
                var taken = avoided.Select(l => Surface(language, l.Root)).ToHashSet();
                Assert.All(language.Lexicon.Where(l => l.PartOfSpeech == partOfSpeech), l => Assert.DoesNotContain(Surface(language, l.Root), taken));
            }
        }
    }

    [Fact]
    public void Coin_InAFrozenLexicon_Throws_ButKnownWordsAreStillThere()
    {
        var lexicon = Fresh(3).Lexicon;
        var dog = lexicon.Noun("dog");
        lexicon.Freeze();

        Assert.Same(dog, lexicon.Noun("dog"));
        Assert.Contains("frozen", Assert.Throws<InvalidOperationException>(() => lexicon.Noun("dragon")).Message);
    }

    [Fact]
    public void Coin_NewWords_LeaveTheBasicWordsAlone()
    {
        var plain = Fresh(3);
        var extended = Fresh(3);
        extended.Lexicon.Noun("dragon");
        extended.Lexicon.Verb("fly");

        Assert.Equal(plain.Lexicon.Select(x => x.Root.Form), extended.Lexicon.Take(plain.Lexicon.Count()).Select(x => x.Root.Form));
    }

    [Fact]
    public void Lookup_SameMeaningTwice_CoinsItOnce()
    {
        var lexicon = Fresh(3).Lexicon;
        var count = lexicon.Count();

        var first = lexicon.Noun("dragon");
        var second = lexicon.Noun("dragon");

        Assert.Same(first, second);
        Assert.Equal(count + 1, lexicon.Count());
        Assert.Same(first, lexicon.Last());
    }

    [Fact]
    public void Lookup_MeaningThatIsTwoPartsOfSpeech_CoinsTwoLexemes()
    {
        var lexicon = Fresh(3).Lexicon;

        var noun = lexicon.Noun("fire");
        var verb = lexicon.Verb("fire");

        Assert.Equal(PartOfSpeech.Noun, noun.PartOfSpeech);
        Assert.Equal(PartOfSpeech.Verb, verb.PartOfSpeech);
        Assert.NotSame(noun, verb);
    }

    [Fact]
    public void Coin_FromManyThreads_GivesTheSameRootsAsOneThread()
    {
        var meanings = Enumerable.Range(0, 200).Select(i => $"word{i}").ToList();
        var serial = Fresh(5).Lexicon;
        var parallel = Fresh(5).Lexicon;

        var expected = meanings.Select(m => serial.Noun(m).Root.Form).ToList();
        var actual = meanings.AsParallel().AsOrdered().Select(m => parallel.Noun(m).Root.Form).ToList();

        Assert.Equal(expected, actual);
        Assert.Equal(serial.Count(), parallel.Count());
    }

    [Fact]
    public void Coin_NewWords_FollowTheLanguagesRules()
    {
        foreach (var language in Enumerable.Range(0, 50).Select(Fresh))
        {
            var rule = language.Phonology.SyllableRule;
            foreach (var meaning in new[] { "dragon", "mountain", "river", "sword", "king" })
            {
                var root = language.Lexicon.Noun(meaning).Root;
                Assert.InRange(root.Form.Count(p => p is Vowel), 1, Generator.MaxRootSyllables);
                Assert.All(language.Phonology.Realize(new Word([root])).Syllables, s => Assert.True(rule.IsLegalOnset([.. s.Onset])));
            }
        }
    }

    [Fact]
    public void For_SameSeedAndKey_GivesTheSameDrawsOnEveryRun()
    {
        //pinned, because the whole point is that this never changes: string.GetHashCode would give
        //a different number in every process
        Assert.Equal(1643044333, Sampling.For(42, "Noun:dog").Next());
    }

    [Fact]
    public void For_DifferentKeys_GiveDifferentDraws()
    {
        var draws = Enumerable.Range(0, 1000).Select(i => Sampling.For(42, $"Noun:word{i}").Next()).ToHashSet();

        Assert.Equal(1000, draws.Count);
    }
}
