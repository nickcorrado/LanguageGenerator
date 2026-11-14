using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class QuestionTests
{
    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Dog = new("dog", Number.Singular);
    private static readonly NounPhrase Fish = new("fish", Number.Singular);
    private static Clause DogEats(Argument food) => new("eat", Tense.Present, Aspect.Imperfective, Dog, food) { Adverbs = [] };
    private static Clause DogEatsFish => DogEats(Fish);

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
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GenerateQuestions(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology, 0.15))];
    }

    [Fact]
    public void GenerateQuestions_ManyDraws_FollowWals116A()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var drawn = Drawn(language, 4000).Select(q => q.Polar.Value).ToList();

        //600 by a particle, 164 by the verb's morphology, 13 by word order and 178 by intonation, of 955
        Assert.InRange(drawn.Count(m => m is QuestionParticle) / 4000.0, 0.60, 0.66);
        Assert.InRange(drawn.Count(m => m is QuestionAffix) / 4000.0, 0.15, 0.19);
        Assert.InRange(drawn.Count(m => m is QuestionInversion) / 4000.0, 0.007, 0.022);
        Assert.InRange(drawn.Count(m => m is QuestionIntonation) / 4000.0, 0.17, 0.21);
    }

    [Theory]
    //58 of 324 SOV languages put question words first, 72 of 290 SVO ones and 46 of 65 VSO ones
    [InlineData(ConstituentWordOrder.SOV, 0.15, 0.21)]
    [InlineData(ConstituentWordOrder.SVO, 0.22, 0.28)]
    [InlineData(ConstituentWordOrder.VSO, 0.67, 0.75)]
    public void GenerateQuestions_ManyDraws_FrontQuestionWordsAsWals93ASaysForTheOrder(ConstituentWordOrder order, double least, double most)
    {
        var language = GeneratedLanguages.Sample.First(l => l.WordOrder.ConstituentWordOrder == order);

        Assert.InRange(Drawn(language, 4000).Count(q => q.FrontsQuestionWords) / 4000.0, least, most);
    }

    [Theory]
    //final in 140 of 200 OV languages with a particle, and initial in 81 of 256 VO ones
    [InlineData(HeadDirectionality.Final, QuestionParticlePosition.Final, 0.66, 0.74)]
    [InlineData(HeadDirectionality.Initial, QuestionParticlePosition.Initial, 0.28, 0.36)]
    public void GenerateQuestions_ManyDraws_PlaceParticlesAsWals92ASaysForTheOrder(HeadDirectionality verbPhrase, QuestionParticlePosition position, double least, double most)
    {
        var language = GeneratedLanguages.Sample.First(l => l.WordOrder.VerbPhraseDirectionality == verbPhrase);
        var particles = Drawn(language, 6000).Select(q => q.Polar.Value).OfType<QuestionParticle>().ToList();

        Assert.InRange(particles.Count(p => p.Position == position) / (double)particles.Count, least, most);
    }

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoQuestionAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.IsNotType<QuestionAffix>(l.Questions.Polar.Value));

    [Fact]
    public void Generate_QuestionWords_AreGlossedByWhatTheyAsk() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            Assert.Equal(["who", "what", "where"], l.Questions.Words.Values.Select(w => w.Root.Meaning));
            Assert.Equal("why", l.Questions.Why.Root.Meaning);
        });

    [Fact]
    public void Realize_YesNoQuestion_IsMarkedAsTheLanguageMarksIt() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var statement = Realized(l, DogEatsFish);
            var question = Realized(l, new Question(DogEatsFish, false));
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
        var languages = GeneratedLanguages.Sample.Where(l => l.Questions.Polar.Value is QuestionAffix && l.Modals.Markers[Modality.Ability].Value is ModalVerb).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Question(new Modalized(DogEatsFish, Modality.Ability, false), false));
            var affix = (QuestionAffix)l.Questions.Polar.Value!;
            var morphemes = sentence.Words[MarkerAt(sentence)].Word.Morphemes;

            Assert.Equal("can", sentence.Words[MarkerAt(sentence)].Word.Root?.Meaning);
            Assert.Equal(affix.Marker, (affix.Position == ExponentPosition.Prefix ? morphemes[0] : morphemes[^1]).Value);
        });
    }

    [Fact]
    public void Realize_ContentQuestion_FrontsTheQuestionWordOrLeavesItInPlace() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var what = l.Questions.Words[Interrogative.What].Root;
            var question = Realized(l, new Question(DogEats(new QuestionWord(Interrogative.What)), false));
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
            var sentence = Realized(l, new Question(clause, false));
            var adposition = l.RoleMarkers[Role.Source].Adposition!;
            var where = IndexOf(sentence, l.Questions.Words[Interrogative.Where].Root);
            var from = IndexOf(sentence, adposition.Lexeme.Root);

            //the phrase can have a case particle as well, as in Hindi kahāṃ se 'where from'
            Assert.InRange(where, 0, 2);
            Assert.InRange(from, 0, 2);
            Assert.Equal(adposition.Type == AdpositionType.Preposition, from < where);
        });
    }

    [Fact]
    public void Realize_Why_GoesFirstWhereQuestionWordsDo() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Questions.FrontsQuestionWords), l =>
            Assert.Equal(0, IndexOf(Realized(l, new Question(DogEatsFish, true)), l.Questions.Why.Root)));

    [Fact]
    public void Realize_WhyInPlace_GoesWhereObliquesGo()
    {
        var languages = GeneratedLanguages.Sample.Where(l => !l.Questions.FrontsQuestionWords).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var house = new NounPhrase("house", Number.Singular);
            var withOblique = Realized(l, DogEatsFish with { Obliques = [new Oblique(Role.Location, house, Configuration.At)] });
            var why = Realized(l, new Question(DogEatsFish, true));

            //where the oblique's phrase starts, before any adposition, case particle or article
            var oblique = IndexOf(withOblique, l.Lexicon.Noun("house").Root);
            Assert.InRange(IndexOf(why, l.Questions.Why.Root), oblique - 3, oblique);
        });
    }

    //none of the shared sample both inverts and fronts, so these seeds were found for it
    [Theory]
    [InlineData(1316)]
    [InlineData(1430)]
    public void Realize_InvertingLanguage_PutsTheVerbAfterAFrontedQuestionWord(int seed)
    {
        var l = Generator.Generate(seed);
        Assert.True(l.Questions is { Polar.Value: QuestionInversion, FrontsQuestionWords: true });

        var sentence = Realized(l, new Question(DogEats(new QuestionWord(Interrogative.What)), false));
        var what = IndexOf(sentence, l.Questions.Words[Interrogative.What].Root);

        Assert.Equal(what + 1, IndexOf(sentence, l.Lexicon.Verb("eat").Root));
    }

    [Fact]
    public void Realize_EveryKindOfClause_CanBeAsked()
    {
        var who = new QuestionWord(Interrogative.Who);
        var house = new NounPhrase("house", Number.Singular);
        var clauses = new Statement[]
        {
            new Question(DogEatsFish, false),
            new Question(new Clause("eat", Tense.Past, Aspect.Perfective, who, Fish) { Adverbs = [] }, false),
            new Question(new PassiveClause("eat", Tense.Past, Aspect.Perfective, Fish, who, false, []) { Adverbs = [] }, false),
            new Question(new PassiveClause("eat", Tense.Past, Aspect.Perfective, Fish, null, false, []) { Adverbs = [] }, true),
            new Question(new CopularClause(new Adjective("big", false), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] }, false),
            new Question(new CopularClause(new Oblique(Role.Location, new QuestionWord(Interrogative.Where), Configuration.At), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] }, false),
            new Question(new CopularClause(new Oblique(Role.Location, house, Configuration.At), Tense.Present, Aspect.Imperfective, who) { Obliques = [], Adverbs = [] }, false),
            new Question(new Comparative(new CopularClause(new Adjective("big", false), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] }, Fish), false),
            new Question(new Comparative(new CopularClause(new Adjective("big", false), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] }, Fish), true),
            new Question(new Modalized(DogEatsFish, Modality.Obligation, true), true),
            new Addressed(new Question(DogEatsFish, false), Dog),
        };
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var clause in clauses)
            {
                Assert.NotEmpty(Realized(l, clause).Words);
            }
        });
    }

    [Theory]
    [InlineData("see.PST.Q  subject:2SG  object:3PL", null, false)]
    [InlineData("want  subject:3PL  object:what", Interrogative.What, false)]
    [InlineData("tell.PST  subject:who  object:2SG", Interrogative.Who, false)]
    [InlineData("get.PST  subject:2SG  object:3SG.N  source:where", Interrogative.Where, false)]
    [InlineData("send.PST.NEG  subject:2SG  why  object:(soldier.PL.DEF)", null, true)]
    public void Parse_Question_AsksWhatItsWordsSay(string gloss, Interrogative? word, bool why)
    {
        var question = Assert.IsType<Question>(Parsed(gloss).Value);
        var clause = Assert.IsType<Clause>(question.Statement.Value);
        Argument[] arguments = [clause.Subject, .. clause.Object.HasValue ? [clause.Object.GetValueOrDefault()] : Array.Empty<Argument>(), .. clause.Obliques.Select(o => o.Argument)];

        Assert.Equal(why, question.Why);
        Assert.Equal(word, arguments.Select(a => a.Value).OfType<QuestionWord>().SingleOrDefault()?.Word);
    }

    [Fact]
    public void Parse_QuestionWithAModalAndAnAddressee_NestsThemInThatOrder()
    {
        var addressed = Assert.IsType<Addressed>(Parsed("swim.ABIL.Q  subject:2SG  addressee:marta").Value);

        Assert.IsType<Modalized>(Assert.IsType<Question>(addressed.Statement.Value).Statement.Value);
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
