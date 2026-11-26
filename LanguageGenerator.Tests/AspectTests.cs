using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class AspectTests
{
    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    private static readonly NounPhrase Dog = BareNoun("dog", Number.Singular);
    private static readonly NounPhrase Fish = BareNoun("fish", Number.Singular);
    private static Clause DogEatsFish(Tense tense, Aspect aspect, bool negative = false) => new Clause(new Doing("eat", Fish), new Declarative(tense, aspect, Dog), negative, [], []);

    private static Sentence Realized(Language language, Statement statement) => Syntax.Realize(language, statement, "x");

    //a few languages marking the perfect and the progressive as these say, each generated for the test
    private static IEnumerable<Language> Languages(AspectKind perfect, AspectKind progressive, Func<LanguageSpec, LanguageSpec>? more = null) =>
        Enumerable.Range(0, 4).Select(seed => Generator.Generate(seed, (more ?? (s => s))(LanguageSpec.Free.Fix(Settings.Perfect, perfect).Fix(Settings.Progressive, progressive))));

    private static IReadOnlyList<string> Glosses(Sentence sentence) => [.. sentence.Words.Select(w => w.Gloss)];

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoAspectAffixes() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.DoesNotContain(l.Aspects.All, m => m?.Value is Bound));

    [Fact]
    public void Generate_PerfectAndProgressiveParticles_SoundDifferent() =>
        Assert.All(Languages(AspectKind.Particle, AspectKind.Particle), l =>
            Assert.False(Assert.IsType<Free>(l.Aspects.Perfect?.Value).Particle.Form.SequenceEqual(Assert.IsType<Free>(l.Aspects.Progressive?.Value).Particle.Form)));

    [Fact]
    public void Realize_UnmarkedPerfect_IsAPerfectivePast() =>
        Assert.All(Languages(AspectKind.None, AspectKind.None), l =>
            Assert.Equal(Glosses(Realized(l, DogEatsFish(Tense.Past, Aspect.Perfective))), Glosses(Realized(l, DogEatsFish(Tense.Present, Aspect.Perfect)))));

    [Fact]
    public void Realize_UnmarkedPastPerfect_StaysPast() =>
        Assert.All(Languages(AspectKind.None, AspectKind.None), l =>
            Assert.Equal(Glosses(Realized(l, DogEatsFish(Tense.Future, Aspect.Perfective))), Glosses(Realized(l, DogEatsFish(Tense.Future, Aspect.Perfect)))));

    [Theory]
    [InlineData("Progressive")]
    [InlineData("Habitual")]
    public void Realize_UnmarkedProgressiveOrHabitual_IsAnImperfective(string aspect) =>
        Assert.All(Languages(AspectKind.None, AspectKind.None, s => s.Fix(Settings.Habitual, AspectKind.None)), l =>
            Assert.Equal(Glosses(Realized(l, DogEatsFish(Tense.Past, Aspect.Imperfective))),
                Glosses(Realized(l, DogEatsFish(Tense.Past, ClauseFile.ValuesOf<Aspect>().Single(a => a.Name == aspect))))));

    [Fact]
    public void Realize_HabitualBe_IsTheCopulaThatSaysWhat() =>
        Assert.All(Languages(AspectKind.None, AspectKind.None, s => s.Fix(Settings.Habitual, AspectKind.Be).Fix(Settings.LocationalCopula, true)), l =>
        {
            var habitual = Realized(l, DogEatsFish(Tense.Past, Aspect.Habitual));
            Assert.True(IndexOf(habitual, l.Predication.Copula.Root) >= 0);
            Assert.Equal([l.Lexicon.Verb("eat").Root], habitual.Words[IndexOf(habitual, l.Lexicon.Verb("eat").Root)].Word.Morphemes.Select(m => m.Value as Root));
        });

    [Theory]
    [InlineData(AspectKind.Have, "have")]
    [InlineData(AspectKind.Finish, "finish")]
    public void Realize_PerfectAuxiliary_TakesTheTenseAndLeavesTheVerbBare(AspectKind kind, string meaning) =>
        Assert.All(Languages(kind, AspectKind.None), l =>
        {
            var perfect = Realized(l, DogEatsFish(Tense.Past, Aspect.Perfect));
            var plain = Realized(l, DogEatsFish(Tense.Past, Aspect.Imperfective));
            var eat = l.Lexicon.Verb("eat").Root;

            Assert.Equal([eat], perfect.Words[IndexOf(perfect, eat)].Word.Morphemes.Select(m => m.Value as Root));
            //the auxiliary is inflected just as the plain verb was
            Assert.Equal(plain.Words.Count, perfect.Words.Count - 1);
            Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss.Replace("eat", meaning), perfect.Words[IndexOf(perfect, meaning)].Gloss);
        });

    [Fact]
    public void Realize_FinishAuxiliary_FollowsTheVerbInVOAndOV() =>
        Assert.All((bool[])[true, false], objectFirst => Assert.All(Languages(AspectKind.Finish, AspectKind.None, s => s.ObjectFirst(objectFirst)), l =>
        {
            var perfect = Realized(l, DogEatsFish(Tense.Present, Aspect.Perfect));
            Assert.True(IndexOf(perfect, "finish") > IndexOf(perfect, l.Lexicon.Verb("eat").Root));
        }));

    [Fact]
    public void Realize_HaveAuxiliary_GoesBeforeTheVerbInVOAndAfterItInOV() =>
        Assert.All((bool[])[true, false], objectFirst => Assert.All(Languages(AspectKind.Have, AspectKind.None, s => s.ObjectFirst(objectFirst)), l =>
        {
            var perfect = Realized(l, DogEatsFish(Tense.Present, Aspect.Perfect));
            var before = IndexOf(perfect, "have") < IndexOf(perfect, l.Lexicon.Verb("eat").Root);
            Assert.Equal(l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial, before);
        }));

    [Fact]
    public void Realize_ProgressiveBe_IsTheCopulaThatSaysWhere() =>
        Assert.All(Languages(AspectKind.None, AspectKind.Be, s => s.Fix(Settings.LocationalCopula, true)), l =>
        {
            var progressive = Realized(l, DogEatsFish(Tense.Present, Aspect.Progressive));
            Assert.True(IndexOf(progressive, l.Predication.LocationalCopula!.Root) >= 0);
        });

    [Fact]
    public void Realize_NegatedPerfectAuxiliary_NegatesTheAuxiliaryAndNotTheVerb() =>
        Assert.All(Languages(AspectKind.Have, AspectKind.None, s => s.Fix(Settings.Negation, NegationStrategy.Prefix, NegationStrategy.Suffix)), l =>
        {
            var hasNot = Realized(l, DogEatsFish(Tense.Present, Aspect.Perfect, negative: true));
            static bool Negated(Word word) => word.Morphemes.Any(m => m.Value is Affix { Grammemes: var g } && g.Contains(Polarity.Negative));

            Assert.True(Negated(hasNot.Words[IndexOf(hasNot, "have")].Word));
            Assert.False(Negated(hasNot.Words[IndexOf(hasNot, l.Lexicon.Verb("eat").Root)].Word));
        });

    [Fact]
    public void Realize_AspectAffix_GoesNextToTheRootOutsideThePassive()
    {
        var languages = Languages(AspectKind.Affix, AspectKind.None, s => s.Fix(Settings.HasPassive, true).Inflecting()).Where(l => l.Passive is { Exponent.Value: Bound }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var sentence = Realized(l, new Clause(new Done("eat", null), new Declarative(Tense.Past, Aspect.Perfect, Fish), false, [], []));
            var morphemes = sentence.Words[IndexOf(sentence, l.Lexicon.Verb("eat").Root)].Word.Morphemes.Select(m => m.Value).ToList();
            var root = morphemes.FindIndex(m => m is Root);
            var prefix = Assert.IsType<Bound>(l.Aspects.Perfect?.Value).Side == Side.Before;
            var passivePrefix = l.Passive!.Exponent.Side == Side.Before;

            var perfect = morphemes.FindIndex(m => m is Affix { Grammemes: [var g] } && g == Aspect.Perfect);
            Assert.Equal(prefix ? root - (passivePrefix ? 2 : 1) : root + (passivePrefix ? 1 : 2), perfect);
        });
    }

    [Fact]
    public void Realize_AspectParticle_GoesBesideTheVerb() =>
        Assert.All(Languages(AspectKind.None, AspectKind.Particle), l =>
        {
            var sentence = Realized(l, DogEatsFish(Tense.Present, Aspect.Progressive));
            var at = sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Particle { Grammemes: [var g] } && g == Aspect.Progressive));
            var eat = IndexOf(sentence, l.Lexicon.Verb("eat").Root);
            Assert.True(at >= 0);
            Assert.Equal(Assert.IsType<Free>(l.Aspects.Progressive?.Value).Side == Side.Before ? eat - 1 : eat + 1, at);
        });

    [Fact]
    public void Realize_CopulaWithAnAspectMarker_IsNeverDropped() =>
        Assert.All(Languages(AspectKind.Particle, AspectKind.None, s => s.Fix(Settings.ZeroCopula, true).Fix(Settings.PredicativeAdjectives, PredicativeAdjectives.Nonverbal)), l =>
        {
            var big = new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, Aspect.Perfect, Dog), false, [], []);
            Assert.True(IndexOf(Realized(l, big), l.Predication.Copula.Root) >= 0);
        });

    [Fact]
    public void Realize_PerfectAuxiliaryUnderASuffix_PutsTheSuffixOnTheAuxiliary() =>
        Assert.All(Languages(AspectKind.Have, AspectKind.None, s => s.Fix(Settings.SubordinatorOf(Subordinator.If), ExponentPosition.Suffix).Fix(Settings.TemporalBalance, Balance.Balanced)), l =>
        {
            var main = new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("cat", Number.Singular)), false, [], []);
            var sentence = Realized(l, new Subordinated(main, Subordinator.If, DogEatsFish(Tense.Present, Aspect.Perfect), false));

            Assert.Contains(l.Subordination.Markers[Subordinator.If].Marker, sentence.Words[IndexOf(sentence, "have")].Word.Morphemes);
        });

    [Fact]
    public void Realize_EveryAspectInEveryLanguage_SaysIt()
    {
        var clauses = new Func<Aspect, Statement>[]
        {
            aspect => DogEatsFish(Tense.Present, aspect),
            aspect => new Clause(new Done("eat", Dog), new Declarative(Tense.Past, aspect, Fish), false, [], []),
            aspect => new Clause(new Being(new Adjective("big", false)), new Declarative(Tense.Present, aspect, Dog), false, [], []),
            aspect => new Question(DogEatsFish(Tense.Past, aspect), null),
        };
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var clause in clauses)
            {
                Assert.NotEmpty(Realized(l, clause(Aspect.Perfect)).Words);
                Assert.NotEmpty(Realized(l, clause(Aspect.Progressive)).Words);
                Assert.NotEmpty(Realized(l, clause(Aspect.Habitual)).Words);
            }
        });
    }

    [Theory]
    [InlineData("repair  subject:nobody  object:3SG.N", null)]
    [InlineData("repair.PRF  subject:nobody  object:3SG.N", "Perfect")]
    [InlineData("mend.PST.PROG  subject:3SG.F  object:net.NDF", "Progressive")]
    [InlineData("walk.PST.HAB  subject:3SG.F", "Habitual")]
    public void Parse_PerfectProgressiveAndHabitual_AreAspects(string gloss, string? aspect) =>
        Assert.Equal(aspect is null ? Aspect.Imperfective : ClauseFile.ValuesOf<Aspect>().Single(a => a.Name == aspect), ForceOf<Declarative>(Parsed(gloss)).Aspect);

    [Theory]
    [InlineData("go.PRF.OBLIG  subject:3SG.M", "OBLIG can't take PRF")]
    [InlineData("wait.PROG.ADVS  subject:2SG", "ADVS can't take PROG")]
    [InlineData("go.IMP.PRF", "a command has no tense or aspect")]
    [InlineData("go.PFV.PRF  subject:2SG", "more than one aspect")]
    public void Parse_BadAspects_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "perfect": "Affix", "fusion": "Isolating" }""", "A perfect can only be an affix" },
        { """{ "progressive": "Affix", "fusion": "Isolating" }""", "A progressive can only be an affix" },
        { """{ "progressive": "Have" }""", "progressive takes one of" },
        { """{ "habitual": "Affix", "fusion": "Isolating" }""", "A habitual can only be an affix" },
        { """{ "inceptive": "Affix", "fusion": "Isolating" }""", "An inceptive can only be an affix" },
        { """{ "cessative": "Sit" }""", "cessative takes one of" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem) =>
        Assert.Contains(problem, Assert.IsType<SpecException>(Record.Exception(() => Generator.Generate(1, SpecFile.Parse(json)))).Message);
}
