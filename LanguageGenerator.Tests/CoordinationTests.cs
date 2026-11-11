using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class CoordinationTests
{
    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static Argument SubjectOf(Statement statement) => Assert.IsType<Clause>(statement.Value).Subject;

    [Fact]
    public void Parse_NounsJoined_AreACoordinationInOrder()
    {
        var subject = Assert.IsType<Coordination>(SubjectOf(Parsed("sleep  subject:(and dog.DEF cat.DEF)")).Value);

        Assert.Equal(Conjunction.And, subject.Conjunction);
        Assert.Equal(["dog", "cat"], subject.Items.Select(i => Assert.IsType<NounPhrase>(i.Value).Noun));
    }

    [Fact]
    public void Parse_Conjuncts_CanBeAnythingARoleTakes()
    {
        var @object = Assert.IsType<Clause>(Parsed("carry  subject:3SG.F  object:(or (purse.NDF empty) 1SG (and dog cat))").Value).Object!.Value;
        var items = Assert.IsType<Coordination>(@object.Value).Items;

        Assert.Equal(["empty"], Assert.IsType<NounPhrase>(items[0].Value).Adjectives.Select(a => a.Meaning));
        Assert.Equal(new Pronoun(Person.First, Number.Singular), items[1].Value);
        Assert.Equal(Conjunction.And, Assert.IsType<Coordination>(items[2].Value).Conjunction);
    }

    [Fact]
    public void Parse_AdjectivesJoined_AreSaidTogether()
    {
        var clause = Assert.IsType<CopularClause>(Parsed("be  subject:inn.DEF  adjective:(and warm crowded)").Value);

        Assert.Equal(new AdjectiveCoordination(Conjunction.And, [new("warm"), new("crowded")]), clause.Complement.Value);
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

        var outer = Assert.IsType<Coordinated>(entry.Statement.Value);
        Assert.Equal(Conjunction.But, outer.Conjunction);
        var inner = Assert.IsType<Coordinated>(outer.First.Value);
        Assert.Equal(("sit", "argue"), (Assert.IsType<Clause>(inner.First.Value).Verb, Assert.IsType<Clause>(inner.Second.Value).Verb));
        Assert.Equal("sleep", Assert.IsType<Clause>(outer.Second.Value).Verb);
    }

    [Fact]
    public void Parse_EntriesAfterAJoinedOne_StartAfresh()
    {
        var entries = ClauseFile.Parse("one\n  sit  subject:dog\n  and  sleep  subject:dog\ntwo\n  sleep  subject:cat");

        Assert.Equal(2, entries.Count);
        Assert.IsType<Clause>(entries[1].Statement.Value);
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
    public void GenerateConjunctions_ManyDraws_MatchWals63AAnd64A()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);
        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GenerateConjunctions(rand, phonology, 0.15)).ToList();

        //63A: 'and' is 'with' in 103 of 234 languages
        Assert.InRange(drawn.Count(c => c.NounAnd.Value is AndWith) / 4000.0, 0.41, 0.47);
        //64A, among the rest: 161 of 301 join nouns and clauses alike, and 15 set both side by side
        var withWords = drawn.Where(c => c.NounAnd.Value is not AndWith).ToList();
        Assert.InRange(withWords.Count(c => c.NounAnd.Value is AndWord w && w.Word == c.ClauseAnd) / (double)withWords.Count, 0.50, 0.57);
        Assert.InRange(withWords.Count(c => c.NounAnd.Value is AndJuxtaposed) / (double)withWords.Count, 0.03, 0.07);
        Assert.All(drawn.Where(c => c.NounAnd.Value is AndJuxtaposed), c => Assert.Null(c.ClauseAnd));
        Assert.All(drawn, c => Assert.All(new[] { c.Or, c.But, c.ClauseAnd }.OfType<Lexeme>(), w => Assert.Equal(PartOfSpeech.Conjunction, w.PartOfSpeech)));
    }

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Dog = new("dog", Number.Singular);
    private static readonly NounPhrase Cat = new("cat", Number.Singular);
    private static readonly NounPhrase Birds = new("bird", Number.Plural);

    private static Sentence Saw(Language l, Argument subject) => Syntax.Realize(l, new Clause("see", Tense.Past, Aspect.Perfective, subject, Birds), "x");

    private static string VerbGloss(Sentence sentence) => sentence.Words[IndexOf(sentence, "see")].Gloss;

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
                case AndWith when l.RoleMarkers[Role.Companion].Value is Adposition with:
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
            Assert.Equal(VerbGloss(Saw(l, new NounPhrase("dog", Number.Dual))), VerbGloss(Saw(l, new Coordination(Conjunction.And, [Dog, Cat])))));
    }

    [Fact]
    public void Realize_NounsOfDifferentGendersJoinedByAnd_TakeTheMasculine()
    {
        var mixed = GeneratedLanguages.Sample.Where(l => l.Genders.VerbsAgree && l.Genders.Values.Contains(Gender.Masculine)
            && new[] { "dog", "cat" }.Select(n => l.GenderOf(l.Lexicon.Noun(n))).Distinct().Count() == 2).ToList();

        Assert.NotEmpty(mixed);
        Assert.All(mixed, l =>
        {
            var masculine = new[] { "dog", "cat" }.FirstOrDefault(n => l.GenderOf(l.Lexicon.Noun(n)) == Gender.Masculine);
            if (masculine is not null)
            {
                Assert.Equal(VerbGloss(Saw(l, new NounPhrase(masculine, Number.Dual))), VerbGloss(Saw(l, new Coordination(Conjunction.And, [Dog, Cat]))));
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
            var sentence = Saw(l, new Coordination(Conjunction.Or, [Dog, new NounPhrase("cat", Number.Plural)]));

            Assert.Equal(VerbGloss(Saw(l, new NounPhrase("cat", Number.Plural))), VerbGloss(sentence));
            Assert.InRange(IndexOf(sentence, l.Conjunctions.Or.Root), IndexOf(sentence, "dog") + 1, IndexOf(sentence, "cat") - 1);
        });
    }

    [Fact]
    public void Realize_ClausesJoined_AreEachAsTheyWouldBeAloneWithTheConjunctionBetween()
    {
        var first = new Clause("sleep", Tense.Past, Aspect.Imperfective, Dog);
        var second = new Clause("sleep", Tense.Past, Aspect.Imperfective, Cat);

        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var conjunction in Enum.GetValues<Conjunction>())
            {
                var joined = Syntax.Realize(l, new Coordinated(conjunction, first, second), "x").Words.Select(w => w.Gloss);
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
            var sentence = Syntax.Realize(l, new CopularClause(new AdjectiveCoordination(Conjunction.And, [new("big"), new("old")]), Tense.Present, Aspect.Imperfective, Dog), "x");

            Assert.True(IndexOf(sentence, "big") >= 0 && IndexOf(sentence, "old") > IndexOf(sentence, "big"));
        });
    }
}
