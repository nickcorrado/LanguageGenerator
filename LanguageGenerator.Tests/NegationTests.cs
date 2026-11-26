namespace LanguageGenerator.Tests;

public class NegationTests
{
    private static Sentence DogDidNotSeeCat(Language l, bool negative = true) =>
        Syntax.Realize(l, new Clause(new Doing("see", new NounPhrase("cat", Number.Singular)), new Declarative(Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular)), negative, [], []), "the dog didn't see the cat");

    //which word is the verb itself, rather than a particle or an argument
    private static int VerbIndex(Sentence sentence) => sentence.Words.ToList().FindIndex(w => w.Word.Root?.Meaning == "see");

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
}
