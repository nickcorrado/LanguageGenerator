using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//complement clauses and quotations: how they're written, how languages draw their marking, and
//how a clause says them
public class ComplementationTests
{
    private static T Parsed<T>(string text) => Said<T>(Assert.Single(ClauseFile.Parse(text)).Utterance);

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static T ObjectOf<T>(Clause clause) => Assert.IsType<T>(PredicateOf<Doing>(clause).Object.GetValueOrDefault().Value);

    [Fact]
    public void Parse_ThatInBrackets_IsAPropositionWithItsOwnSubject()
    {
        var complement = ObjectOf<ComplementClause>(Parsed<Clause>("x\n  tell.PST  subject:1SG  recipient:2SG  object:[that  be.PST  subject:2SG  adjective:late]"));

        Assert.Equal(ComplementKind.Proposition, complement.Kind);
        Assert.False(complement.SharesSubject);
        PredicateOf<Being>(Said<Clause>(complement.Clause));
        var clause = ForceOf<Declarative>(Said<Clause>(complement.Clause));
        Assert.Equal(Tense.Past, clause.Tense);
        Assert.Equal(new Pronoun(Person.Second, Number.Singular), clause.Subject.Value);
    }

    [Fact]
    public void Parse_ToWithoutASubject_IsAnEventSharingTheMainClauses()
    {
        var complement = ObjectOf<ComplementClause>(Parsed<Clause>("x\n  want  subject:3PL  object:[to  go  goal:home]"));

        Assert.Equal(ComplementKind.Event, complement.Kind);
        Assert.True(complement.SharesSubject);
        Assert.Equal(new Pronoun(Person.Third, Number.Plural), ForceOf<Declarative>(Said<Clause>(complement.Clause)).Subject.Value);
    }

    [Fact]
    public void Parse_ToInACommand_SharesYou()
    {
        var command = Parsed<Clause>("x\n  try.IMP  subject:2PL  object:[to  sleep]");
        ForceOf<Directive>(command);
        var complement = Assert.IsType<ComplementClause>(PredicateOf<Doing>(command).Object.GetValueOrDefault().Value);

        Assert.Equal(new Pronoun(Person.Second, Number.Plural), ForceOf<Declarative>(Said<Clause>(complement.Clause)).Subject.Value);
    }

    [Fact]
    public void Parse_Quote_KeepsWhatWasSaidAsItWas()
    {
        var quotation = ObjectOf<Quotation>(Parsed<Clause>("x\n  say  subject:3SG.F  quote:[go.IMP  goal:inn.DEF  addressee:marta]"));

        var addressed = Said<Addressed>(quotation.Utterance);
        ForceOf<Directive>(Said<Clause>(addressed.Statement));
    }

    [Fact]
    public void Parse_ClauseInsideAClauseInsideAClause_Nests()
    {
        var outer = ObjectOf<ComplementClause>(Parsed<Clause>("x\n  say  subject:3SG.F  object:[that  know  subject:1SG  object:[that  come.PST  subject:(man.DEF old)]]"));
        var inner = ObjectOf<ComplementClause>(Said<Clause>(outer.Clause));

        Assert.Equal("come", PredicateOf<Doing>(Said<Clause>(inner.Clause)).Verb);
    }

    [Fact]
    public void Parse_QuestionAboutAClause_AsksTheMainOne()
    {
        var question = Parsed<Question>("x\n  tell.PST  subject:who  recipient:2SG  object:[that  come.PST  subject:1SG]");

        Assert.IsType<ComplementClause>(PredicateOf<Doing>(Said<Clause>(question.Statement)).Object.GetValueOrDefault().Value);
    }

