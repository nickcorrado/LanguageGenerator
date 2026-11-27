using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ModalTests
{
    private static Sentence Parsed(string gloss) => Stated(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance);

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons

    private static Rendering Realized(Language language, Sentence statement) => Syntax.Realize(language, statement, "x");

    //where the modal's marker is in a sentence: the modal verb's word, or the word with its affix or particle
    private static int ModalAt(Rendering sentence) => sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value switch
    {
        Root r => r.Meaning is "can" or "may" or "must" or "should" or "might",
        Affix a => a.Grammemes.Any(g => g is Modality),
        Particle p => p.Grammemes.Any(g => g is Modality),
    }));

    private static bool IsNegated(Word word) => word.Morphemes.Any(m => m.Value is Affix { Grammemes: var g } && g.Contains(Polarity.Negative));

    private static IReadOnlyList<Modals> Drawn(Language language, int count)
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GenerateModals(rand, language.MorphologicalProfile, language.WordOrder, language.Predication, language.Phonology))];
    }

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoModalAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.DoesNotContain(l.Modals.Markers.Values, m => m.Value is Bound));

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
        var clauses = new Clause[]
        {
            DogEatsFish,
            Declared(new Event(new Done("eat", Dog), Fish, [], []), Tense.Past, Aspect.Perfective, false),
            Declared(new Event(new Being(new Adjective("big", false)), Dog, [], []), Tense.Present, Aspect.Imperfective, false),
            Declared(new Event(new Being(new Oblique(Role.Location, BareNoun("house", Number.Singular), Configuration.At)), Dog, [], []), Tense.Present, Aspect.Imperfective, false),
        };
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var modality in ModalityMap.Modalities)
            {
                foreach (var clause in clauses)
                {
                    var modal = modality is SituationalModality situational ? WithModal(clause, situational, false) : Judging(clause, (EpistemicModality)modality, false);
                    Assert.True(ModalAt(Realized(l, modal)) >= 0);
                }
            }
        });
    }

    [Fact]
    public void Realize_ModalVerb_GoesBeforeTheVerbInVOAndAfterItInOV()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[SituationalModality.Obligation].Value is ModalVerb).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, WithModal(DogEatsFish, SituationalModality.Obligation, false));
            var before = ModalAt(sentence) < IndexOf(sentence, l.Lexicon.Verb("eat").Root);
            Assert.Equal(l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial, before);
        });
    }

    [Fact]
    public void Realize_ModalVerb_TakesTheTenseAndLeavesTheVerbBare()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[SituationalModality.Ability].Value is ModalVerb).ToList();
        Assert.All(languages, l =>
        {
            var clause = DogEatsFish.At(Tense.Past, Aspect.Imperfective);
            var sentence = Realized(l, WithModal(clause, SituationalModality.Ability, false));
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
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[SituationalModality.Ability].Value is Bound && l.Passive is { Exponent.Value: Bound }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, WithModal(Declared(new Event(new Done("eat", null), Fish, [], []), Tense.Past, Aspect.Perfective, false), SituationalModality.Ability, false));
            var morphemes = sentence.Words[ModalAt(sentence)].Word.Morphemes.Select(m => m.Value).ToList();
            var root = morphemes.FindIndex(m => m is Root);
            var prefix = ((Bound)l.Modals.Markers[SituationalModality.Ability].Value!).Side == Side.Before;
            var passivePrefix = l.Passive!.Exponent.Side == Side.Before;

            var modal = morphemes.FindIndex(m => m is Affix { Grammemes: [Modality] });
            Assert.Equal(prefix ? root - (passivePrefix ? 2 : 1) : root + (passivePrefix ? 1 : 2), modal);
        });
    }

    [Fact]
    public void Realize_NegatedModalVerb_NegatesTheModalAndNotTheVerb()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[SituationalModality.Ability].Value is ModalVerb
            && l.Negation.Strategy is NegationStrategy.Prefix or NegationStrategy.Suffix).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var eat = l.Lexicon.Verb("eat").Root;
            var cannot = Realized(l, WithModal(DogEatsFish, SituationalModality.Ability, true));
            var mustNot = Realized(l, WithModal(DogEatsFish.Not(), SituationalModality.Ability, false));

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
            var big = Declared(new Event(new Being(new Adjective("big", false)), Dog, [], []), Tense.Present, Aspect.Imperfective, false);
            var sentence = Realized(l, WithModal(big, SituationalModality.Obligation, false));

            Assert.True(IndexOf(sentence, l.Predication.Copula.Root) >= 0);
        });
    }

    [Fact]
    public void Realize_ModalClauseUnderASuffix_PutsTheSuffixOnTheModalVerb()
    {
        var spec = LanguageSpec.Free.Fix(Settings.ModalOf(SituationalModality.Ability), ModalKind.Verb)
            .Fix(Settings.SubordinatorOf(Subordinator.If), ExponentPosition.Suffix).Fix(Settings.TemporalBalance, Balance.Balanced);

        Assert.All(Enumerable.Range(0, 5).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            var main = Declared(new Event(new Doing("sleep", null), BareNoun("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false);
            var sentence = Realized(l, Under(main, Subordinator.If, WithModal(DogEatsFish, SituationalModality.Ability, false), false));
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
        var layers = PropositionOf(Parsed(gloss)).StateOfAffairs;
        var modalized = Assert.IsType<Modalized>((layers.Value is Negated negated ? negated.StateOfAffairs : layers).Value);

        Assert.Equal(modalNegative, layers.Value is Negated);
        Assert.Equal(verbNegative, modalized.StateOfAffairs.Value is Negated);
    }

    [Fact]
    public void Parse_ModalWithBe_IsAModalCopularClause()
    {
        var statement = Parsed("be.OBLIG  subject:2SG  adjective:careful");

        Assert.Equal(SituationalModality.Obligation, Assert.IsType<Modalized>(PropositionOf(statement).StateOfAffairs.Value).Modality);
        PredicateOf<Being>(statement);
    }

    [Fact]
    public void Parse_ModalPassiveWithAnAddressee_KeepsTheAddresseeOutside()
    {
        var addressed = Addressed(Line("carry.PASS.PERM  subject:box.DEF  addressee:marta"));

        Assert.IsType<Modalized>(PropositionOf(SentenceOf(addressed)).StateOfAffairs.Value);
        PredicateOf<Done>(SentenceOf(addressed));
    }

    [Theory]
    [InlineData("go.IMP.ABIL", "A command can't take ABIL")]
    [InlineData("be.OBLIG  subject:marta  adjective:tall.CMPR  standard:man.PL", "A comparison can't take OBLIG")]
    [InlineData("go.ABIL.OBLIG  subject:2SG", "more than one modality")]
    [InlineData("go.ABIL.DUB  subject:2SG", "more than one modality")]
    public void Parse_BadModals_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);

    //a few languages marking an epistemic modal as this says, each generated for the test
    private static IEnumerable<Language> Epistemic(Modality modality, ModalKind kind, Func<LanguageSpec, LanguageSpec> more) =>
        Enumerable.Range(0, 4).Select(seed => Generator.Generate(seed, more(LanguageSpec.Free.Fix(Settings.ModalOf(modality), kind))));

    [Theory]
    [InlineData("Dubitative", false)]
    [InlineData("Presumptive", false)]
    [InlineData("Dubitative", true)]
    public void Realize_EpistemicModalVerb_IsImpersonalAndLeavesTheClauseItsTense(string modality, bool negative) =>
        Assert.All(Epistemic(ClauseFile.ValuesOf<Modality>().Single(m => m.Name == modality), ModalKind.Verb,
            s => s.Fix(Settings.PropositionBalance, Balance.Balanced).Fix(Settings.Negation, NegationStrategy.Prefix, NegationStrategy.Suffix)), l =>
        {
            var epistemic = ClauseFile.ValuesOf<Modality>().Single(m => m.Name == modality);
            var clause = DogEatsFish.At(Tense.Past, Aspect.Imperfective);
            var sentence = Realized(l, Judging(clause, (EpistemicModality)epistemic, negative));
            var plain = Realized(l, clause);
            var eat = l.Lexicon.Verb("eat").Root;
            var modal = sentence.Words[ModalAt(sentence)];

            //what's judged keeps its own tense and agreement, and the judgment is the speaker's now, about 'it'
            Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss, sentence.Words[IndexOf(sentence, eat)].Gloss);
            Assert.Equal(ModalityMap.VerbGloss(epistemic), modal.Word.Root!.Meaning);
            Assert.DoesNotContain(Tense.Past.Gloss, modal.Gloss);
            Assert.Equal(negative, IsNegated(modal.Word));
        });

    [Theory]
    [InlineData(ModalKind.Affix)]
    [InlineData(ModalKind.Particle)]
    public void Realize_EpistemicAffixOrParticle_LeavesTheVerbItsTense(ModalKind kind) =>
        Assert.All(Epistemic(EpistemicModality.Presumptive, kind, s => kind == ModalKind.Affix ? s.Inflecting() : s), l =>
        {
            var clause = DogEatsFish.At(Tense.Past, Aspect.Perfect);
            var sentence = Realized(l, Judging(clause, EpistemicModality.Presumptive, false));
            var plain = Realized(l, clause);
            var eat = l.Lexicon.Verb("eat").Root;

            Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss,
                sentence.Words[IndexOf(sentence, eat)].Gloss.Replace($"-{EpistemicModality.Presumptive.Gloss}", "").Replace($"{EpistemicModality.Presumptive.Gloss}-", ""));
            Assert.Equal(kind == ModalKind.Particle ? plain.Words.Count + 1 : plain.Words.Count, sentence.Words.Count);
        });

    [Theory]
    [InlineData("leave.PRF.PRSM  subject:3SG.M", "Presumptive", "Perfect")]
    [InlineData("wait.PROG.DUB  subject:3PL  object:1PL", "Dubitative", "Progressive")]
    public void Parse_EpistemicModal_JudgesThePerfectOrProgressive(string gloss, string modality, string aspect)
    {
        var statement = Parsed(gloss);

        Assert.Equal(modality, JudgmentOf(statement)?.Name);
        Assert.Equal(aspect, AspectOf(statement).Name);
    }
}
