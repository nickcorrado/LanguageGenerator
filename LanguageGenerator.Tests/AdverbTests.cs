using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class AdverbTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly Clause DogWalks = new("walk", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)) { Adverbs = [] };
    private static readonly CopularClause DogIsBig = new(new Adjective("big", false), Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular))
    {
        Obliques = [],
        Adverbs = [],
    };
    private static readonly Oblique AtTheHouse = new(Role.Location, new NounPhrase("house", Number.Singular), Configuration.At);
    private static readonly Adverb Now = new("now", AdverbKind.When);
    private static readonly Adverb Again = new("again", AdverbKind.Often);

    private static Sentence Realized(Language l, Statement statement) => statement.Value switch
    {
        Clause c => Syntax.Realize(l, c, "x"),
        CopularClause c => Syntax.Realize(l, c, "x"),
        Command c => Syntax.Realize(l, c, "x"),
        CopularCommand c => Syntax.Realize(l, c, "x"),
        PassiveClause c => Syntax.Realize(l, c, "x"),
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
                Obliques = [AtTheHouse, new(Role.Goal, new NounPhrase("water", Number.Singular), Configuration.At)],
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
                Obliques = [new(Role.Source, new NounPhrase("tree", Number.Singular), Configuration.At), new(Role.Goal, new NounPhrase("water", Number.Singular), Configuration.At)],
            });

            Assert.True(IndexOf(sentence, "tree") < IndexOf(sentence, "water"));
        });

    [Fact]
    public void Realize_Standard_GoesRightBeforeTheAdjectiveInAnOVLanguage()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Comparison.Value is not ConjoinedComparison).ToList();
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Comparative(DogIsBig with { Adverbs = [Now] }, new NounPhrase("cat", Number.Plural)));
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
        sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Particle p && p == l.Intensifier.Marker)) - IndexOf(sentence, root);

    [Fact]
    public void Realize_Intensified_PutsVeryBesideTheAdjective() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var side = l.Intensifier.BeforeAdjective ? -1 : 1;
            Statement[] statements =
            [
                DogIsBig with { Complement = new Adjective("big", true) },
                DogWalks with { Subject = new NounPhrase("dog", Number.Singular, [new Adjective("big", true)]) },
                DogWalks with { Adverbs = [new Manner(new Adjective("big", true))] },
                new CopularCommand(new Adjective("big", true), Number.Singular, false) { Obliques = [], Adverbs = [] },
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
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Degree.Marker.Value is DegreeWord), l =>
        {
            var sentence = Realized(l, new Comparative(DogIsBig with { Complement = new Adjective("big", true) }, null));
            var side = l.Intensifier.BeforeAdjective ? -1 : 1;

            Assert.Equal(2 * side, Intensifier(l, sentence, "big"));
            Assert.Equal("CMPR", sentence.Words[IndexOf(sentence, "big") + side].Gloss);
        });

    [Fact]
    public void Generate_DegreeWords_GoOnTheAdjectivePhrasesSide()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var before = l.WordOrder.AdjectivePhraseDirectionality == HeadDirectionality.Final;
            Assert.Equal(Degree.Intensive, Assert.Single(l.Intensifier.Marker.Grammemes));
            Assert.Equal(before, l.Intensifier.BeforeAdjective);
            if (l.Degree.Marker.Value is DegreeWord comparative)
            {
                Assert.Equal(before, comparative.BeforeAdjective);
            }
        });
        Assert.True(GeneratedLanguages.Sample.Count(l => l.Degree.Marker.Value is DegreeWord w && w.Marker.Form.SequenceEqual(l.Intensifier.Marker.Form)) < 5);
    }

    [Fact]
    public void GenerateManner_ManyDraws_MatchTheGuesses()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var isolating = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion == Fusion.Isolating);
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateManner(rand, language.MorphologicalProfile, language.Phonology, 0.15)).ToList();
        var words = drawn.Select(m => m.Value).OfType<MannerWord>().ToList();

        Assert.InRange(drawn.Count(m => m.Value is MannerBare) / 4000.0, 0.37, 0.43);
        Assert.InRange(drawn.Count(m => m.Value is MannerAffix) / 4000.0, 0.37, 0.43);
        Assert.InRange(words.Count(w => w.BeforeAdjective) / (double)words.Count, 0.26, 0.34);
        Assert.DoesNotContain(Enumerable.Range(0, 500).Select(_ => Generator.GenerateManner(rand, isolating.MorphologicalProfile, isolating.Phonology, 0.15)),
            m => m.Value is MannerAffix);
    }

    [Fact]
    public void GenerateDuration_ManyDraws_MatchTheGuesses()
    {
        var language = GeneratedLanguages.Sample[0];
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateDuration(rand, new Dictionary<Role, Lexeme>(), language.Phonology, 0.15)).ToList();

        Assert.InRange(drawn.Count(d => d.Like == Role.DirectObject) / 4000.0, 0.37, 0.43);
        Assert.InRange(drawn.Count(d => d.Like == Role.Time) / 4000.0, 0.22, 0.28);
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
    public void Generate_Time_IsMostlyMarkedLikeAPlace()
    {
        var share = GeneratedLanguages.Sample.Count(l => l.RoleMarkers[Role.Time].Equals(l.RoleMarkers[Role.Location])) / (double)GeneratedLanguages.Sample.Count;

        Assert.InRange(share, 0.5, 0.7);
        Assert.All(GeneratedLanguages.Sample.Where(l => !l.RoleMarkers[Role.Time].Equals(l.RoleMarkers[Role.Location])), l =>
            Assert.Equal("during", Assert.IsType<Adposition>(l.RoleMarkers[Role.Time].Adposition).Gloss));
    }

    [Fact]
    public void Realize_CopularClause_SaysItsAdverbsAndTimes() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Oblique sunrise = new(Role.Time, new NounPhrase("sun", Number.Singular), Configuration.At);
            CopularClause[] clauses =
            [
                DogIsBig with { Adverbs = [Now], Obliques = [sunrise] },
                DogIsBig with { Complement = AtTheHouse, Adverbs = [Now], Obliques = [sunrise] },
                DogIsBig with { Complement = new NounPhrase("cat", Number.Singular), Adverbs = [Now], Obliques = [sunrise] },
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
                new Command("eat", Number.Singular, new NounPhrase("fish", Number.Singular)) { Adverbs = [Now] },
                new CopularCommand(AtTheHouse, Number.Plural, false) { Obliques = [], Adverbs = [Now] },
                new PassiveClause("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), null, false, []) { Adverbs = [Now] },
                new Subordinated(DogWalks, Subordinator.Purpose, new Clause("drink", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)) { Adverbs = [Now] }, true),
            ];
            Assert.All(statements, s => Assert.Single(Realized(l, s).Words, w => w.Word.Root?.Meaning == "now"));
        });

    [Fact]
    public void Realize_ConjoinedComparison_SaysItsAdverbsOnce() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Comparison.Value is ConjoinedComparison), l =>
            Assert.Single(Realized(l, new Comparative(DogIsBig with { Adverbs = [Now] }, new NounPhrase("cat", Number.Plural))).Words,
                w => w.Word.Root?.Meaning == "now"));

    [Fact]
    public void Parse_AdverbsMannersTimesAndDurations_KeepTheirOrder()
    {
        var clause = Assert.IsType<Clause>(Parsed("walk  subject:dog  when:now  manner:slow.INTS  time:night  often:again  duration:(year 3)").Value);

        ValueList<Adverbial> adverbs = [Now, new Manner(new Adjective("slow", true)), Again];
        ValueList<Oblique> obliques = [new(Role.Time, new NounPhrase("night", Number.Singular), Configuration.At), new(Role.Duration, new NounPhrase("year", Number.Paucal, Count: 3), Configuration.At)];
        Assert.Equal(adverbs, clause.Adverbs);
        Assert.Equal(obliques, clause.Obliques);
    }

    [Fact]
    public void Parse_Be_TakesTimesAndAdverbs()
    {
        var big = Assert.IsType<CopularClause>(Parsed("be  subject:dog  adjective:big.INTS  time:night  when:now").Value);
        var there = Assert.IsType<CopularClause>(Parsed("be  subject:dog  location:house  duration:(year 3)").Value);
        var command = Assert.IsType<CopularCommand>(Parsed("be.IMP  adjective:careful  when:now").Value);

        Assert.Equal(new Adjective("big", true), big.Complement.Value);
        Assert.Equal(new Oblique(Role.Time, new NounPhrase("night", Number.Singular), Configuration.At), Assert.Single(big.Obliques));
        Assert.Equal(Now, Assert.Single(big.Adverbs).Value);
        Assert.Equal(Role.Location, Assert.IsType<Oblique>(there.Complement.Value).Role);
        Assert.Equal(Role.Duration, Assert.Single(there.Obliques).Role);
        Assert.Equal(Now, Assert.Single(command.Adverbs).Value);
    }

    [Fact]
    public void Parse_Intensive_GoesOnAnyAdjective()
    {
        var phrase = Assert.IsType<NounPhrase>(Assert.IsType<Clause>(Parsed("walk  subject:(dog big.INTS)").Value).Subject.Value);
        var comparative = Assert.IsType<Comparative>(Parsed("be  subject:dog  adjective:big.CMPR.INTS").Value);
        var together = Assert.IsType<AdjectiveCoordination>(Assert.IsType<CopularClause>(Parsed("be  subject:dog  adjective:(and big.INTS old)").Value).Complement.Value);

        Assert.Equal(new Adjective("big", true), Assert.Single(phrase.Adjectives));
        Assert.Equal(new Adjective("big", true), comparative.Clause.Complement.Value);
        Assert.Equal<Adjective>([new("big", true), new("old", false)], together.Adjectives);
    }

    [Fact]
    public void Nouns_IncludeTimesSaidOfBe() =>
        Assert.Contains("night", ClauseFile.Nouns(ClauseFile.Parse("x\n  be  subject:dog  adjective:big  time:night")).Select(n => n.Noun));

    [Theory]
    [InlineData("walk  subject:dog  adverb:now", "Say what an adverb says: when:now")]
    [InlineData("walk  subject:dog  when:here", "words for where")]
    [InlineData("walk  subject:dog  often:very", "very goes on an adjective as INTS")]
    [InlineData("walk  subject:dog  often:not", "NEG on the verb")]
    [InlineData("walk  subject:dog  when:now.PL", "an adverb takes no glosses")]
    [InlineData("walk  subject:dog  when:why", "why fills no role")]
    [InlineData("walk  subject:dog  when:who", "who isn't an adverb")]
    [InlineData("walk  subject:dog  when:when", "A question can't ask when yet")]
    [InlineData("walk  subject:dog  often:(again always)", "An adverb is one word, as in often:again")]
    [InlineData("walk  subject:dog  manner:slow.CMPR", "A manner can't compare yet")]
    [InlineData("walk  subject:dog  manner:(and slow quiet)", "manner: takes one adjective")]
    [InlineData("walk  subject:dog  manner:slow.ADV", "an adverb made from an adjective is written manner:slow")]
    [InlineData("be  subject:dog  adjective:big.INTS.INTS", "more than one INTS")]
    [InlineData("be  subject:dog  adjective:big  companion:cat", "and time: or duration:")]
    [InlineData("walk  subject:dog  time:what", "can't ask when or for how long")]
    [InlineData("walk  subject:dog  duration:what", "can't ask when or for how long")]
    [InlineData("walk  subject:dog  time:night  time:sun", "more than one time")]
    public void Parse_BadAdverbs_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);
}
