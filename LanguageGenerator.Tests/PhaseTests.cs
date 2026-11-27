using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//beginning and ceasing: begin or stop with a clause, which a language with an inceptive or cessative folds into the verb
public class PhaseTests
{

    //the dog begins or stops what's begun, which shares its subject
    private static Clause Phased(string verb, Tense tense, Aspect aspect, Statement begun, bool negative) =>
        Declared(new Event(new Doing(verb, new ComplementClause(ComplementKind.Event, begun, SharesSubject: true)), Dog, [], []), tense, aspect, negative);


    private static Sentence Realized(Language language, Statement statement) => Syntax.Realize(language, statement, "x");

    //a few languages marking beginning and ceasing as these say, each generated for the test
    private static IEnumerable<Language> Languages(AspectKind inceptive, AspectKind cessative, Func<LanguageSpec, LanguageSpec>? more = null) =>
        Enumerable.Range(0, 4).Select(seed => Generator.Generate(seed, (more ?? (s => s))(LanguageSpec.Free.Fix(Settings.Inceptive, inceptive).Fix(Settings.Cessative, cessative))));

    private static bool Marks(Word word, Grammeme phase) => word.Morphemes.Any(m => m.Value is Affix { Grammemes: var g } && g.Contains(phase) || m.Value is Particle { Grammemes: var p } && p.Contains(phase));

