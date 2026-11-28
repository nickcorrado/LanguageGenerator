using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class CoordinationTests
{
    private static Sentence Parsed(string gloss) => Stated(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance);

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static Argument SubjectOf(Sentence statement) => EventOf(statement).Subject;

    [Fact]
    public void Parse_NounsJoined_AreACoordinationInOrder()
    {
        var subject = Assert.IsType<Coordination>(SubjectOf(Parsed("sleep  subject:(and dog.DEF cat.DEF)")).Value);

        Assert.Equal(Conjunction.And, subject.Conjunction);
        Assert.Equal(["dog", "cat"], subject.Items.Select(i => NounOf(i).Meaning));
    }

    [Fact]
    public void Parse_Conjuncts_CanBeAnythingARoleTakes()
    {
        var @object = PredicateOf<Doing>(Parsed("carry  subject:3SG.F  object:(or (purse.NDF empty) 1SG (and dog cat))")).Object!.Value;
        var items = Assert.IsType<Coordination>(@object.Value).Items;

        Assert.Equal(["empty"], NounOf(items[0]).Adjectives.Select(a => a.Meaning));
        Assert.Equal(new Pronoun(Person.First, Number.Singular), items[1].Value);
        Assert.Equal(Conjunction.And, Assert.IsType<Coordination>(items[2].Value).Conjunction);
    }

    [Fact]
    public void Parse_AdjectivesJoined_AreSaidTogether()
    {
        var clause = PredicateOf<Being>(Parsed("be  subject:inn.DEF  adjective:(and warm crowded)"));

        Assert.Equal(new AdjectiveCoordination(Conjunction.And, [new("warm", false), new("crowded", false)]), clause.Attribute.Value);
    }

    [Fact]
    public void Parse_LinesStartingWithAConjunction_JoinTheirClausesToTheOnesAbove()
    {
        var entry = Assert.Single(ClauseFile.Parse("""
            they sit and argue, but she sleeps
              sit  subject:hunter.PL
              and  argue  subject:hunter.PL
              but  sleep  subject:3SG.F
            """));

        var outer = Assert.IsType<Coordinated<Sentence>>(Stated(entry.Utterance).Value);
        Assert.Equal(Conjunction.But, outer.Conjunction);
        var inner = Assert.IsType<Coordinated<Sentence>>(outer.First.Value);
        Assert.Equal(("sit", "argue"), (PredicateOf<Doing>(inner.First).Verb, PredicateOf<Doing>(inner.Second).Verb));
        Assert.Equal("sleep", PredicateOf<Doing>(outer.Second).Verb);
    }

    [Fact]
    public void Parse_EntriesAfterAJoinedOne_StartAfresh()
    {
        var entries = ClauseFile.Parse("one\n  sit  subject:dog\n  and  sleep  subject:dog\ntwo\n  sleep  subject:cat");

        Assert.Equal(2, entries.Count);
        Assert.IsType<Clause>(Stated(entries[1].Utterance).Value);
    }

    [Theory]
    [InlineData("x\n  sleep  subject:(dog and cat)", "Write and first")]
    [InlineData("x\n  sleep  subject:(and dog)", "joins too few things")]
    [InlineData("x\n  sleep  subject:(but dog cat)", "but joins only clauses")]
    [InlineData("x\n  and  sleep  subject:dog", "can't come first")]
    [InlineData("x\n  be  subject:inn  adjective:(warm crowded)", "Adjectives in parentheses are joined")]
    [InlineData("x\n  be  subject:inn  adjective:(and warm crowded)  standard:house", "compares an adjective:")]
    public void Parse_BadCoordination_SaysWhy(string text, string message) => Assert.Contains(message, Error(text).Message);

    [Fact]
    public void Nouns_IncludeEveryConjunct() =>
        Assert.Equal(["dog", "cat", "bird"], ClauseFile.Nouns(ClauseFile.Parse("x\n  see  subject:(and dog cat)\n  and  sleep  subject:bird")).Select(n => n.Noun));

    [Fact]
    public void GenerateConjunctions_ManyDraws_AreConjunctionsAndJuxtaposeClausesWhereTheyJuxtaposeNouns()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);
        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateConjunctions(rand, phonology)).ToList();

        //64A: 15 of 301 languages set both nouns and clauses side by side
        Assert.Contains(drawn, c => c.NounAnd.Value is AndJuxtaposed);
        Assert.All(drawn.Where(c => c.NounAnd.Value is AndJuxtaposed), c => Assert.Null(c.ClauseAnd));
        Assert.All(drawn, c => Assert.All(new[] { c.Or, c.But, c.ClauseAnd }.OfType<Lexeme>(), w => Assert.Equal(PartOfSpeech.Conjunction, w.PartOfSpeech)));
    }

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Birds = BareNoun("bird", Number.Plural);

    private static Rendering Saw(Language l, Argument subject) => Syntax.Realize(l, Declared(new Event(new Doing("see", Birds), subject, [], []), Tense.Past, Aspect.Perfective, false), "x");

    private static string VerbGloss(Rendering sentence) => sentence.Words[IndexOf(sentence, "see")].Gloss;

    [Fact]
    public void Realize_NounsJoinedByAnd_FollowTheLanguagesStrategy()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Saw(l, new Coordination(Conjunction.And, [Dog, Cat]));
            var (dog, cat) = (IndexOf(sentence, "dog"), IndexOf(sentence, "cat"));
            Assert.True(dog < cat);
            switch (l.Conjunctions.NounAnd.Value)
            {
                case AndWord and:
                    Assert.InRange(IndexOf(sentence, and.Word.Root), dog + 1, cat - 1);
                    break;
                case AndWith when l.RoleMarkers[Role.Companion].Adposition is { } with:
                    Assert.True(IndexOf(sentence, with.Lexeme.Root) > dog);
                    break;
                case AndJuxtaposed:
                    Assert.DoesNotContain(sentence.Words, w => w.Word.Root?.Meaning == "and");
                    break;
            }
        });
        Assert.Contains(GeneratedLanguages.Sample, l => l.Conjunctions.NounAnd.Value is AndWith);
    }

    //leaving out languages whose verbs agree in a gender the dog and the cat don't share, where the
    //two together take the resolved gender instead of the dog's
    [Fact]
    public void Realize_TwoNounsJoinedByAnd_AgreeAsADual()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => !l.Genders.VerbsAgree || l.GenderOf(l.Lexicon.Noun("dog")) == l.GenderOf(l.Lexicon.Noun("cat"))), l =>
            Assert.Equal(VerbGloss(Saw(l, BareNoun("dog", Number.Dual))), VerbGloss(Saw(l, new Coordination(Conjunction.And, [Dog, Cat])))));
    }

    //a king and a woman, whose genders differ in every language with masculine and feminine, which assign people by sex.
    //They're new words, so the languages are its own rather than the shared sample's
    [Fact]
    public void Realize_NounsOfDifferentGendersJoinedByAnd_TakeTheMasculine()
    {
        var mixed = Enumerable.Range(0, 60).Select(seed => Generator.Generate(seed, LanguageSpec.Free.Fix(Settings.Genders, NounGenders.MasculineFeminine)))
            .Where(l => l.Genders.VerbsAgree).ToList();
        mixed.ForEach(l => l.Lexicon.Classify(TestNouns.All));

        Assert.NotEmpty(mixed);
        Assert.All(mixed, l =>
        {
            var masculine = new[] { "king", "woman" }.FirstOrDefault(n => l.GenderOf(l.Lexicon.Noun(n)) == Gender.Masculine);
            if (masculine is not null)
            {
                Assert.Equal(VerbGloss(Saw(l, BareNoun(masculine, Number.Dual))), VerbGloss(Saw(l, new Coordination(Conjunction.And, [BareNoun("king", Number.Singular), BareNoun("woman", Number.Singular)]))));
            }
        });
    }

    [Fact]
    public void Realize_YouAndI_AgreeAsWe()
    {
        var youAndMe = new Coordination(Conjunction.And, [new Pronoun(Person.Second, Number.Singular), new Pronoun(Person.First, Number.Singular)]);

        Assert.All(GeneratedLanguages.Sample, l =>
            Assert.Equal(VerbGloss(Saw(l, new Pronoun(Person.First, Number.Dual))), VerbGloss(Saw(l, youAndMe))));
    }

    [Fact]
    public void Realize_NounsJoinedByOr_AgreeWithTheLast()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Saw(l, new Coordination(Conjunction.Or, [Dog, BareNoun("cat", Number.Plural)]));

            Assert.Equal(VerbGloss(Saw(l, BareNoun("cat", Number.Plural))), VerbGloss(sentence));
            Assert.InRange(IndexOf(sentence, l.Conjunctions.Or.Root), IndexOf(sentence, "dog") + 1, IndexOf(sentence, "cat") - 1);
        });
    }

    [Fact]
    public void Realize_ClausesJoined_AreEachAsTheyWouldBeAloneWithTheConjunctionBetween()
    {
        var first = Declared(new Event(new Doing("sleep", null), Dog, [], []), Tense.Past, Aspect.Imperfective, false);
        var second = Declared(new Event(new Doing("sleep", null), Cat, [], []), Tense.Past, Aspect.Imperfective, false);

        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var conjunction in Enum.GetValues<Conjunction>())
            {
                var joined = Syntax.Realize(l, new Coordinated<Sentence>(conjunction, first, second), "x").Words.Select(w => w.Gloss);
                var word = l.Conjunctions.BetweenClauses(conjunction);
                IEnumerable<string> between = word is null ? [] : [word.Root.Meaning];

                Assert.Equal(Syntax.Realize(l, first, "x").Words.Select(w => w.Gloss).Concat(between).Concat(Syntax.Realize(l, second, "x").Words.Select(w => w.Gloss)), joined);
            }
        });
    }

    [Fact]
    public void Realize_AdjectivesSaidTogether_SayEach()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, Declared(new Event(new Being(new AdjectiveCoordination(Conjunction.And, [new("big", false), new("old", false)])), Dog, [], []), Tense.Present, Aspect.Imperfective, false), "x");

            Assert.True(IndexOf(sentence, "big") >= 0 && IndexOf(sentence, "old") > IndexOf(sentence, "big"));
        });
    }

    private static Event Doing(string verb, Argument subject) => new(new Doing(verb, null), subject, [], []);

    [Fact]
    public void Parse_JoinedEvents_ShareTheSubjectAndTense()
    {
        var she = new Pronoun(Person.Third, Number.Singular, Gender: Gender.Feminine);
        var away = new Oblique(Role.Goal, BareNoun("away", Number.Singular), Configuration.At);

        Assert.Equal<Clause>(new Declarative(new Proposition(new Coordinated<StateOfAffairs>(Conjunction.And, Doing("turn", she), Doing("walk", she) with { Obliques = [away] }), Tense.Past)),
            ClauseOf(Parsed("and.PST  subject:3SG.F  [turn]  [walk  goal:away]")));
    }

    [Fact]
    public void Parse_JoinedEvents_KeepSubjectsOfTheirOwnAndOperatorsOfTheirOwn()
    {
        var joined = new Coordinated<StateOfAffairs>(Conjunction.Or, Doing("eat", Dog), new Negated(Doing("sleep", Cat)));

        Assert.Equal<Clause>(new Declarative(new Proposition(joined, Tense.Past)), ClauseOf(Parsed("or.PST  [eat  subject:dog]  [sleep.NEG  subject:cat]")));
    }

    [Fact]
    public void Parse_JoinedEvents_HaveTheAspectOverThemAll()
    {
        var joined = new Coordinated<StateOfAffairs>(Conjunction.And, Doing("eat", Dog), Doing("drink", Dog));

        Assert.Equal<Clause>(new Declarative(new Proposition(new Aspected(Aspect.Perfect, joined), Tense.Past)), ClauseOf(Parsed("and.PST.PRF  subject:dog  [eat]  [drink]")));
    }

    [Fact]
    public void Parse_JoinedCommands_AreToldToYou()
    {
        var you = new Pronoun(Person.Second, Number.Plural);

        Assert.Equal<Clause>(new Directive(new Coordinated<StateOfAffairs>(Conjunction.And, Doing("sit", you), Doing("eat", you))), ClauseOf(Parsed("and.IMP  subject:2PL  [sit]  [eat]")));
    }

    [Fact]
    public void Parse_ThreeJoinedEvents_NestToTheLeft()
    {
        var joined = new Coordinated<StateOfAffairs>(Conjunction.And, new Coordinated<StateOfAffairs>(Conjunction.And, Doing("see", Dog), Doing("eat", Dog)), Doing("sleep", Dog));

        Assert.Equal<Clause>(new Declarative(new Proposition(joined, Tense.Present)), ClauseOf(Parsed("and  subject:dog  [see]  [eat]  [sleep]")));
    }

    [Fact]
    public void Parse_JoinedEventsThenAClause_JoinsTheClauseToThem()
    {
        var coordinated = Assert.IsType<Coordinated<Sentence>>(Stated(Assert.Single(ClauseFile.Parse("x\n  and.PST  subject:dog  [eat]  [drink]\n  but  sleep.PST  subject:cat")).Utterance).Value);

        Assert.Equal(2, ClauseOf(coordinated.First).StateOfAffairs.Events.Count());
    }

    [Theory]
    [InlineData("x\n  and.PST.NEG  subject:dog  [eat]  [drink]", "can't be said yet")]
    [InlineData("x\n  and.ABIL  subject:dog  [eat]  [drink]", "can't be said yet")]
    [InlineData("x\n  and.Q  subject:dog  [eat]  [drink]", "can't be said yet")]
    [InlineData("x\n  and  subject:dog  [eat.PST]  [drink]", "goes on the conjunction")]
    [InlineData("x\n  and  subject:dog  [eat.IMP]  [drink]", "goes on the conjunction")]
    [InlineData("x\n  but  subject:dog  [eat]  [drink]", "but joins only clauses")]
    [InlineData("x\n  and  subject:dog  [eat]", "two events or more")]
    [InlineData("x\n  and  subject:dog  location:house  [eat]  [drink]", "goes in the brackets")]
    [InlineData("x\n  and  subject:dog  [eat  object:what]  [drink]", "can't ask yet")]
    [InlineData("x\n  and  [eat]  [drink]", "needs a subject")]
    [InlineData("x\n  see  subject:1SG  object:(cat relative:[and  [sleep  subject:_]  [eat  subject:_]])", "can't join events yet")]
    public void Parse_BadJoinedEvents_SayWhy(string text, string message) => Assert.Contains(message, Error(text).Message);

    [Fact]
    public void Realize_JoinedEvents_SayASharedSubjectOnce()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Declarative(new Proposition(new Coordinated<StateOfAffairs>(Conjunction.And, Doing("eat", Dog), Doing("sleep", Dog)), Tense.Past)), "x");

            Assert.Single(sentence.Words, w => w.Word.Root?.Meaning == "dog");
            Assert.True(IndexOf(sentence, "eat") >= 0 && IndexOf(sentence, "sleep") > IndexOf(sentence, "eat"));
        });
    }

    [Fact]
    public void Realize_JoinedEventsWithSubjectsOfTheirOwn_AreEachClauseAsItsSaidAlone()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var (eats, sleeps) = (Doing("eat", Dog), Doing("sleep", Cat));
            var joined = Syntax.Realize(l, new Declarative(new Proposition(new Coordinated<StateOfAffairs>(Conjunction.And, eats, sleeps), Tense.Past)), "x").Words.Select(w => w.Gloss);
            IEnumerable<string> between = l.Conjunctions.BetweenClauses(Conjunction.And) is { } word ? [word.Root.Meaning] : [];
            IEnumerable<string> Alone(Event e) => Syntax.Realize(l, new Declarative(new Proposition(e, Tense.Past)), "x").Words.Select(w => w.Gloss);

            Assert.Equal(Alone(eats).Concat(between).Concat(Alone(sleeps)), joined);
        });
    }

    [Fact]
    public void Realize_JoinedCommands_TellEach()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var you = new Pronoun(Person.Second, Number.Singular);
            var sentence = Syntax.Realize(l, new Directive(new Coordinated<StateOfAffairs>(Conjunction.And, Doing("eat", you), Doing("drink", you))), "x");

            Assert.True(IndexOf(sentence, "eat") >= 0 && IndexOf(sentence, "drink") > IndexOf(sentence, "eat"));
        });
    }
}
