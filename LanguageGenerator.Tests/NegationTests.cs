namespace LanguageGenerator.Tests;

public class NegationTests
{
    private static Sentence DogDidNotSeeCat(Language l, bool negative = true) =>
        Syntax.Realize(l, new Clause("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), new NounPhrase("cat", Number.Singular), negative), "the dog didn't see the cat");

    //which word is the verb itself, rather than a particle or an argument
    private static int VerbIndex(Sentence sentence) => sentence.Words.ToList().FindIndex(w => w.Word.Root?.Meaning == "see");

    [Fact]
    public void GenerateNegation_ManyDraws_MatchTheWalsCounts()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var rand = new Random(1);

        var strategies = Enumerable.Range(0, 5000).Select(_ => Generator.GenerateNegation(rand, language.MorphologicalProfile, language.Phonology, 0.15).Strategy).ToList();
        double Share(NegationStrategy s) => strategies.Count(x => x == s) / 5000.0;

        //WALS 143A: 525, 171, 162, 202 and 114 of the 1174 languages modeled
        Assert.InRange(Share(NegationStrategy.ParticleBefore), 0.43, 0.47);
        Assert.InRange(Share(NegationStrategy.ParticleAfter), 0.13, 0.16);
        Assert.InRange(Share(NegationStrategy.Prefix), 0.12, 0.15);
        Assert.InRange(Share(NegationStrategy.Suffix), 0.16, 0.19);
        Assert.InRange(Share(NegationStrategy.Double), 0.08, 0.11);
    }

    [Fact]
    public void Generate_IsolatingLanguages_NeverNegateWithAnAffix()
    {
        var isolating = GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating).ToList();

        Assert.NotEmpty(isolating);
        Assert.All(isolating, l => Assert.DoesNotContain(l.Negation.Markers, m => m.Position is ExponentPosition.Prefix or ExponentPosition.Suffix));
    }

    [Fact]
    public void Realize_NegativeClause_PutsEachMarkerWhereTheLanguageDoes()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = DogDidNotSeeCat(language);
            var verb = VerbIndex(sentence);
            var words = sentence.Words.Select(w => w.Word).ToList();

            switch (language.Negation.Strategy)
            {
                case NegationStrategy.Prefix:
                    Assert.Equal("NEG", words[verb].Morphemes[0].Gloss);
                    break;
                case NegationStrategy.Suffix:
                    Assert.Equal("NEG", words[verb].Morphemes[^1].Gloss);
                    break;
                case NegationStrategy.ParticleBefore:
                    Assert.Equal(1, words.Take(verb).Count(w => w.Gloss == "NEG"));
                    break;
                case NegationStrategy.ParticleAfter:
                    Assert.Equal(1, words.Skip(verb + 1).Count(w => w.Gloss == "NEG"));
                    break;
                case NegationStrategy.Double:
                    Assert.Equal(1, words.Take(verb).Count(w => w.Gloss == "NEG"));
                    Assert.Equal(1, words.Skip(verb + 1).Count(w => w.Gloss == "NEG"));
                    break;
            }
        }
    }

    [Fact]
    public void Realize_NegativeClause_KeepsTheVerbsParticlesAndArgumentsTogether()
    {
        //the negative goes outside the verb's own particles, so the constituents stay in word order
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Negation.Strategy is NegationStrategy.ParticleBefore or NegationStrategy.ParticleAfter or NegationStrategy.Double))
        {
            var affirmative = DogDidNotSeeCat(language, negative: false).Words.Select(w => w.Gloss).ToList();
            var negative = DogDidNotSeeCat(language).Words.Select(w => w.Gloss).Where(g => g != "NEG").ToList();

            Assert.Equal(affirmative, negative);
        }
    }

    [Fact]
    public void Realize_AffirmativeClause_HasNoNegative()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.DoesNotContain(DogDidNotSeeCat(l, negative: false).Words, w => w.Gloss.Contains("NEG")));
    }

    [Fact]
    public void Realize_NegativeClauses_FollowThePhonology()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var rule = language.Phonology.SyllableRule;
            Assert.All(DogDidNotSeeCat(language).Words.SelectMany(w => w.Syllables), s =>
            {
                Assert.True(rule.IsLegalOnset(s.Onset));
                Assert.True(rule.IsLegalCoda(s.Coda is null ? [] : [s.Coda]));
            });
        }
    }
}
