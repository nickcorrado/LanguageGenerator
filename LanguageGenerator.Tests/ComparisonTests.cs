using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ComparisonTests
{
    private static readonly CopularClause DogIsBig = new(new Adjective("big", false), Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)) { Obliques = [], Adverbs = [] };
    private static readonly NounPhrase Cats = new("cat", Number.Plural);

    //'the dog is bigger than the cats', in only the basic meanings, since coining new ones would add
    //to the shared sample's lexicons
    private static Sentence Bigger(Language l) => Syntax.Realize(l, new Comparative(DogIsBig, Cats), "x");

    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    [Fact]
    public void GenerateComparison_ManyDraws_MatchTheWalsCounts()
    {
        var language = GeneratedLanguages.Sample[0];
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateComparison(rand, language.WordOrder, language.Phonology, 0.15)).ToList();

        //WALS 121A: 78 locational, 33 exceed, 34 conjoined and 22 with a particle, of 167
        Assert.InRange(drawn.Count(c => c.Value is LocationalComparison) / 4000.0, 0.44, 0.49);
        Assert.InRange(drawn.Count(c => c.Value is ExceedComparison) / 4000.0, 0.18, 0.22);
        Assert.InRange(drawn.Count(c => c.Value is ConjoinedComparison) / 4000.0, 0.18, 0.23);
        Assert.InRange(drawn.Count(c => c.Value is ParticleComparison) / 4000.0, 0.11, 0.15);
        //and the guessed places a locational standard is marked as
        var locational = drawn.Select(c => c.Value).OfType<LocationalComparison>().ToList();
        Assert.InRange(locational.Count(c => c.Role == Role.Source) / (double)locational.Count, 0.56, 0.64);
        Assert.InRange(locational.Count(c => c.Role == Role.Goal) / (double)locational.Count, 0.17, 0.23);
        Assert.InRange(locational.Count(c => c.Role == Role.Location) / (double)locational.Count, 0.17, 0.23);
    }

    [Fact]
    public void Generate_Particles_GoOnTheirAdpositionsSide() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ParticleComparison), l =>
            Assert.Equal(l.WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial, ((ParticleComparison)l.Comparison.Value!).BeforeStandard));

    [Fact]
    public void Parse_Standard_MakesAComparative()
    {
        var comparative = Assert.IsType<Comparative>(Parsed("be  subject:marta  adjective:tall.CMPR  standard:man.PL.DEF").Value);

        Assert.Equal(new Adjective("tall", false), comparative.Clause.Complement.Value);
        Assert.Equal(new NounPhrase("man", Number.Plural, Definiteness: Definiteness.Definite), comparative.Standard.Value);
    }

    [Fact]
    public void Parse_StandardWithAnAddressee_IsAnAddressedComparative()
    {
        var addressed = Assert.IsType<Addressed>(Parsed("be  subject:2SG  adjective:tall.CMPR  standard:1SG  addressee:marta").Value);

        Assert.Equal(new Pronoun(Person.First, Number.Singular), Assert.IsType<Comparative>(addressed.Statement.Value).Standard.Value);
    }

    [Theory]
    [InlineData("see  subject:dog  standard:cat", "Only be takes a standard:")]
    [InlineData("be  subject:dog  noun:fool  standard:cat", "compares an adjective:")]
    [InlineData("be  subject:dog  location:house  standard:cat", "compares an adjective:")]
    [InlineData("be.NEG  subject:dog  adjective:big.CMPR  standard:cat", "can't be negated yet")]
    [InlineData("be.IMP  adjective:big.CMPR  standard:cat", "A command can't compare yet")]
    public void Parse_BadStandards_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);

    [Fact]
    public void Realize_Comparative_SaysTheDogTheCatsAndWhatTheyCompareIn()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Bigger(l);

            Assert.All(new[] { "dog", "cat", "big" }, meaning => Assert.True(IndexOf(sentence, meaning) >= 0, meaning));
        });
    }

    [Fact]
    public void Realize_LocationalComparison_MarksTheStandardLikeItsPlace()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is LocationalComparison), l =>
        {
            var sentence = Bigger(l);
            var marked = l.RoleMarkers[((LocationalComparison)l.Comparison.Value!).Role] switch
            {
                { Adposition: { } a } => IndexOf(sentence, a.Lexeme.Root) >= 0,
                { Case: { } c } => sentence.Words[IndexOf(sentence, "cat")].Gloss.Split('-', '.', '[', ']').Contains(c.Gloss)
                    || sentence.Words.Any(w => w.Word.Morphemes.Any(m => m.Value is Particle { Grammemes: var g } && g.Contains(c))),
                _ => true,
            };

            Assert.True(marked);
        });
    }

    [Fact]
    public void Realize_ParticleComparison_PutsTheParticleBesideTheStandard()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ParticleComparison), l =>
        {
            var particle = (ParticleComparison)l.Comparison.Value!;
            var sentence = Bigger(l);
            var (than, cat) = (IndexOf(sentence, particle.Than.Root), IndexOf(sentence, "cat"));

            Assert.True(particle.BeforeStandard ? than < cat : than > cat);
            Assert.DoesNotContain(sentence.Words, w => w.Word.Morphemes.Any(m => m.Value is Root { Meaning: "from" or "to" or "at" or "toward" }));
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Comparison.Value is ParticleComparison);
    }

    [Fact]
    public void Realize_ConjoinedComparison_IsTheClauseThenTheStandardDenied()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ConjoinedComparison), l =>
        {
            var first = Syntax.Realize(l, DogIsBig, "x");
            var second = Syntax.Realize(l, DogIsBig with { Subject = Cats, Negative = true }, "x");

            Assert.Equal(first.Words.Concat(second.Words).Select(w => w.Gloss), Bigger(l).Words.Select(w => w.Gloss));
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Comparison.Value is ConjoinedComparison);
    }

    [Fact]
    public void Realize_ExceedComparison_PutsTheStandardAsTheVerbsObject()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ExceedComparison).ToList();
        Assert.All(languages, l =>
        {
            var exceed = ((ExceedComparison)l.Comparison.Value!).Exceed;
            var sentence = Bigger(l);
            var (verb, cat) = (IndexOf(sentence, exceed.Root), IndexOf(sentence, "cat"));

            Assert.True(verb >= 0);
            Assert.True(l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial ? verb < cat : verb > cat);
            //the standard is said as an object is
            var seen = Syntax.Realize(l, new Clause("see", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular), Cats) { Adverbs = [] }, "x");
            Assert.Equal(seen.Words[IndexOf(seen, "cat")].Gloss, sentence.Words[cat].Gloss);
        });
        Assert.NotEmpty(languages);
    }

    [Fact]
    public void Generate_ExceedVerb_IsAClosedClassTheLexiconAvoids() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ExceedComparison), l =>
        {
            var exceed = ((ExceedComparison)l.Comparison.Value!).Exceed;
            Assert.Equal(PartOfSpeech.Verb, exceed.PartOfSpeech);
            Assert.DoesNotContain(l.Lexicon.Where(x => x.PartOfSpeech == PartOfSpeech.Verb), x => x.Root.Form.SequenceEqual(exceed.Root.Form));
        });

    [Fact]
    public void GenerateDegree_ManyDraws_MatchTheWalsCountsAndGuesses()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var rand = new Random(1);
        DegreeMarking Draw(Comparison comparison) => Generator.GenerateDegree(rand, comparison, language.MorphologicalProfile, language.WordOrder, language.Phonology, 0.15);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Draw(new LocationalComparison(Role.Source))).ToList();
        var words = drawn.Select(d => d.Marker.Value).OfType<DegreeWord>().ToList();

        Assert.InRange(drawn.Count(d => d.Marker.Value is DegreeAffix) / 4000.0, 0.22, 0.28);
        Assert.All(words, w => Assert.Equal(language.WordOrder.AdjectivePhraseDirectionality == HeadDirectionality.Final, w.BeforeAdjective));
        Assert.InRange(drawn.Count(d => d.WithStandard) / 4000.0, 0.13, 0.17);
        Assert.InRange(Enumerable.Range(0, 4000).Count(_ => Draw(new ParticleComparison(null!, true)).WithStandard) / 4000.0, 0.57, 0.63);
        Assert.DoesNotContain(Enumerable.Range(0, 500).Select(_ => Draw(new ConjoinedComparison())), d => d.WithStandard);
    }

    [Fact]
    public void Generate_IsolatingLanguages_MarkComparativesAndSimilesWithWords() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating), l =>
        {
            Assert.IsType<DegreeWord>(l.Degree.Marker.Value);
            Assert.IsType<SimilativeWord>(l.Similative.Value);
        });

    //'the dog is bigger', with nothing it's compared with
    private static Sentence BiggerAlone(Language l) => Syntax.Realize(l, new Comparative(DogIsBig, null), "x");

    //where the comparative's marker is in a sentence: next to the adjective's root in its word, or a
    //word right beside it. Null if it isn't there
    private static bool? Marked(Language l, Sentence sentence)
    {
        var big = IndexOf(sentence, "big");
        switch (l.Degree.Marker.Value)
        {
            case DegreeAffix affix:
            {
                var morphemes = sentence.Words[big].Word.Morphemes.ToList();
                var at = morphemes.FindIndex(m => m.Value is Affix a && a == affix.Marker);
                var root = morphemes.FindIndex(m => m.Value is Root);
                return at < 0 ? null : at == root + (affix.Position == ExponentPosition.Prefix ? -1 : 1);
            }
            case DegreeWord word:
            {
                var at = sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Particle p && p == word.Marker));
                return at < 0 ? null : at == big + (word.BeforeAdjective ? -1 : 1);
            }
            default:
                throw new InvalidOperationException();
        }
    }

    [Fact]
    public void Realize_ComparativeWithoutAStandard_MarksTheAdjective() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = BiggerAlone(l);

            Assert.True(Marked(l, sentence));
            Assert.Contains(sentence.Words, w => w.Gloss.Split('-', '.', '[', ']').Contains("CMPR"));
            Assert.Equal(-1, IndexOf(sentence, "cat"));
        });

    [Fact]
    public void Realize_ComparativeWithAStandard_MarksTheAdjectiveOnlyWhereTheLanguageDoes()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Comparison.Value is not ConjoinedComparison).ToList();
        Assert.All(languages, l => Assert.Equal(l.Degree.WithStandard ? true : null, Marked(l, Bigger(l))));
        Assert.Contains(languages, l => l.Degree.WithStandard);
        Assert.Contains(languages, l => !l.Degree.WithStandard);
    }

    private static readonly Clause DogWalks = new("walk", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)) { Adverbs = [] };

    //'the dog walks like a cat'
    private static Sentence WalksLikeACat(Language l) => Syntax.Realize(l, DogWalks with { Obliques = [new(Role.Similative, new NounPhrase("cat", Number.Singular), Configuration.At)] }, "x");

    [Fact]
    public void Generate_SimilativeWords_GoOnTheirAdpositionsSide() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Similative.Value is SimilativeWord), l =>
            Assert.Equal(l.WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial, ((SimilativeWord)l.Similative.Value!).BeforeStandard));

    [Fact]
    public void Realize_Simile_MarksWhatSomethingIsLikeAtItsEdge()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = WalksLikeACat(l);
            var words = sentence.Words.ToList();
            var cat = IndexOf(sentence, "cat");
            switch (l.Similative.Value)
            {
                case SimilativeWord word:
                {
                    //with nothing between it and the noun but the noun's own particles
                    var like = IndexOf(sentence, word.Like.Root);
                    Assert.True(word.BeforeStandard ? like < cat : like > cat);
                    Assert.All(words[(Math.Min(like, cat) + 1)..Math.Max(like, cat)], w => Assert.Null(w.Word.Root));
                    break;
                }
                case SimilativeAffix affix:
                {
                    var marked = words.FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Affix a && a == affix.Marker));
                    var morphemes = words[marked].Word.Morphemes;
                    Assert.Equal(affix.Marker, (affix.Position == ExponentPosition.Prefix ? morphemes[0] : morphemes[^1]).Value);
                    Assert.Contains("SIM", words[marked].Gloss);
                    break;
                }
            }
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Similative.Value is SimilativeWord);
        Assert.Contains(GeneratedLanguages.Sample, l => l.Similative.Value is SimilativeAffix);
    }

    [Fact]
    public void Realize_Simile_GoesWhereObliquesGo() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var simile = WalksLikeACat(l);
            var located = Syntax.Realize(l, DogWalks with { Obliques = [new(Role.Location, new NounPhrase("cat", Number.Singular), Configuration.At)] }, "x");

            Assert.Equal(IndexOf(located, "walk") < IndexOf(located, "cat"), IndexOf(simile, "walk") < IndexOf(simile, "cat"));
        });

    [Fact]
    public void Parse_Comparative_WithoutAStandard_HasNone()
    {
        var comparative = Assert.IsType<Comparative>(Parsed("be  subject:marta  adjective:tall.CMPR").Value);

        Assert.Equal(new Adjective("tall", false), comparative.Clause.Complement.Value);
        Assert.Null(comparative.Standard);
    }

    [Fact]
    public void Parse_Like_IsASimile()
    {
        var clause = Assert.IsType<Clause>(Parsed("shine  subject:3SG.N  like:water").Value);

        Assert.Equal(new Oblique(Role.Similative, new NounPhrase("water", Number.Singular), Configuration.At), Assert.Single(clause.Obliques));
    }

    [Theory]
    [InlineData("be  subject:dog  adjective:big  standard:cat", "adjective:big.CMPR")]
    [InlineData("be.IMP  adjective:big.CMPR", "A command can't compare yet")]
    [InlineData("be.ABIL  subject:dog  adjective:big.CMPR", "A comparison can't take ABIL")]
    [InlineData("see  subject:(dog big.CMPR)", "Only the adjective: be says of its subject can take CMPR")]
    [InlineData("be  subject:dog  adjective:(and big.CMPR old)", "Only the adjective: be says of its subject can take CMPR")]
    [InlineData("be  subject:dog  adjective:big.PL", "adjectives have no features but CMPR")]
    [InlineData("be  subject:dog  adjective:big  like:cat", "be can't say what its subject is like yet")]
    [InlineData("walk  subject:dog  like:what", "can't ask what something is like")]
    [InlineData("walk  subject:dog  like:cat  like:fish", "more than one like")]
    public void Parse_BadComparativesAndSimiles_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);
}
