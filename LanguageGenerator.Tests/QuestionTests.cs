using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class QuestionTests
{
    private static Statement Parsed(string gloss) => Stated(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance);

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static Clause DogEats(Argument food) => DogEatsFish with { Predicate = new Doing("eat", food) };

    private static Sentence Realized(Language language, Statement statement) => Syntax.Realize(language, statement, "x");

    //where a yes-no question's marker is: the word with its particle or affix
    private static int MarkerAt(Sentence sentence) => sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value switch
    {
        Root => false,
        Affix a => a.Grammemes.Contains(Mood.Interrogative),
        Particle p => p.Grammemes.Contains(Mood.Interrogative),
    }));

    private static IReadOnlyList<Questions> Drawn(Language language, int count)
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GenerateQuestions(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology))];
    }

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoQuestionAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.IsNotType<QuestionAffix>(l.Questions.Polar.Value));

    [Fact]
    public void Generate_QuestionWords_AreGlossedByWhatTheyAsk() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Assert.Equal(["who", "what", "where", "when"], l.Questions.Words.Values.Select(w => w.Root.Meaning));
            Assert.Equal(["why", "how"], l.Questions.Adverbs.Values.Select(w => w.Root.Meaning));
        });

    [Fact]
    public void Realize_YesNoQuestion_IsMarkedAsTheLanguageMarksIt() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var statement = Realized(l, DogEatsFish);
            var question = Realized(l, new Question(DogEatsFish, null));
            var eat = l.Lexicon.Verb("eat").Root;
            switch (l.Questions.Polar.Value)
            {
                case QuestionParticle p:
                    Assert.Equal(statement.Words.Count + 1, question.Words.Count);
                    Assert.Equal(p.Position switch
                    {
                        QuestionParticlePosition.Initial => 0,
                        QuestionParticlePosition.Final => question.Words.Count - 1,
                        _ => 1,
                    }, MarkerAt(question));
                    break;
                case QuestionAffix:
                    Assert.Equal(IndexOf(question, eat), MarkerAt(question));
                    break;
                case QuestionInversion:
                    Assert.Equal(0, IndexOf(question, eat));
                    break;
                default:
                    Assert.Equal(statement.Text, question.Text);
                    break;
            }
        });

    [Fact]
    public void Realize_QuestionAffix_GoesOutsideEverythingOnTheModalVerb()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Questions.Polar.Value is QuestionAffix && l.Modals.Markers[SituationalModality.Ability].Value is ModalVerb).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Question(new Modalized(DogEatsFish, SituationalModality.Ability, false), null));
            var affix = (QuestionAffix)l.Questions.Polar.Value!;
            var morphemes = sentence.Words[MarkerAt(sentence)].Word.Morphemes;

            Assert.Equal("can", sentence.Words[MarkerAt(sentence)].Word.Root?.Meaning);
            Assert.Equal(affix.Marker.Affix, (affix.Marker.Side == Side.Before ? morphemes[0] : morphemes[^1]).Value);
        });
    }

    [Fact]
    public void Realize_ContentQuestion_FrontsTheQuestionWordOrLeavesItInPlace() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var what = l.Questions.Words[Interrogative.What].Root;
            var question = Realized(l, new Question(DogEats(new QuestionWord(Interrogative.What)), null));
            var statement = Realized(l, DogEatsFish);

            Assert.Equal(-1, MarkerAt(question));
            if (l.Questions.FrontsQuestionWords)
            {
                //the object's case and number are on the word itself or particles before it, if anywhere
                Assert.InRange(IndexOf(question, what), 0, l.Paradigms.Nouns.Slots.Count(s => s.Position == ExponentPosition.PrecedingParticle));
            }
            else
            {
                Assert.Equal(IndexOf(statement, l.Lexicon.Noun("fish").Root), IndexOf(question, what));
            }
        });

    [Fact]
    public void Realize_FrontedQuestionWord_TakesItsAdpositionWithIt()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Questions.FrontsQuestionWords && l.RoleMarkers[Role.Source].Adposition is not null).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var clause = DogEatsFish with { Obliques = [new Oblique(Role.Source, new QuestionWord(Interrogative.Where), Configuration.At)] };
            var sentence = Realized(l, new Question(clause, null));
            var adposition = l.RoleMarkers[Role.Source].Adposition!;
            var where = IndexOf(sentence, l.Questions.Words[Interrogative.Where].Root);
            var from = IndexOf(sentence, adposition.Lexeme.Root);

            //the phrase can have a case particle as well, as in Hindi kahāṃ se 'where from'
            Assert.InRange(where, 0, 2);
            Assert.InRange(from, 0, 2);
            Assert.Equal(adposition.Type == AdpositionType.Preposition, from < where);
        });
    }

    [Theory]
    [InlineData(QuestionAdverb.Why)]
    [InlineData(QuestionAdverb.How)]
    public void Realize_WhyOrHow_GoesFirstWhereQuestionWordsDo(QuestionAdverb adverb) =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Questions.FrontsQuestionWords), l =>
            Assert.Equal(0, IndexOf(Realized(l, new Question(DogEatsFish, adverb)), l.Questions.Adverbs[adverb].Root)));

    [Fact]
    public void Realize_When_IsMarkedAsATimeIs()
    {
        Assert.All(GeneratedLanguages.Sample.Take(100), l =>
        {
            Clause At(Argument time) => DogEatsFish with { Obliques = [new Oblique(Role.Time, time, Configuration.At)] };
            var night = Realized(l, At(BareNoun("sun", Number.Singular)));
            var when = Realized(l, new Question(At(new QuestionWord(Interrogative.When)), null));
            var word = l.Questions.Words[Interrogative.When].Root;

            //'when' takes the time's case or adposition as any noun there would, so it says as many words
            Assert.Equal(night.Words.Count, when.Words.Count);
            Assert.Equal(night.Words[IndexOf(night, "sun")].Gloss.Replace("sun", "X"), when.Words[IndexOf(when, word)].Gloss.Replace("when", "X"));
            //fronted, it brings its whole phrase, as many words of adposition, case and article as the time has before it
            if (l.Questions.FrontsQuestionWords)
            {
                var before = IndexOf(when, word);
                var sun = IndexOf(night, "sun");
                Assert.Equal(night.Words.Skip(sun - before).Take(before).Select(w => w.Gloss), when.Words.Take(before).Select(w => w.Gloss));
            }
        });
    }

    [Fact]
    public void Realize_WhyInPlace_GoesWhereObliquesGo()
    {
        var languages = GeneratedLanguages.Sample.Where(l => !l.Questions.FrontsQuestionWords).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var house = BareNoun("house", Number.Singular);
            var withOblique = Realized(l, DogEatsFish with { Obliques = [new Oblique(Role.Location, house, Configuration.At)] });
            var why = Realized(l, new Question(DogEatsFish, QuestionAdverb.Why));

            //where the oblique's phrase starts, before any adposition, case particle or article
            var oblique = IndexOf(withOblique, l.Lexicon.Noun("house").Root);
            Assert.InRange(IndexOf(why, l.Questions.Adverbs[QuestionAdverb.Why].Root), oblique - 3, oblique);
        });
    }

    //none of the shared sample both inverts and fronts, so these seeds were found for it
    [Theory]
    [InlineData(1316)]
    [InlineData(1430)]
    public void Realize_InvertingLanguage_PutsTheVerbAfterAFrontedQuestionWord(int seed)
    {
        var l = Generator.Generate(seed, LanguageSpec.Free);
        Assert.True(l.Questions is { Polar.Value: QuestionInversion, FrontsQuestionWords: true });

        var sentence = Realized(l, new Question(DogEats(new QuestionWord(Interrogative.What)), null));
        var what = IndexOf(sentence, l.Questions.Words[Interrogative.What].Root);

        Assert.Equal(what + 1, IndexOf(sentence, l.Lexicon.Verb("eat").Root));
    }

    [Fact]
    public void Realize_EveryKindOfClause_CanBeAsked()
    {
        var who = new QuestionWord(Interrogative.Who);
        var house = BareNoun("house", Number.Singular);
        var clauses = new Statement[]
        {
            new Question(DogEatsFish, null),
            new Question(new Clause(new Doing("eat", Fish), new Declarative(Tense.Past, Aspect.Perfective, who), false, [], []), null),
            new Question(new Clause(new Done("eat", who), new Declarative(Tense.Past, Aspect.Perfective, Fish), false, [], []), null),
            new Question(new Clause(new Done("eat", null), new Declarative(Tense.Past, Aspect.Perfective, Fish), false, [], []), QuestionAdverb.Why),
            new Question(new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []), null),
            new Question(new Clause(new Being(new Oblique(Role.Location, new QuestionWord(Interrogative.Where), Configuration.At)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []), null),
            new Question(new Clause(new Being(new Oblique(Role.Location, house, Configuration.At)), new Declarative(Tense.Present, Aspect.Imperfective, who), false, [], []), null),
            new Question(new Comparative(new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []), Fish), null),
            new Question(new Comparative(new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []), Fish), QuestionAdverb.Why),
            new Question(new Modalized(DogEatsFish, SituationalModality.Obligation, true), QuestionAdverb.Why),
        };
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var clause in clauses)
            {
                Assert.NotEmpty(Realized(l, clause).Words);
            }
            Assert.NotEmpty(Syntax.Realize(l, new Message((Statement)new Question(DogEatsFish, null), Dog), "x").Words);
        });
    }

    [Theory]
    [InlineData("see.PST.Q  subject:2SG  object:3PL", null, null)]
    [InlineData("want  subject:3PL  object:what", Interrogative.What, null)]
    [InlineData("tell.PST  subject:who  object:2SG", Interrogative.Who, null)]
    [InlineData("get.PST  subject:2SG  object:3SG.N  source:where", Interrogative.Where, null)]
    [InlineData("arrive.PST  subject:3PL  time:when", Interrogative.When, null)]
    [InlineData("send.PST.NEG  subject:2SG  why  object:(soldier.PL.DEF)", null, QuestionAdverb.Why)]
    [InlineData("fix.PST  subject:2SG  object:3SG.N  how", null, QuestionAdverb.How)]
    public void Parse_Question_AsksWhatItsWordsSay(string gloss, Interrogative? word, QuestionAdverb? adverb)
    {
        var question = Assert.IsType<Question>(Parsed(gloss).Value);
        var clause = Assert.IsType<Clause>(question.Statement.Value);
        Argument[] arguments = [clause.Force.Subject, .. PredicateOf<Doing>(clause).Object.HasValue ? [PredicateOf<Doing>(clause).Object.GetValueOrDefault()] : Array.Empty<Argument>(), .. clause.Obliques.Select(o => o.Argument)];

        Assert.Equal(adverb, question.Adverb);
        Assert.Equal(word, arguments.Select(a => a.Value).OfType<QuestionWord>().SingleOrDefault()?.Word);
    }

    [Fact]
    public void Parse_QuestionWithAModalAndAnAddressee_NestsThemInThatOrder()
    {
        var addressed = Addressed(Line("swim.ABIL.Q  subject:2SG  addressee:marta"));

        Assert.IsType<Modalized>(Assert.IsType<Question>(StatementOf(addressed).Value).Statement.Value);
    }

    [Theory]
    [InlineData("see.PST  subject:where", "where asks about a place")]
    [InlineData("see.PST  subject:who  object:what", "only ask one thing")]
    [InlineData("leave.PST  subject:who  why", "only ask one thing")]
    [InlineData("see.PST.Q  subject:who", "needs no Q")]
    [InlineData("go.IMP.Q", "more than one mood")]
    [InlineData("eat.IMP  object:what", "A command can't be a question")]
    [InlineData("leave.PST  subject:3SG  reason:why", "'reason' isn't a role")]
    [InlineData("leave.PST  subject:why", "written on its own")]
    [InlineData("leave.PST  subject:how", "written on its own")]
    [InlineData("leave.PST  subject:3SG  when:how", "written on its own")]
    [InlineData("leave.PST  subject:3SG  why  how", "only ask one thing")]
    [InlineData("see.PST  subject:when", "when asks about a time")]
    [InlineData("leave.PST  subject:3SG  when:when", "time:when")]
    [InlineData("leave.PST  subject:3SG  time:what", "is time:when")]
    [InlineData("leave.PST  subject:3SG  duration:what", "for how long")]
    [InlineData("leave.PST  subject:who.PL", "a question word takes no glosses")]
    [InlineData("see.PST  subject:2SG  object:(house possessor:who)", "whose")]
    [InlineData("see.PST  subject:(and who 2SG)", "can't be joined")]
    [InlineData("be  subject:2SG  noun:who", "can't ask what its subject is")]
    [InlineData("be  subject:marta  adjective:tall.CMPR  standard:who", "standard")]
    [InlineData("be  subject:2SG  adjective:what", "isn't an adjective")]
    public void Parse_BadQuestions_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);

    [Theory]
    [InlineData("x\n  sleep subject:dog\n  if see.PST.Q subject:2SG", "can't be a question")]
    [InlineData("x\n  see.PST.Q subject:2SG\n  and sleep subject:dog", "can't be joined")]
    public void Parse_QuestionJoinedToAnotherClause_IsAnError(string text, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text)).Message);
}
