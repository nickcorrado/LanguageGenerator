using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//relative clauses: how they're written, how languages draw their strategies, and how a noun
//phrase says one
public class RelativeClauseTests
{
    private static Sentence Parsed(string text) => Stated(Assert.Single(ClauseFile.Parse(text)).Utterance);

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static NounPhrase ObjectOf(Sentence statement) => Assert.IsType<NounPhrase>(PredicateOf<Doing>(statement).Object.GetValueOrDefault().Value);

    [Fact]
    public void Parse_RelativeInANounPhrase_IsItsClauseWithTheGapInItsRole()
    {
        var net = ObjectOf(Parsed("x\n  lift  subject:3SG.F  object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_])"));

        var relative = Assert.IsType<Clause>(net.Relative?.Value);
        Assert.Equal("mend", PredicateOf<Doing>(relative).Verb);
        Assert.Equal(new Gap(null), PredicateOf<Doing>(relative).Object.GetValueOrDefault().Value);
        Assert.Equal(Definiteness.Definite, NounOf(net).Determiner?.Value);
    }

    [Fact]
    public void Parse_GapInAnOblique_KeepsItsRoleAndConfiguration()
    {
        var clearing = Assert.IsType<NounPhrase>(Assert.IsType<Clause>(Parsed("x\n  stand  subject:circle.DEF  location.in:(clearing.NDF relative:[grow  subject:nothing  location.in:_])").Value).Event.Obliques[0].Argument.Value);

        var oblique = Assert.Single(Assert.IsType<Clause>(clearing.Relative?.Value).Event.Obliques);
        Assert.Equal((Role.Location, Configuration.In), (oblique.Role, oblique.Configuration));
        Assert.IsType<Gap>(oblique.Argument.Value);
    }

    [Fact]
    public void Parse_RelativeWithAnEventSharingItsGappedSubject_IsOneGap()
    {
        var man = ObjectOf(Parsed("x\n  see.PST  subject:1SG  object:(man.DEF relative:[want  subject:_  object:[to  go  goal:home]])"));

        Assert.IsType<Gap>(Assert.IsType<Clause>(man.Relative?.Value).Event.Subject.Value);
    }

    [Theory]
    [InlineData("see  subject:1SG  object:(man relative:[sleep  subject:dog])", "needs _")]
    [InlineData("see  subject:1SG  object:(man relative:[see  subject:_  object:_])", "has one _")]
    [InlineData("see  subject:1SG  object:(man relative:[see  subject:dog  object:(cat possessor:_)])", "has one _")]
    [InlineData("see  subject:1SG  object:(man relative:[say  subject:dog  object:[that  sleep  subject:_]])", "has one _")]
    [InlineData("see  subject:1SG  object:(man relative:[be  subject:dog  adjective:tall.CMPR  standard:_])", "what it compares")]
    [InlineData("see  subject:1SG  object:(man relative:[go.IMP  goal:_])", "can't be a command")]
    [InlineData("see  subject:1SG  object:(man relative:[sleep.Q  subject:_])", "can't be a question")]
    [InlineData("see  subject:1SG  object:(man relative:[sleep  subject:_  addressee:marta])", "can't be said to someone")]
    [InlineData("see  subject:1SG  object:(man relative:dog)", "relative: is a clause in brackets")]
    [InlineData("see  subject:1SG  object:(man relative:[sleep  subject:_] relative:[eat  subject:_])", "more than one relative clause")]
    [InlineData("see  subject:_  object:man", "goes only inside relative:")]
    [InlineData("see  subject:1SG  object:(man [sleep  subject:_])", "Only object: and quote:")]
    public void Parse_BadRelativeClauses_SayWhy(string gloss, string message) => Assert.Contains(message, Error($"x\n  {gloss}").Message);

    [Fact]
    public void Parse_GapInAJoinedClause_IsRefused() =>
        Assert.Contains("goes only inside relative:", Error("x\n  sleep  subject:dog\n  and  eat  subject:_").Message);

