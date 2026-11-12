using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ModalTests
{
    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Dog = new("dog", Number.Singular);
    private static readonly NounPhrase Fish = new("fish", Number.Singular);
    private static Clause DogEatsFish(bool negative) => new("eat", Tense.Present, Aspect.Imperfective, Dog, Fish, negative) { Adverbs = [] };

    private static Sentence Realized(Language language, Statement statement) => Syntax.Realize(language, statement, "x");

    //where the modal's marker is in a sentence: the modal verb's word, or the word with its affix or particle
    private static int ModalAt(Sentence sentence) => sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value switch
    {
        Root r => r.Meaning is "can" or "may" or "must" or "should",
        Affix a => a.Grammemes.Any(g => g is Modality),
        Particle p => p.Grammemes.Any(g => g is Modality),
    }));

    private static bool IsNegated(Word word) => word.Morphemes.Any(m => m.Value is Affix { Grammemes: var g } && g.Contains(Polarity.Negative));

    private static IReadOnlyList<Modals> Drawn(Language language, int count)
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GenerateModals(rand, language.MorphologicalProfile, language.WordOrder, language.Predication, language.Phonology, 0.15))];
    }

    [Fact]
    public void GenerateModals_ManyDraws_FollowWals74A()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var drawn = Drawn(language, 4000).Select(m => m.Markers[Modality.Ability].Value).ToList();

        //63 by an affix, 158 by a verb and 13 otherwise, of 234
        Assert.InRange(drawn.Count(m => m is ModalAffix) / 4000.0, 0.25, 0.29);
        Assert.InRange(drawn.Count(m => m is ModalVerb) / 4000.0, 0.65, 0.70);
        Assert.InRange(drawn.Count(m => m is ModalParticle) / 4000.0, 0.045, 0.07);
    }

    [Theory]
    //ability and permission joined 60% of the time, and obligation and advisability 50%, and
    //rarely the same by chance otherwise
    [InlineData("Ability", "Permission", 0.57, 0.65)]
    [InlineData("Obligation", "Advisability", 0.47, 0.55)]
    public void GenerateModals_ManyDraws_ShareMarkersAsTheMapSays(string first, string second, double least, double most)
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var (a, b) = (ModalityMap.Modalities.Single(m => m.Name == first), ModalityMap.Modalities.Single(m => m.Name == second));
        static IReadOnlyList<Phoneme> Form(ModalMarking marking) => marking.Value switch
        {
            ModalAffix a => a.Marker.Form,
            ModalParticle p => p.Marker.Form,
            ModalVerb v => v.Verb.Root.Form,
        };
        var shared = Drawn(language, 4000).Count(m => Form(m.Markers[a]).SequenceEqual(Form(m.Markers[b]))
            && m.Markers[a].Value.GetType() == m.Markers[b].Value.GetType());

        Assert.InRange(shared / 4000.0, least, most);
    }

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoModalAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.DoesNotContain(l.Modals.Markers.Values, m => m.Value is ModalAffix));

    [Fact]
    public void Generate_ModalVerbs_AreGlossedByWhatTheyMeanInEachUse() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var (modality, marking) in l.Modals.Markers)
            {
                if (marking.Value is ModalVerb verb)
                {
                    Assert.Equal(ModalityMap.VerbGloss(modality), verb.Verb.Root.Meaning);
                }
            }
        });

    [Fact]
    public void Realize_EveryModalInEveryLanguage_SaysIt()
    {
        var clauses = new Statement[]
        {
            DogEatsFish(false),
            new PassiveClause("eat", Tense.Past, Aspect.Perfective, Fish, Dog, false, []) { Adverbs = [] },
            new CopularClause(new Adjective("big", false), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] },
            new CopularClause(new Oblique(Role.Location, new NounPhrase("house", Number.Singular)), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] },
        };
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var modality in ModalityMap.Modalities)
            {
                foreach (var clause in clauses)
                {
                    Assert.True(ModalAt(Realized(l, new Modalized(clause, modality, false))) >= 0);
                }
            }
        });
    }

    [Fact]
    public void Realize_ModalVerb_GoesBeforeTheVerbInVOAndAfterItInOV()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[Modality.Obligation].Value is ModalVerb).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Modalized(DogEatsFish(false), Modality.Obligation, false));
            var before = ModalAt(sentence) < IndexOf(sentence, l.Lexicon.Verb("eat").Root);
            Assert.Equal(l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial, before);
        });
    }

    [Fact]
    public void Realize_ModalVerb_TakesTheTenseAndLeavesTheVerbBare()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[Modality.Ability].Value is ModalVerb).ToList();
        Assert.All(languages, l =>
        {
            var clause = DogEatsFish(false) with { Tense = Tense.Past };
            var sentence = Realized(l, new Modalized(clause, Modality.Ability, false));
            var plain = Realized(l, clause);
            var eat = l.Lexicon.Verb("eat").Root;

            Assert.Equal([eat], sentence.Words[IndexOf(sentence, eat)].Word.Morphemes.Select(m => m.Value as Root));
            //the modal verb is inflected just as the plain verb was
            Assert.Equal(plain.Words.Count, sentence.Words.Count - 1);
            Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss.Replace("eat", "can"), sentence.Words[ModalAt(sentence)].Gloss);
        });
    }

    [Fact]
    public void Realize_ModalAffix_GoesNextToTheRootOutsideThePassive()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[Modality.Ability].Value is ModalAffix && l.Passive is { Marker.Value: Affix }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Modalized(new PassiveClause("eat", Tense.Past, Aspect.Perfective, Fish, null, false, []) { Adverbs = [] }, Modality.Ability, false));
            var morphemes = sentence.Words[ModalAt(sentence)].Word.Morphemes.Select(m => m.Value).ToList();
            var root = morphemes.FindIndex(m => m is Root);
            var prefix = ((ModalAffix)l.Modals.Markers[Modality.Ability].Value!).Position == ExponentPosition.Prefix;
            var passivePrefix = l.Passive!.Position == ExponentPosition.Prefix;

            var modal = morphemes.FindIndex(m => m is Affix { Grammemes: [Modality] });
            Assert.Equal(prefix ? root - (passivePrefix ? 2 : 1) : root + (passivePrefix ? 1 : 2), modal);
        });
    }

    [Fact]
    public void Realize_NegatedModalVerb_NegatesTheModalAndNotTheVerb()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[Modality.Ability].Value is ModalVerb
            && l.Negation.Strategy is NegationStrategy.Prefix or NegationStrategy.Suffix).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var eat = l.Lexicon.Verb("eat").Root;
            var cannot = Realized(l, new Modalized(DogEatsFish(false), Modality.Ability, true));
            var mustNot = Realized(l, new Modalized(DogEatsFish(true), Modality.Ability, false));

            Assert.True(IsNegated(cannot.Words[ModalAt(cannot)].Word));
            Assert.False(IsNegated(cannot.Words[IndexOf(cannot, eat)].Word));
            Assert.False(IsNegated(mustNot.Words[ModalAt(mustNot)].Word));
            Assert.True(IsNegated(mustNot.Words[IndexOf(mustNot, eat)].Word));
        });
    }

    [Fact]
    public void Realize_CopulaWithAModal_IsNeverDropped()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Predication is { ZeroCopula: true, Adjectives: PredicativeAdjectives.Nonverbal }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var big = new CopularClause(new Adjective("big", false), Tense.Present, Aspect.Imperfective, Dog) { Obliques = [], Adverbs = [] };
            var sentence = Realized(l, new Modalized(big, Modality.Obligation, false));

            Assert.True(IndexOf(sentence, l.Predication.Copula.Root) >= 0);
        });
    }

    [Fact]
    public void Realize_ModalClauseUnderASuffix_PutsTheSuffixOnTheModalVerb()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[Modality.Ability].Value is ModalVerb
            && l.Subordination.Markers[Subordinator.If] is { Position: ExponentPosition.Suffix, Balance: Balance.Balanced }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var main = new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("cat", Number.Singular)) { Adverbs = [] };
            var sentence = Realized(l, new Subordinated(main, Subordinator.If, new Modalized(DogEatsFish(false), Modality.Ability, false), false));
            var marker = l.Subordination.Markers[Subordinator.If].Marker;

            Assert.Contains(marker, sentence.Words[ModalAt(sentence)].Word.Morphemes);
        });
    }

    [Theory]
    [InlineData("swim.ABIL.NEG  subject:2SG", true, false)]
    [InlineData("sing.NEG.OBLIG  subject:2SG", false, true)]
    [InlineData("find.OBLIG  subject:2SG  object:key.DEF", false, false)]
    [InlineData("follow.NEG.ADVS  subject:2SG  object:3PL", false, true)]
    public void Parse_Modal_NegatesWhatTheNegationFollows(string gloss, bool modalNegative, bool verbNegative)
    {
        var modalized = Assert.IsType<Modalized>(Parsed(gloss).Value);

        Assert.Equal(modalNegative, modalized.Negative);
        Assert.Equal(verbNegative, Assert.IsType<Clause>(modalized.Clause.Value).Negative);
    }

    [Fact]
    public void Parse_ModalWithBe_IsAModalCopularClause()
    {
        var modalized = Assert.IsType<Modalized>(Parsed("be.OBLIG  subject:2SG  adjective:careful").Value);

        Assert.Equal(Modality.Obligation, modalized.Modality);
        Assert.IsType<CopularClause>(modalized.Clause.Value);
    }

    [Fact]
    public void Parse_ModalPassiveWithAnAddressee_KeepsTheAddresseeOutside()
    {
        var addressed = Assert.IsType<Addressed>(Parsed("carry.PASS.PERM  subject:box.DEF  addressee:marta").Value);

        Assert.IsType<PassiveClause>(Assert.IsType<Modalized>(addressed.Statement.Value).Clause.Value);
    }

    [Theory]
    [InlineData("go.IMP.ABIL", "A command can't take ABIL")]
    [InlineData("be.OBLIG  subject:marta  adjective:tall.CMPR  standard:man.PL", "A comparison can't take OBLIG")]
    [InlineData("go.ABIL.OBLIG  subject:2SG", "more than one modality")]
    public void Parse_BadModals_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);
}