    [Theory]
    [InlineData("x\n  say  subject:1SG  object:[sleep  subject:dog]", "starts with that")]
    [InlineData("x\n  want  subject:1SG  object:[to  sleep.PST]", "can't take PST")]
    [InlineData("x\n  say  subject:1SG  object:[that  go.IMP]", "can't be a command")]
    [InlineData("x\n  see.PST  subject:1SG  object:[that  sleep.Q  subject:dog]", "A question inside another clause")]
    [InlineData("x\n  say  subject:1SG  object:[that  sleep  subject:dog  addressee:marta]", "Say who's addressed")]
    [InlineData("x\n  say  subject:1SG  object:[that  sleep]", "needs a subject")]
    [InlineData("x\n  sleep  subject:1SG  location:[that  sleep  subject:dog]", "Only object: and quote:")]
    [InlineData("x\n  say  subject:1SG  quote:dog", "word for word")]
    [InlineData("x\n  tell  subject:1SG  object:dog  quote:[sleep  subject:dog]", "more than one object")]
    [InlineData("x\n  be  subject:1SG  object:[that  sleep  subject:dog]", "be has no object")]
    [InlineData("x\n  say  subject:1SG  object:[that  sleep  subject:dog", "isn't closed")]
    [InlineData("x\n  say  subject:1SG  object:(dog]", "should close")]
    [InlineData("x\n  sleep  subject:(dog [that])", "Only object: and quote:")]
    public void Parse_BadClausesInBrackets_SayWhy(string text, string message) => Assert.Contains(message, Error(text).Message);

    [Fact]
    public void Nouns_IncludeTheClausesInside() =>
        Assert.Equal(["woman", "dog", "cat"], ClauseFile.Nouns(ClauseFile.Parse("x\n  say  subject:woman.DEF  object:[that  see.PST  subject:dog  object:cat]")).Select(n => n.Noun));

