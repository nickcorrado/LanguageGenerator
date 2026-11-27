using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class AdverbTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly Clause DogWalks = new Clause(new Doing("walk", null), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("dog", Number.Singular)), false, [], []);
    private static readonly Clause DogIsBig = new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("dog", Number.Singular)), false, [], []);
    private static readonly Oblique AtTheHouse = new(Role.Location, BareNoun("house", Number.Singular), Configuration.At);
    private static readonly Adverb Now = new("now", AdverbKind.When);
    private static readonly Adverb Again = new("again", AdverbKind.Often);

    private static Sentence Realized(Language l, Statement statement) => statement.Value switch
    {
        Clause c => Syntax.Realize(l, c, "x"),
        Comparative c => Syntax.Realize(l, c, "x"),
        Subordinated s => Syntax.Realize(l, s, "x"),
        _ => throw new ArgumentException("Not a statement these tests realize.", nameof(statement)),
    };

    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    [Fact]
    public void Realize_Adverb_IsItsBareRootWhereObliquesGo() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var adverb = Realized(l, DogWalks with { Adverbs = [Now] });
            var located = Realized(l, DogWalks with { Obliques = [AtTheHouse] });

            var now = adverb.Words[IndexOf(adverb, "now")];
            Assert.Equal("now", now.Gloss);
            Assert.Single(now.Word.Morphemes);
            Assert.Equal(IndexOf(located, "walk") < IndexOf(located, "house"), IndexOf(adverb, "walk") < IndexOf(adverb, "now"));
        });

    //how far each of these roots is from the verb's, in words
    private static int[] Distances(Sentence sentence, params string[] roots) =>
        [.. roots.Select(r => Math.Abs(IndexOf(sentence, r) - IndexOf(sentence, "walk")))];

    [Fact]
    public void Realize_Modifiers_GoNearerTheVerbByWhatTheySay() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            //written outermost first, as 'now, again, at the house, well, to the water' could be
            var sentence = Realized(l, DogWalks with
            {
                Adverbs = [Now, Again, new Manner(new Adjective("good", false))],
                Obliques = [AtTheHouse, new(Role.Goal, BareNoun("water", Number.Singular), Configuration.At)],
            });

            var distances = Distances(sentence, "water", "good", "house", "again", "now");
            Assert.Equal(distances.Order(), distances);
        });

    [Fact]
    public void Realize_ModifiersOfARank_KeepTheirWrittenOrder() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Realized(l, DogWalks with
            {
                Obliques = [new(Role.Source, BareNoun("tree", Number.Singular), Configuration.At), new(Role.Goal, BareNoun("water", Number.Singular), Configuration.At)],
            });

            Assert.True(IndexOf(sentence, "tree") < IndexOf(sentence, "water"));
        });

    [Fact]
    public void Realize_Standard_GoesRightBeforeTheAdjectiveInAnOVLanguage()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Comparison.Value is not ConjoinedComparison).ToList();
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Comparative(DogIsBig with { Adverbs = [Now] }, BareNoun("cat", Number.Plural)));
            var (cat, big, now) = (IndexOf(sentence, "cat"), IndexOf(sentence, "big"), IndexOf(sentence, "now"));
            if (l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Final)
            {
                //with nothing of the clause's between them, only the standard's own marker and the
                //adjective's degree word
                Assert.True(cat < big);
                Root?[] clause = [l.Lexicon.Adverb("now").Root, l.Lexicon.Noun("dog").Root, l.Predication.Copula.Root];
                Assert.DoesNotContain(sentence.Words.Skip(cat + 1).Take(big - cat - 1), w => clause.Contains(w.Word.Root));
            }
            else if (l.WordOrder.ObliquePosition == ObliquePosition.AfterVerbAndObject)
            {
                //nearest the verb where obliques go
                Assert.True(big < cat && cat < now);
            }
        });
        Assert.Contains(languages, l => l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Final && l.WordOrder.ObliquePosition == ObliquePosition.BetweenObjectAndVerb);
    }

    [Fact]
    public void Realize_Manner_MakesTheAdverbAsTheLanguageDoes()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Realized(l, DogWalks with { Adverbs = [new Manner(new Adjective("good", false))] });
            var words = sentence.Words.ToList();
            var good = IndexOf(sentence, "good");
            var morphemes = words[good].Word.Morphemes;
            switch (l.Manner.Value)
            {
                case MannerBare:
                    Assert.Single(morphemes);
                    Assert.DoesNotContain(words, w => w.Gloss.Contains("ADV"));
                    break;
                case MannerAffix affix:
                    Assert.Equal(affix.Marker, (affix.Position == ExponentPosition.Prefix ? morphemes[0] : morphemes[^1]).Value);
                    Assert.Equal(2, morphemes.Count);
                    Assert.Contains("ADV", words[good].Gloss);
                    break;
                case MannerWord word:
                    var at = words.FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Particle p && p == word.Marker));
                    Assert.Equal(good + (word.BeforeAdjective ? -1 : 1), at);
                    Assert.Equal("ADV", words[at].Gloss);
                    break;
            }
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Manner.Value is MannerBare);
        Assert.Contains(GeneratedLanguages.Sample, l => l.Manner.Value is MannerAffix);
        Assert.Contains(GeneratedLanguages.Sample, l => l.Manner.Value is MannerWord);
    }

    //where 'very' is in a sentence, relative to the word with this root: -1 right before it, 1 right after
    private static int Intensifier(Language l, Sentence sentence, string root) =>
        sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Particle p && p == l.Intensifier.Particle)) - IndexOf(sentence, root);

    [Fact]
    public void Realize_Intensified_PutsVeryBesideTheAdjective() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var side = l.Intensifier.Side == Side.Before ? -1 : 1;
            Statement[] statements =
            [
                DogIsBig with { Predicate = new Being(new Adjective("big", true)) },
                DogWalks with { Force = new Declarative(Tense.Present, Aspect.Imperfective, new NounPhrase(new Noun("dog", Number.Singular, [new Adjective("big", true)], null, null, null, null, null), null)) },
                DogWalks with { Adverbs = [new Manner(new Adjective("big", true))] },
                new Clause(new Being(new Adjective("big", true)), new Directive(Number.Singular), false, [], []),
            ];
            Assert.All(statements, s =>
            {
                var sentence = Realized(l, s);
                Assert.Equal(side, Intensifier(l, sentence, "big"));
                //where the phrase's affixes go on its edge, 'very' can be the edge
                Assert.Contains("INTS", sentence.Words[IndexOf(sentence, "big") + side].Gloss.Split('-', '.'));
            });
            Assert.DoesNotContain(Realized(l, DogIsBig).Words, w => w.Gloss == "INTS");
        });

    [Fact]
    public void Realize_IntensifiedComparative_PutsTheComparativeNearer() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Degree.Marker.Value is Free), l =>
        {
            var sentence = Realized(l, new Comparative(DogIsBig with { Predicate = new Being(new Adjective("big", true)) }, null));
            var side = l.Intensifier.Side == Side.Before ? -1 : 1;

            Assert.Equal(2 * side, Intensifier(l, sentence, "big"));
            Assert.Equal("CMPR", sentence.Words[IndexOf(sentence, "big") + side].Gloss);
        });

    [Fact]
    public void Generate_DegreeWords_GoOnTheAdjectivePhrasesSide()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var side = l.WordOrder.AdjectivePhraseDirectionality == HeadDirectionality.Final ? Side.Before : Side.After;
            Assert.Equal(Degree.Intensive, Assert.Single(l.Intensifier.Particle.Grammemes));
            Assert.Equal(side, l.Intensifier.Side);
            if (l.Degree.Marker.Value is Free comparative)
            {
                Assert.Equal(side, comparative.Side);
            }
        });
        Assert.True(GeneratedLanguages.Sample.Count(l => l.Degree.Marker.Value is Free w && w.Particle.Form.SequenceEqual(l.Intensifier.Particle.Form)) < 5);
    }

    [Fact]
    public void GenerateManner_IsolatingLanguages_NeverTakeAnAffix()
    {
        var isolating = GeneratedLanguages.With(s => s.Fix(Settings.Fusion, Fusion.Isolating));
        var rand = new Random(1);

        Assert.DoesNotContain(Enumerable.Range(0, 500).Select(_ => Generator.GenerateManner(rand, isolating.MorphologicalProfile, isolating.Phonology)),
            m => m.Value is MannerAffix);
    }

    [Fact]
    public void GenerateDuration_MarkedLikeNoRole_SaysFor()
    {
        var language = GeneratedLanguages.Sample[0];
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 1000).Select(_ => Generator.GenerateDuration(rand, new Dictionary<Role, Lexeme>(), language.Phonology)).ToList();

        Assert.Contains(drawn, d => d.Like is null);
        Assert.All(drawn.Where(d => d.Like is null), d => Assert.Equal("for", d.Word!.Root.Meaning));
    }

    [Fact]
    public void Generate_Duration_IsMarkedLikeTheObjectLikeATimeOrWithFor() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var marker = l.RoleMarkers[Role.Duration];
            Assert.True(marker.Equals(l.RoleMarkers[Role.DirectObject]) || marker.Equals(l.RoleMarkers[Role.Time]) || marker.Adposition is { Gloss: "for" },
                $"{marker} is like nothing.");
        });

    [Fact]
    public void Generate_TimeMarkedApartFromAPlace_SaysDuring()
    {
        Assert.Contains(GeneratedLanguages.Sample, l => !l.RoleMarkers[Role.Time].Equals(l.RoleMarkers[Role.Location]));
        Assert.All(GeneratedLanguages.Sample.Where(l => !l.RoleMarkers[Role.Time].Equals(l.RoleMarkers[Role.Location])), l =>
            Assert.Equal("during", Assert.IsType<Adposition>(l.RoleMarkers[Role.Time].Adposition).Gloss));
    }

    [Fact]
    public void Realize_CopularClause_SaysItsAdverbsAndTimes() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Oblique sunrise = new(Role.Time, BareNoun("sun", Number.Singular), Configuration.At);
            Clause[] clauses =
            [
                DogIsBig with { Adverbs = [Now], Obliques = [sunrise] },
                DogIsBig with { Predicate = new Being(AtTheHouse), Adverbs = [Now], Obliques = [sunrise] },
                DogIsBig with { Predicate = new Being(BareNoun("cat", Number.Singular)), Adverbs = [Now], Obliques = [sunrise] },
            ];
            Assert.All(clauses, c =>
            {
                var sentence = Realized(l, c);
                Assert.NotEqual(-1, IndexOf(sentence, "now"));
                Assert.NotEqual(-1, IndexOf(sentence, "sun"));
            });
        });

    [Fact]
    public void Realize_EveryKindOfClause_KeepsItsAdverbs() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Statement[] statements =
            [
                new Clause(new Doing("eat", BareNoun("fish", Number.Singular)), new Directive(Number.Singular), false, [], [Now]),
                new Clause(new Being(AtTheHouse), new Directive(Number.Plural), false, [], [Now]),
                new Clause(new Done("see", null), new Declarative(Tense.Past, Aspect.Perfective, BareNoun("dog", Number.Singular)), false, [], [Now]),
                new Subordinated(DogWalks, Subordinator.Purpose, new Clause(new Doing("drink", null), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("dog", Number.Singular)), false, [], [Now]), true),
            ];
            Assert.All(statements, s => Assert.Single(Realized(l, s).Words, w => w.Word.Root?.Meaning == "now"));
        });

    [Fact]
    public void Realize_ConjoinedComparison_SaysItsAdverbsOnce() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ConjoinedComparison), l =>
            Assert.Single(Realized(l, new Comparative(DogIsBig with { Adverbs = [Now] }, BareNoun("cat", Number.Plural))).Words,
                w => w.Word.Root?.Meaning == "now"));

    [Fact]
    public void Parse_AdverbsMannersTimesAndDurations_KeepTheirOrder()
    {
        var clause = Assert.IsType<Clause>(Parsed("walk  subject:dog  when:now  manner:slow.INTS  time:night  often:again  duration:(year 3)").Value);

        ValueList<Adverbial> adverbs = [Now, new Manner(new Adjective("slow", true)), Again];
        ValueList<Oblique> obliques = [new(Role.Time, BareNoun("night", Number.Singular), Configuration.At), new(Role.Duration, new NounPhrase(new Noun("year", Number.Paucal, [], 3, null, null, null, null), null), Configuration.At)];
        Assert.Equal(adverbs, clause.Adverbs);
        Assert.Equal(obliques, clause.Obliques);
    }

    [Fact]
    public void Parse_Be_TakesTimesAndAdverbs()
    {
        var big = Assert.IsType<Clause>(Parsed("be  subject:dog  adjective:big.INTS  time:night  when:now").Value);
        var there = Assert.IsType<Clause>(Parsed("be  subject:dog  location:house  duration:(year 3)").Value);
        var command = Assert.IsType<Clause>(Parsed("be.IMP  adjective:careful  when:now").Value);

        Assert.Equal(new Adjective("big", true), PredicateOf<Being>(big).Complement.Value);
        Assert.Equal(new Oblique(Role.Time, BareNoun("night", Number.Singular), Configuration.At), Assert.Single(big.Obliques));
        Assert.Equal(Now, Assert.Single(big.Adverbs).Value);
        Assert.Equal(Role.Location, Assert.IsType<Oblique>(PredicateOf<Being>(there).Complement.Value).Role);
        Assert.IsType<Being>(command.Predicate.Value);
        Assert.IsType<Directive>(command.Force.Value);
        Assert.Equal(Role.Duration, Assert.Single(there.Obliques).Role);
        Assert.Equal(Now, Assert.Single(command.Adverbs).Value);
    }

    [Fact]
    public void Parse_Intensive_GoesOnAnyAdjective()
    {
        var phrase = Assert.IsType<NounPhrase>(ForceOf<Declarative>(Parsed("walk  subject:(dog big.INTS)")).Subject.Value);
        var comparative = Assert.IsType<Comparative>(Parsed("be  subject:dog  adjective:big.CMPR.INTS").Value);
        var together = Assert.IsType<AdjectiveCoordination>(PredicateOf<Being>(Parsed("be  subject:dog  adjective:(and big.INTS old)")).Complement.Value);

        Assert.Equal(new Adjective("big", true), Assert.Single(NounOf(phrase).Adjectives));
        Assert.Equal(new Adjective("big", true), PredicateOf<Being>(comparative.Clause).Complement.Value);
        Assert.Equal<Adjective>([new("big", true), new("old", false)], together.Adjectives);
    }

    [Fact]
    public void Nouns_IncludeTimesSaidOfBe() =>
        Assert.Contains("night", ClauseFile.Nouns(ClauseFile.Parse("x\n  be  subject:dog  adjective:big  time:night")).Select(n => n.Noun));

    [Theory]
    [InlineData("walk  subject:dog  adverb:now", "Say what an adverb says: when:now")]
    [InlineData("walk  subject:dog  when:inside", "words for where")]
    [InlineData("walk  subject:dog  often:very", "very goes on an adjective as INTS")]
    [InlineData("walk  subject:dog  often:not", "NEG on the verb")]
    [InlineData("walk  subject:dog  when:now.PL", "an adverb takes no glosses")]
    [InlineData("walk  subject:dog  when:why", "why fills no role")]
    [InlineData("walk  subject:dog  when:who", "who isn't an adverb")]
    [InlineData("walk  subject:dog  when:when", "so it's time:when")]
    [InlineData("walk  subject:dog  often:(again always)", "An adverb is one word, as in often:again")]
    [InlineData("walk  subject:dog  manner:slow.CMPR", "A manner can't compare yet")]
    [InlineData("walk  subject:dog  manner:(and slow quiet)", "manner: takes one adjective")]
    [InlineData("walk  subject:dog  manner:slow.ADV", "an adverb made from an adjective is written manner:slow")]
    [InlineData("be  subject:dog  adjective:big.INTS.INTS", "more than one INTS")]
    [InlineData("be  subject:dog  adjective:big  companion:cat", "and time: or duration:")]
    [InlineData("walk  subject:dog  time:what", "is time:when")]
    [InlineData("walk  subject:dog  duration:what", "can't ask for how long")]
    [InlineData("walk  subject:dog  time:night  time:sun", "more than one time")]
    public void Parse_BadAdverbs_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);
}
