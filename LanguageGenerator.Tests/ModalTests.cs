using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ModalTests
{
    private static T Parsed<T>(string gloss) => Said<T>(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance);

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Dog = BareNoun("dog", Number.Singular);
    private static readonly NounPhrase Fish = BareNoun("fish", Number.Singular);
    private static Clause DogEatsFish(bool negative) => new Clause(new Doing("eat", Fish), new Declarative(Tense.Present, Aspect.Imperfective, Dog), negative, [], []);

    private static Sentence Realized(Language language, Statement statement) => Syntax.Realize(language, statement, "x");

    //where the modal's marker is in a sentence: the modal verb's word, or the word with its affix or particle
    private static int ModalAt(Sentence sentence) => sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value switch
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
        var clauses = new Statement[]
        {
            DogEatsFish(false),
            new Clause(new Done("eat", Dog), new Declarative(Tense.Past, Aspect.Perfective, Fish), false, [], []),
            new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []),
            new Clause(new Being(new Oblique(Role.Location, BareNoun("house", Number.Singular), Configuration.At)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []),
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
            var clause = DogEatsFish(false) with { Force = new Declarative(Tense.Past, Aspect.Imperfective, Dog) };
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
        var languages = GeneratedLanguages.Sample.Where(l => l.Modals.Markers[Modality.Ability].Value is Bound && l.Passive is { Exponent.Value: Bound }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Modalized(new Clause(new Done("eat", null), new Declarative(Tense.Past, Aspect.Perfective, Fish), false, [], []), Modality.Ability, false));
            var morphemes = sentence.Words[ModalAt(sentence)].Word.Morphemes.Select(m => m.Value).ToList();
            var root = morphemes.FindIndex(m => m is Root);
            var prefix = ((Bound)l.Modals.Markers[Modality.Ability].Value!).Side == Side.Before;
            var passivePrefix = l.Passive!.Exponent.Side == Side.Before;

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
            var big = new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Imperfective, Dog), false, [], []);
            var sentence = Realized(l, new Modalized(big, Modality.Obligation, false));

            Assert.True(IndexOf(sentence, l.Predication.Copula.Root) >= 0);
        });
    }

    [Fact]
    public void Realize_ModalClauseUnderASuffix_PutsTheSuffixOnTheModalVerb()
    {
        var spec = LanguageSpec.Free.Fix(Settings.ModalOf(Modality.Ability), ModalKind.Verb)
            .Fix(Settings.SubordinatorOf(Subordinator.If), ExponentPosition.Suffix).Fix(Settings.TemporalBalance, Balance.Balanced);

        Assert.All(Enumerable.Range(0, 5).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            var main = new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("cat", Number.Singular)), false, [], []);
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
        var modalized = Parsed<Modalized>(gloss);

        Assert.Equal(modalNegative, modalized.Negative);
        Assert.Equal(verbNegative, Assert.IsType<Clause>(modalized.Clause.Value).Negative);
    }

    [Fact]
    public void Parse_ModalWithBe_IsAModalCopularClause()
    {
        var modalized = Parsed<Modalized>("be.OBLIG  subject:2SG  adjective:careful");

        Assert.Equal(Modality.Obligation, modalized.Modality);
        PredicateOf<Being>(Said<Clause>(modalized.Clause));
    }

    [Fact]
    public void Parse_ModalPassiveWithAnAddressee_KeepsTheAddresseeOutside()
    {
        var addressed = Parsed<Addressed>("carry.PASS.PERM  subject:box.DEF  addressee:marta");

        PredicateOf<Done>(Said<Clause>(Assert.IsType<Modalized>(addressed.Statement.Value).Clause));
    }

    [Theory]
    [InlineData("go.IMP.ABIL", "A command can't take ABIL")]
    [InlineData("be.OBLIG  subject:marta  adjective:tall.CMPR  standard:man.PL", "A comparison can't take OBLIG")]
    [InlineData("go.ABIL.OBLIG  subject:2SG", "more than one modality")]
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
            var clause = DogEatsFish(false) with { Force = new Declarative(Tense.Past, Aspect.Imperfective, Dog) };
            var sentence = Realized(l, new Modalized(clause, epistemic, negative));
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
        Assert.All(Epistemic(Modality.Presumptive, kind, s => kind == ModalKind.Affix ? s.Inflecting() : s), l =>
        {
            var clause = DogEatsFish(false) with { Force = new Declarative(Tense.Past, Aspect.Perfect, Dog) };
            var sentence = Realized(l, new Modalized(clause, Modality.Presumptive, false));
            var plain = Realized(l, clause);
            var eat = l.Lexicon.Verb("eat").Root;

            Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss,
                sentence.Words[IndexOf(sentence, eat)].Gloss.Replace($"-{Modality.Presumptive.Gloss}", "").Replace($"{Modality.Presumptive.Gloss}-", ""));
            Assert.Equal(kind == ModalKind.Particle ? plain.Words.Count + 1 : plain.Words.Count, sentence.Words.Count);
        });

    [Theory]
    [InlineData("leave.PRF.PRSM  subject:3SG.M", "Presumptive", "Perfect")]
    [InlineData("wait.PROG.DUB  subject:3PL  object:1PL", "Dubitative", "Progressive")]
    public void Parse_EpistemicModal_JudgesThePerfectOrProgressive(string gloss, string modality, string aspect)
    {
        var modalized = Parsed<Modalized>(gloss);

        Assert.Equal(modality, modalized.Modality.Name);
        Assert.Equal(aspect, ForceOf<Declarative>(Said<Clause>(modalized.Clause)).Aspect.Name);
    }
}
