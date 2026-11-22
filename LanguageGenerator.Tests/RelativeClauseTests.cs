using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//relative clauses: how they're written, how languages draw their strategies, and how a noun
//phrase says one
public class RelativeClauseTests
{
    private static Statement Parsed(string text) => Assert.Single(ClauseFile.Parse(text)).Statement;

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static NounPhrase ObjectOf(Statement statement) => Assert.IsType<NounPhrase>(Assert.IsType<Clause>(statement.Value).Object.GetValueOrDefault().Value);

    [Fact]
    public void Parse_RelativeInANounPhrase_IsItsClauseWithTheGapInItsRole()
    {
        var net = ObjectOf(Parsed("x\n  lift  subject:3SG.F  object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_])"));

        var relative = Assert.IsType<Clause>(net.Relative?.Value);
        Assert.Equal("mend", relative.Verb);
        Assert.Equal(new Gap(null), relative.Object.GetValueOrDefault().Value);
        Assert.Equal(Definiteness.Definite, net.Definiteness);
    }

    [Fact]
    public void Parse_GapInAnOblique_KeepsItsRoleAndConfiguration()
    {
        var clearing = Assert.IsType<NounPhrase>(Assert.IsType<Clause>(Parsed("x\n  stand  subject:circle.DEF  location.in:(clearing.NDF relative:[grow  subject:nothing  location.in:_])").Value).Obliques[0].Argument.Value);

        var oblique = Assert.Single(Assert.IsType<Clause>(clearing.Relative?.Value).Obliques);
        Assert.Equal((Role.Location, Configuration.In), (oblique.Role, oblique.Configuration));
        Assert.IsType<Gap>(oblique.Argument.Value);
    }

    [Fact]
    public void Parse_RelativeWithAnEventSharingItsGappedSubject_IsOneGap()
    {
        var man = ObjectOf(Parsed("x\n  see.PST  subject:1SG  object:(man.DEF relative:[want  subject:_  object:[to  go  goal:home]])"));

        Assert.IsType<Gap>(Assert.IsType<Clause>(man.Relative?.Value).Subject.Value);
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
        Assert.All(Draws(s => s.Fix(Settings.Fusion, Fusion.Isolating)), r => Assert.NotEqual(RelativizerKind.Affix, r.Relativizer.Kind));

    private static NounPhrase Head(string noun, Number number, Statement relative) => new(noun, number, Definiteness: Definiteness.Definite, Relative: relative);

    //'the dogs that sleep sleep', with the gap as the relative clause's subject
    private static readonly Clause Sleeps = new("sleep", Tense.Present, Aspect.Imperfective, new Gap(null)) { Adverbs = [] };

    //'the woman sees the fish that the cat ate', with the gap as its object
    private static Clause SeesWhatTheCatAte() => new("see", Tense.Present, Aspect.Imperfective, new NounPhrase("woman", Number.Singular),
        Head("fish", Number.Singular, new Clause("eat", Tense.Past, Aspect.Perfective, new NounPhrase("cat", Number.Singular), new Gap(null)) { Adverbs = [] })) { Adverbs = [] };

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
        var dogs = new NounPhrase("dog", Number.Plural, Definiteness: Definiteness.Definite);
        var alone = Syntax.Realize(language, Sleeps with { Subject = dogs }, "x");
        var relative = Syntax.Realize(language, new Clause("bark", Tense.Present, Aspect.Imperfective, Head("dog", Number.Plural, Sleeps)) { Adverbs = [] }, "x");

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
        var at = words.FindIndex(r => r is not null && (r == pronoun.Own.Root || language.Questions.Words.Values.Any(q => q.Root == r)));

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
        var grows = new Clause("grow", Tense.Present, Aspect.Imperfective, new NounPhrase("tree", Number.Plural), Obliques: [new(Role.Location, new Gap(null), Configuration.At)]) { Adverbs = [] };
        var relative = Syntax.Realize(language, new Clause("sleep", Tense.Present, Aspect.Imperfective, Head("field", Number.Singular, grows)) { Adverbs = [] }, "x");
        var main = Syntax.Realize(language, new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("field", Number.Singular, Definiteness: Definiteness.Definite)) { Adverbs = [] }, "x");
        var placeless = Syntax.Realize(language, grows with { Obliques = [] }, "x");

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
            Assert.True(r is { BeforeNoun: true, Subject: RelativeStrategy.Pronoun, Object: RelativeStrategy.Resumptive, Oblique: RelativeStrategy.Gap, Relativizer.Kind: RelativizerKind.Affix });
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