    [Theory]
    [InlineData("begin", AspectKind.Affix)]
    [InlineData("begin", AspectKind.Particle)]
    [InlineData("stop", AspectKind.Affix)]
    [InlineData("stop", AspectKind.Particle)]
    public void Realize_MarkedPhase_IsWhatsBegunWithTheMarkerAndThePhaseVerbsTense(string verb, AspectKind kind) =>
        Assert.All(Languages(kind, kind), l =>
        {
            var phase = Aspects.PhaseOf(verb)!;
            var sentence = Realized(l, Phased(verb, Tense.Past, Aspect.Perfective, DogEatsFish, negative: false));
            var plain = Realized(l, DogEatsFish.At(Tense.Past, Aspect.Perfective));
            var eat = l.Lexicon.Verb("eat").Root;

            Assert.Equal(-1, IndexOf(sentence, verb));
            if (kind == AspectKind.Affix)
            {
                var word = sentence.Words[IndexOf(sentence, eat)];
                Assert.True(Marks(word.Word, phase));
                Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss, word.Gloss.Replace($"-{phase.Gloss}", "").Replace($"{phase.Gloss}-", ""));
                Assert.Equal(plain.Words.Count, sentence.Words.Count);
            }
            else
            {
                var at = IndexOf(sentence, eat);
                var marker = sentence.Words.ToList().FindIndex(w => Marks(w.Word, phase));
                var before = Assert.IsType<Free>((verb == "begin" ? l.Aspects.Inceptive : l.Aspects.Cessative)?.Value).Side == Side.Before;
                Assert.Equal(before ? at - 1 : at + 1, marker);
                Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss, sentence.Words[at].Gloss);
                Assert.Equal(plain.Words.Count + 1, sentence.Words.Count);
            }
        });

    [Fact]
    public void Realize_UnmarkedPhase_SaysTheVerb() =>
        Assert.All(Languages(AspectKind.None, AspectKind.None), l =>
        {
            Assert.True(IndexOf(Realized(l, Phased("begin", Tense.Past, Aspect.Perfective, DogEatsFish, negative: false)), "begin") >= 0);
            Assert.True(IndexOf(Realized(l, Phased("stop", Tense.Present, Aspect.Imperfective, DogEatsFish, negative: false)), "stop") >= 0);
        });

    public static TheoryData<string, Clause> Unfoldable => new()
    {
        { "what's begun has its own subject", Declared(new Event(new Doing("begin",
            new ComplementClause(ComplementKind.Event, Declared(new Event(new Doing("sleep", null), BareNoun("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), SharesSubject: false)),
            Dog, [], []), Tense.Past, Aspect.Perfective, false) },
        { "what's begun is negated", Phased("begin", Tense.Past, Aspect.Perfective, DogEatsFish.Not(), negative: false) },
        { "what's begun is said with be", Phased("begin", Tense.Past, Aspect.Perfective,
            Declared(new Event(new Being(new Adjective("big", false)), Dog, [], []), Tense.Present, Aspect.Imperfective, false), negative: false) },
        { "what's begun begins something itself", Phased("begin", Tense.Past, Aspect.Perfective, Phased("begin", Tense.Present, Aspect.Imperfective, DogEatsFish, negative: false), negative: false) },
        { "nothing is begun", Declared(new Event(new Doing("begin", null), Dog, [], []), Tense.Past, Aspect.Perfective, false) },
        { "what's begun is a thing", Declared(new Event(new Doing("begin", Fish), Dog, [], []), Tense.Past, Aspect.Perfective, false) },
    };

    [Theory]
    [MemberData(nameof(Unfoldable))]
    public void Realize_PhaseThatCantFold_KeepsItsVerb(string because, Clause clause) =>
        Assert.All(Languages(AspectKind.Affix, AspectKind.Affix, s => s.Inflecting()), l =>
            Assert.True(IndexOf(Realized(l, clause), "begin") >= 0, because));

    [Fact]
    public void Realize_NegatedPhaseVerb_NegatesTheFoldedVerb() =>
        Assert.All(Languages(AspectKind.Affix, AspectKind.Affix, s => s.Inflecting().Fix(Settings.Negation, NegationStrategy.Prefix, NegationStrategy.Suffix)), l =>
        {
            var sentence = Realized(l, Phased("stop", Tense.Past, Aspect.Perfective, DogEatsFish, negative: true));
            var plain = Realized(l, DogEatsFish.At(Tense.Past, Aspect.Perfective).Not());
            var eat = l.Lexicon.Verb("eat").Root;

            Assert.Equal(plain.Words[IndexOf(plain, eat)].Gloss, sentence.Words[IndexOf(sentence, eat)].Gloss.Replace("-CESS", "").Replace("CESS-", ""));
        });

    [Fact]
    public void Realize_PhaseAffix_GoesInsideTheAspectAndOutsideThePassive()
    {
        var languages = Languages(AspectKind.Affix, AspectKind.None, s => s.Inflecting().Fix(Settings.HasPassive, true).Fix(Settings.Perfect, AspectKind.Affix))
            .Where(l => l.Passive is { Exponent.Value: Bound }).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var begun = Declared(new Event(new Done("eat", null), Dog, [], []), Tense.Present, Aspect.Imperfective, false);
            var sentence = Realized(l, Phased("begin", Tense.Past, Aspect.Perfect, begun, negative: false));
            var morphemes = sentence.Words[IndexOf(sentence, l.Lexicon.Verb("eat").Root)].Word.Morphemes.Select(m => m.Value).ToList();
            int Distance(Grammeme grammeme) => Math.Abs(morphemes.FindIndex(m => m is Affix { Grammemes: var g } && g.Contains(grammeme)) - morphemes.FindIndex(m => m is Root));

            //each nearer the root than the one before, where they're on the same side of it
            var inceptive = Assert.IsType<Bound>(l.Aspects.Inceptive?.Value).Side;
            if (l.Passive!.Exponent.Side == inceptive)
            {
                Assert.True(Distance(Voice.Passive) < Distance(Phase.Inceptive));
            }
            if (inceptive == Assert.IsType<Bound>(l.Aspects.Perfect?.Value).Side)
            {
                Assert.True(Distance(Phase.Inceptive) < Distance(Aspect.Perfect));
            }
        });
    }

    [Fact]
    public void Realize_FoldedPhaseUnderASuffix_PutsTheSuffixOnWhatsBegun() =>
        Assert.All(Languages(AspectKind.Affix, AspectKind.None, s => s.Inflecting().Fix(Settings.SubordinatorOf(Subordinator.If), ExponentPosition.Suffix).Fix(Settings.TemporalBalance, Balance.Balanced)), l =>
        {
            var main = Declared(new Event(new Doing("sleep", null), BareNoun("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false);
            var sentence = Realized(l, new Subordinated(main, Subordinator.If, Phased("begin", Tense.Present, Aspect.Imperfective, DogEatsFish, negative: false), false));

            Assert.Contains(l.Subordination.Markers[Subordinator.If].Marker, sentence.Words[IndexOf(sentence, l.Lexicon.Verb("eat").Root)].Word.Morphemes);
        });

    [Fact]
    public void Realize_DerankedPhase_FoldsToo() =>
        Assert.All(Languages(AspectKind.Affix, AspectKind.None, s => s.Inflecting().Fix(Settings.EventBalance, Balance.Deranked).Fix(Settings.Desiderative, DesiderativeKind.Verb)), l =>
        {
            var wanted = new ComplementClause(ComplementKind.Event, Phased("begin", Tense.Present, Aspect.Imperfective, DogEatsFish, negative: false), SharesSubject: true);
            var sentence = Realized(l, Declared(new Event(new Doing("want", wanted), Dog, [], []), Tense.Present, Aspect.Imperfective, false));

            Assert.Equal(-1, IndexOf(sentence, "begin"));
            Assert.True(Marks(sentence.Words[IndexOf(sentence, l.Lexicon.Verb("eat").Root)].Word, Phase.Inceptive));
        });

    [Theory]
    [InlineData("hum.INCEP  subject:3SG.N", "Write hum.INCEP as begin object:[to  hum]")]
    [InlineData("talk.CESS  subject:everyone", "Write talk.CESS as stop object:[to  talk]")]
    public void Parse_PhaseGloss_SaysToWriteItsVerb(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);
}