    [Fact]
    public void Nouns_IncludeTheRelativeClauses() =>
        Assert.Equal(["woman", "net", "fish"], ClauseFile.Nouns(ClauseFile.Parse("x\n  lift  subject:woman.DEF  object:(net.DEF relative:[catch.PST  subject:_  object:fish.PL])")).Select(n => n.Noun));

    private static List<Relativization> Draws(Func<LanguageSpec, LanguageSpec> fix)
    {
        var language = GeneratedLanguages.With(fix);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, 4000).Select(_ => Generator.GenerateRelativization(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology))];
    }

    [Fact]
    public void GenerateRelativization_ManyDraws_KeepToTheAccessibilityHierarchy()
    {
        var drawn = Draws(s => s);

        Assert.Contains(drawn, r => r.Subject == RelativeStrategy.Resumptive);
        //resuming a subject resumes the roles below it on the Accessibility Hierarchy too
        Assert.All(drawn.Where(r => r.Subject == RelativeStrategy.Resumptive), r => Assert.Equal(RelativeStrategy.Resumptive, r.Oblique));
        Assert.All(drawn, r => Assert.True(r.Object == r.Subject || r.Object == r.Oblique));
    }

    [Fact]
    public void GenerateRelativization_IsolatingLanguage_UsesNoAffix() =>
        Assert.All(Draws(s => s.Fix(Settings.Fusion, Fusion.Isolating)), r => Assert.NotEqual(RelativizerKind.Affix, r.RelativizerKind));

    private static NounPhrase Head(string noun, Number number, Sentence relative) => new NounPhrase(new Noun(noun, number, [], null, null, null, Definiteness.Definite, null), relative);

    //'the dogs that sleep sleep', with the gap as the relative clause's subject
    private static readonly Clause Sleeps = Declared(new Event(new Doing("sleep", null), new Gap(null), [], []), Tense.Present, Aspect.Imperfective, false);

    //'the woman sees the fish that the cat ate', with the gap as its object
    private static Clause SeesWhatTheCatAte() => Declared(new Event(new Doing("see", Head("fish", Number.Singular, Declared(new Event(new Doing("eat", new Gap(null)), BareNoun("cat", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false))), BareNoun("woman", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_RelativeClause_GoesOnTheSideTheLanguageHasIt(bool before)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.RelativeClausesFirst, before));
        var sentence = Syntax.Realize(language, SeesWhatTheCatAte(), "x");

        Assert.Equal(before, IndexOf(sentence, "eat") < IndexOf(sentence, "fish"));
    }

    [Fact]
    public void Realize_GappedSubject_AgreesWithItsNoun()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.SubjectRelativization, RelativeStrategy.Gap).Fix(Settings.Relativizer, RelativizerKind.None));
        var dogs = new NounPhrase(new Noun("dog", Number.Plural, [], null, null, null, Definiteness.Definite, null), null);
        var alone = Syntax.Realize(language, Sleeps.WithEvent(e => e with { Subject = dogs }), "x");
        var relative = Syntax.Realize(language, Declared(new Event(new Doing("bark", null), Head("dog", Number.Plural, Sleeps), [], []), Tense.Present, Aspect.Imperfective, false), "x");

        Assert.Single(relative.Words, w => w.Word.Root?.Meaning == "dog");
        Assert.Equal(alone.Words[IndexOf(alone, "sleep")].Gloss, relative.Words[IndexOf(relative, "sleep")].Gloss);
    }

    [Fact]
    public void Realize_ResumedObject_LeavesAPronounInItsPlace()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.ObjectRelativization, RelativeStrategy.Resumptive).Fix(Settings.Relativizer, RelativizerKind.None));
        var sentence = Syntax.Realize(language, SeesWhatTheCatAte(), "x");

        Assert.Contains(sentence.Words, w => w.Gloss.StartsWith("3SG"));
    }

    [Fact]
    public void Realize_RelativePronoun_GoesFirstInItsClause()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.ObjectRelativization, RelativeStrategy.Pronoun).Fix(Settings.Relativizer, RelativizerKind.None));
        var sentence = Syntax.Realize(language, SeesWhatTheCatAte(), "x");
        var pronoun = language.Relativization.Pronoun;
        var words = sentence.Words.Select(w => w.Word.Root).ToList();
        var at = words.FindIndex(r => r is not null && (pronoun.Value is OwnRelativePronoun own && r == own.Word.Root || language.Questions.Words.Values.Any(q => q.Root == r)));

        Assert.True(at >= 0);
        var clause = new[] { at, IndexOf(sentence, "cat"), IndexOf(sentence, "eat") };
        Assert.Equal(at, clause.Min());
    }

    [Fact]
    public void Realize_ParticleRelativizer_GoesBesideTheNoun()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Relativizer, RelativizerKind.Particle).Fix(Settings.ObjectRelativization, RelativeStrategy.Gap));
        var sentence = Syntax.Realize(language, SeesWhatTheCatAte(), "x");
        var relativizer = sentence.Words.ToList().FindIndex(w => w.Gloss == "REL");

        Assert.True(relativizer >= 0);
        Assert.Equal(language.Relativization.BeforeNoun, relativizer > IndexOf(sentence, "eat"));
    }

    [Fact]
    public void Realize_AffixRelativizer_MakesTheVerbAParticiple()
    {
        var language = GeneratedLanguages.With(s => s.Inflecting().Fix(Settings.Relativizer, RelativizerKind.Affix));
        var sentence = Syntax.Realize(language, SeesWhatTheCatAte(), "x");
        var eat = sentence.Words[IndexOf(sentence, "eat")].Gloss;

        Assert.Contains("REL", eat);
        Assert.DoesNotContain("PST", eat);
    }

    [Fact]
    public void Realize_GappedOblique_LeavesNoAdpositionBehind()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.ObliqueRelativization, RelativeStrategy.Gap).Fix(Settings.Relativizer, RelativizerKind.None));
        var grows = Declared(new Event(new Doing("grow", null), BareNoun("tree", Number.Plural), [new(Role.Location, new Gap(null), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false);
        var relative = Syntax.Realize(language, Declared(new Event(new Doing("sleep", null), Head("field", Number.Singular, grows), [], []), Tense.Present, Aspect.Imperfective, false), "x");
        var main = Syntax.Realize(language, Declared(new Event(new Doing("sleep", null), new NounPhrase(new Noun("field", Number.Singular, [], null, null, null, Definiteness.Definite, null), null), [], []), Tense.Present, Aspect.Imperfective, false), "x");
        var placeless = Syntax.Realize(language, grows.WithEvent(e => e with { Obliques = [] }), "x");

        //the relative clause is the clause without its place, nothing of the oblique left
        Assert.Equal(main.Words.Count + placeless.Words.Count, relative.Words.Count);
    }

    [Fact]
    public void Generate_RelativizationSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""
            {
              "relative-clauses-before-noun": true,
              "subject-relativization": "Pronoun",
              "object-relativization": "Resumptive",
              "oblique-relativization": "Gap",
              "relativizer": "Affix"
            }
            """);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            var r = l.Relativization;
            Assert.True(r is { BeforeNoun: true, Subject: RelativeStrategy.Pronoun, Object: RelativeStrategy.Resumptive, Oblique: RelativeStrategy.Gap, RelativizerKind: RelativizerKind.Affix });
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Problems_AffixInAnIsolatingLanguage_SaysWhy()
    {
        var spec = SpecFile.Parse("""{ "relativizer": "Affix", "fusion": "Isolating" }""");

        Assert.Contains(spec.Problems(), p => p.Contains("A relativizer can only be an affix"));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }
}