    private static List<Complementation> Draws(Func<LanguageSpec, LanguageSpec> fix)
    {
        var language = GeneratedLanguages.With(fix);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, 4000).Select(_ => Generator.GenerateComplementation(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology))];
    }

    [Fact]
    public void GenerateComplementation_DesiderativeMarker_SaysVolition()
    {
        Assert.All(Draws(s => s.Inflecting()), c => Assert.True(c.Desiderative?.Value is null or Bound { Affix.Grammemes: [Volition] } or Free { Particle.Grammemes: [Volition] }));
    }

    [Fact]
    public void GenerateComplementation_ManyDraws_MarkQuotationsWithTheComplementizerOnlyWhereThereIsOne() =>
        Assert.All(Draws(s => s), c => Assert.False(c.QuotativeKind == QuotativeKind.Complementizer && c.Proposition.Position == ComplementizerPosition.None));

    [Fact]
    public void GenerateComplementation_IsolatingLanguage_UsesNoAffixes() =>
        Assert.All(Draws(s => s.Fix(Settings.Fusion, Fusion.Isolating)), c =>
        {
            Assert.NotEqual(ComplementizerPosition.Suffix, c.Proposition.Position);
            Assert.NotEqual(ComplementizerPosition.Suffix, c.Event.Position);
            Assert.NotEqual(DesiderativeKind.Affix, c.DesiderativeKind);
        });

    private static readonly Clause CatEats = new Clause(new Doing("eat", BareNoun("fish", Number.Singular)), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("cat", Number.Singular)), false, [], []);
    private static readonly Clause DogSleeps = new Clause(new Doing("sleep", null), new Declarative(Tense.Past, Aspect.Imperfective, BareNoun("dog", Number.Singular)), false, [], []);

    private static Clause Says(Argument said) => new Clause(new Doing("say", said), new Declarative(Tense.Past, Aspect.Perfective, BareNoun("woman", Number.Singular)), false, [], []);

    private static Clause Wants(Argument wanter, Statement wanted, bool shares) =>
        new Clause(new Doing("want", new ComplementClause(ComplementKind.Event, wanted, shares)), new Declarative(Tense.Present, Aspect.Imperfective, wanter), false, [], []);

    [Fact]
    public void Realize_PropositionWithItsComplementizerFirst_PutsItBetweenTheClauses()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.PropositionComplementizer, ComplementizerPosition.Initial)
            .Fix(Settings.PropositionBalance, Balance.Balanced).Fix(Settings.ComplementsLast, true));
        var sentence = Syntax.Realize(language, Says(new ComplementClause(ComplementKind.Proposition, DogSleeps, false)), "x");
        var complementizer = sentence.Words.ToList().FindIndex(w => w.Gloss == "COMP");

        Assert.True(IndexOf(sentence, "say") < complementizer);
        Assert.True(complementizer < IndexOf(sentence, "dog"));
        Assert.True(complementizer < IndexOf(sentence, "sleep"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_PropositionInAnOVLanguage_GoesLastOrWhereTheObjectGoes(bool last)
    {
        var language = GeneratedLanguages.With(s => s.ObjectFirst(true).Fix(Settings.PropositionBalance, Balance.Balanced).Fix(Settings.ComplementsLast, last));
        var sentence = Syntax.Realize(language, Says(new ComplementClause(ComplementKind.Proposition, DogSleeps, false)), "x");

        Assert.Equal(last, IndexOf(sentence, "sleep") > IndexOf(sentence, "say"));
    }

    [Fact]
    public void Realize_BalancedEvent_KeepsItsVerbsOrdinaryForm()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.EventBalance, Balance.Balanced).Fix(Settings.Desiderative, DesiderativeKind.Verb)
            .Fix(Settings.EventComplementizer, ComplementizerPosition.None).Fix(Settings.ComplementsLast, true));
        var it = new Pronoun(Person.Third, Number.Singular);
        var alone = Syntax.Realize(language, CatEats with { Force = new Declarative(Tense.Present, Aspect.Imperfective, it) }, "x");
        var wanted = Syntax.Realize(language, Wants(BareNoun("cat", Number.Singular), CatEats, true), "x");

        Assert.Equal(alone.Words[IndexOf(alone, "eat")].Gloss, wanted.Words[IndexOf(wanted, "eat")].Gloss);
    }

    [Fact]
    public void Realize_DerankedEventSharingTheSubject_LeavesItUnsaid()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.EventBalance, Balance.Deranked).Fix(Settings.Desiderative, DesiderativeKind.Verb));
        var sentence = Syntax.Realize(language, Wants(BareNoun("cat", Number.Singular), CatEats, true), "x");

        Assert.Single(sentence.Words, w => w.Word.Root?.Meaning == "cat");
        Assert.True(IndexOf(sentence, "want") >= 0);
    }

    [Theory]
    [InlineData(DesiderativeKind.Affix)]
    [InlineData(DesiderativeKind.Particle)]
    public void Realize_WantAsAMarker_GoesOnTheVerbOfWhatsWanted(DesiderativeKind kind)
    {
        var language = GeneratedLanguages.With(s => s.Inflecting().Fix(Settings.Desiderative, kind));
        var sentence = Syntax.Realize(language, Wants(BareNoun("cat", Number.Singular), CatEats, true), "x");

        Assert.Equal(-1, IndexOf(sentence, "want"));
        Assert.Single(sentence.Words, w => w.Word.Root?.Meaning == "cat");
        if (kind == DesiderativeKind.Affix)
        {
            Assert.Contains("DESID", sentence.Words[IndexOf(sentence, "eat")].Gloss);
        }
        else
        {
            Assert.Contains(sentence.Words, w => w.Gloss == "DESID");
        }
    }

    [Fact]
    public void Realize_WantingSomeoneElseToDoIt_NeedsTheVerb()
    {
        var language = GeneratedLanguages.With(s => s.Inflecting().Fix(Settings.Desiderative, DesiderativeKind.Affix));
        var sentence = Syntax.Realize(language, Wants(BareNoun("dog", Number.Singular), CatEats, false), "x");

        Assert.True(IndexOf(sentence, "want") >= 0);
        Assert.DoesNotContain(sentence.Words, w => w.Gloss.Contains("DESID"));
    }

    [Fact]
    public void Realize_UnmarkedQuotationLast_IsSaidAsItWouldBeAlone()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Quotative, QuotativeKind.None).Fix(Settings.ComplementsLast, true));
        var command = new Clause(new Doing("walk", null), new Directive(Number.Singular), false, [new(Role.Goal, BareNoun("house", Number.Singular), Configuration.At)], []);
        var alone = Syntax.Realize(language, command, "x").Words.Select(w => w.Gloss);
        var quoted = Syntax.Realize(language, Says(new Quotation((Statement)command)), "x").Words.Select(w => w.Gloss);

        Assert.Equal(alone, quoted.TakeLast(alone.Count()));
    }

    [Theory]
    [InlineData(QuotativeKind.Word)]
    [InlineData(QuotativeKind.Complementizer)]
    public void Realize_MarkedQuotation_TakesTheQuotative(QuotativeKind kind)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Quotative, kind).Fix(Settings.PropositionComplementizer, ComplementizerPosition.Initial, ComplementizerPosition.Final));
        var sentence = Syntax.Realize(language, Says(new Quotation((Statement)DogSleeps)), "x");
        var quotative = Assert.Single(sentence.Words, w => w.Gloss == "QUOT");

        if (kind == QuotativeKind.Complementizer)
        {
            Assert.Equal(language.Complementation.Proposition.Marker?.Marker.Form, quotative.Word.Morphemes[0].Form);
        }
    }

    [Fact]
    public void Generate_ComplementationSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""
            {
              "proposition-complementizer": "Final",
              "event-complementizer": "Suffix",
              "proposition-balance": "Either",
              "event-balance": "Balanced",
              "quotative": "Complementizer",
              "complements-last": false,
              "desiderative": "Particle"
            }
            """);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            var c = l.Complementation;
            Assert.True(c.Proposition is { Position: ComplementizerPosition.Final, Balance: Balance.Either });
            Assert.True(c.Event is { Position: ComplementizerPosition.Suffix, Balance: Balance.Balanced });
            Assert.Equal(QuotativeKind.Complementizer, c.QuotativeKind);
            Assert.False(c.Last);
            Assert.IsType<Free>(c.Desiderative?.Value);
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_RareComplementizerInAVerbObjectLanguage_IsWrittenIn()
    {
        var spec = LanguageSpec.Free.ObjectFirst(false).Inflecting().Fix(Settings.PropositionComplementizer, ComplementizerPosition.Suffix);

        Assert.All(Enumerable.Range(0, 5).Select(seed => Generator.Generate(seed, spec)), l =>
            Assert.Equal(ComplementizerPosition.Suffix, l.Complementation.Proposition.Position));
    }

    [Fact]
    public void Write_QuotativeComplementizerWhereThereIsNone_SaysOneWithTheEventsForm()
    {
        var drawn = Draws(s => s).First(c => c.Proposition.Position == ComplementizerPosition.None && c.Event.Marker is not null);
        var written = Settings.Quotative.Write(drawn, [QuotativeKind.Complementizer]);

        Assert.Equal(ComplementizerPosition.Initial, written.Proposition.Position);
        Assert.Equal(drawn.Event.Marker?.Marker.Form, written.Proposition.Marker?.Marker.Form);
    }

    [Fact]
    public void Write_ComplementizerWithNoFormToReuse_PassesTheDrawOver()
    {
        var drawn = Draws(s => s).First(c => c.Proposition.Marker is null && c.Event.Marker is null && c.Quotative is not { Value: Free });

        Assert.Throws<NoFormException>(() => Settings.PropositionComplementizer.Write(drawn, [ComplementizerPosition.Initial]));
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "proposition-complementizer": "Suffix", "fusion": "Isolating" }""", "proposition-complementizer can only be a suffix" },
        { """{ "event-complementizer": "Suffix", "fusion": "Isolating" }""", "event-complementizer can only be a suffix" },
        { """{ "desiderative": "Affix", "fusion": "Isolating" }""", "'Want' can only be an affix" },
        { """{ "quotative": "Complementizer", "proposition-complementizer": "None" }""", "can't be None" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }
}
