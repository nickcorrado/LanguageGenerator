using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Phrases;
namespace LanguageGenerator.Tests;

public class CommandTests
{
    private static readonly Clause Eat = new Clause(new Doing("eat", null), new Directive(Number.Singular), false, [], []);
    private static IEnumerable<Language> Sample => GeneratedLanguages.Sample;

    //a language's own lexicon, since coining a word into a shared one is off limits
    private static Sentence Say(Language language, Clause command) => Syntax.Realize(language, command, "");

    //the first of a few languages with these settings fixed that also has what no setting says, as a
    //bare stem for an imperative
    private static Language FirstWith(Func<LanguageSpec, LanguageSpec> fix, Func<Language, bool> keep) =>
        Enumerable.Range(0, 30).Select(seed => Generator.Generate(seed, fix(LanguageSpec.Free))).First(keep);

    private static LanguageSpec WithSingular(LanguageSpec spec) =>
        spec.Fix(Settings.Imperatives, ImperativeForms.SingularAndPlural, ImperativeForms.SingularOnly, ImperativeForms.OneForBoth);

    private static bool Bare(Morpheme? marker) => marker.HasValue && marker.GetValueOrDefault().Form.Count == 0;
    private static bool Sounded(Morpheme? marker) => marker.HasValue && marker.GetValueOrDefault().Form.Count > 0;

    private static string Glosses(Sentence sentence) => string.Join(" ", sentence.Words.Select(w => w.Gloss));

    [Fact]
    public void GenerateCommands_ManyLanguages_UseBareStems()
    {
        Assert.Contains(Sample, l => Bare(l.Commands.Singular));
        Assert.Contains(Sample, l => Sounded(l.Commands.Singular));
    }

    [Fact]
    public void GenerateCommands_PluralWithItsOwnForm_IsNeverZero()
    {
        Assert.DoesNotContain(Sample, l => Bare(l.Commands.Plural) && !l.Commands.Plural.Equals(l.Commands.Singular));
    }

    [Fact]
    public void Generate_ProhibitiveNegator_IsGlossedAsOne()
    {
        Assert.All(Sample.Where(l => l.Commands.Prohibitive is not null), l =>
            Assert.All(l.Commands.Prohibitive!.Markers, m => Assert.Equal("PROH", m.Marker.Gloss)));
    }

    [Fact]
    public void Realize_Imperative_HasNoTenseOrAgreement()
    {
        var language = FirstWith(s => WithSingular(s).Fix(Settings.VerbAgreement, true), l => Sounded(l.Commands.Singular));

        var gloss = Glosses(Say(language, Eat));

        Assert.Contains("IMP", gloss);
        Assert.DoesNotContain("PRS", gloss);
        Assert.DoesNotContain("2", gloss);
    }

    [Fact]
    public void Realize_BareStemImperative_GlossesWithNoFormOfItsOwn()
    {
        var language = FirstWith(WithSingular, l => Bare(l.Commands.Singular));

        var words = Say(language, Eat).Words;

        Assert.Equal("eat", Assert.Single(words).Word.Morphemes.Single().Gloss);
        Assert.Contains("[IMP", words[0].Gloss);
    }

    [Fact]
    public void Realize_NoImperativeOfItsOwn_UsesTheSecondPerson()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Imperatives, ImperativeForms.PluralOnly, ImperativeForms.None).Fix(Settings.VerbAgreement, true));

        var gloss = Glosses(Say(language, Eat));

        Assert.DoesNotContain("IMP", gloss);
        Assert.Contains("2SG", gloss);
    }

    [Fact]
    public void Realize_SingularAndPlural_SpeakToDifferentNumbers()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Imperatives, ImperativeForms.SingularAndPlural));

        Assert.Contains("IMP.SG", Glosses(Say(language, Eat)));
        Assert.Contains("IMP.PL", Glosses(Say(language, Eat with { Force = new Directive(Number.Plural) })));
    }

    [Fact]
    public void Realize_DualCommand_TakesThePlural()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Imperatives, ImperativeForms.SingularAndPlural));

        Assert.Contains("IMP.PL", Glosses(Say(language, Eat with { Force = new Directive(Number.Dual) })));
    }

    [Fact]
    public void Realize_ProhibitiveWithItsOwnNegator_SaysThatInsteadOfNegation()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.ProhibitiveNegator, true));

        var gloss = Glosses(Say(language, Eat with { Negative = true }));

        Assert.Contains("PROH", gloss);
        Assert.DoesNotContain("NEG", gloss);
    }

    [Fact]
    public void Realize_ProhibitiveWithOrdinaryNegation_SaysNegation()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.ProhibitiveNegator, false));

        Assert.Contains("NEG", Glosses(Say(language, Eat with { Negative = true })));
    }

    [Fact]
    public void Realize_ProhibitiveNotUsingTheImperative_UsesTheSecondPerson()
    {
        var language = GeneratedLanguages.With(s => WithSingular(s).Fix(Settings.ProhibitiveUsesImperative, false).Fix(Settings.VerbAgreement, true));

        var (command, prohibitive) = (Glosses(Say(language, Eat)), Glosses(Say(language, Eat with { Negative = true })));

        Assert.Contains("IMP", command);
        Assert.DoesNotContain("IMP", prohibitive);
        Assert.Contains("2SG", prohibitive);
    }

    [Fact]
    public void Realize_ProhibitiveUsingTheImperative_KeepsIt()
    {
        var language = GeneratedLanguages.With(s => WithSingular(s).Fix(Settings.ProhibitiveUsesImperative, true));

        Assert.Contains("IMP", Glosses(Say(language, Eat with { Negative = true })));
    }

    [Fact]
    public void Realize_Command_SaysNoSubjectEvenWhereSubjectsAreSaid()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.DropsSubjects, false));

        var words = Syntax.Realize(language, Eat, "").Words;

        Assert.DoesNotContain(words, w => w.Word.Root is { } root && language.Pronouns.Forms.Values.Any(p => p.Root == root));
    }

    [Fact]
    public void Realize_CommandWithAnObjectAndOblique_MarksThemAsUsual()
    {
        var language = Generator.Generate(3, LanguageSpec.Free);
        var hunter = BareNoun("hunter", Number.Singular);
        var inn = BareNoun("inn", Number.Singular);

        var told = Syntax.Realize(language, new Clause(new Doing("give", hunter), new Directive(Number.Singular), false, [new(Role.Goal, inn, Configuration.At)], []), "");
        var said = Syntax.Realize(language, new Clause(new Doing("give", hunter), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("dog", Number.Singular)), false, [new(Role.Goal, inn, Configuration.At)], []), "");

        Assert.Equal(said.Words.Single(w => w.Word.Root?.Meaning == "hunter").Gloss, told.Words.Single(w => w.Word.Root?.Meaning == "hunter").Gloss);
        Assert.Equal(said.Words.Single(w => w.Word.Root?.Meaning == "inn").Gloss, told.Words.Single(w => w.Word.Root?.Meaning == "inn").Gloss);
    }

    [Fact]
    public void Realize_AnyLanguage_SaysEveryKindOfCommand()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            foreach (var number in new[] { Number.Singular, Number.Dual, Number.Plural })
            {
                foreach (var negative in new[] { false, true })
                {
                    Assert.NotEmpty(Syntax.Realize(language, new Clause(new Doing("eat", BareNoun("fish", Number.Plural)), new Directive(number), negative, [new(Role.Location, BareNoun("house", Number.Singular), Configuration.At)], []), "").Text);
                }
            }
        }
    }

    private static T Parsed<T>(string gloss) => Said<T>(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance);

    [Fact]
    public void Parse_CommandWithBe_SaysWhatOrWhereToBe()
    {
        Assert.Equal(new Clause(new Being(new Adjective("careful", false)), new Directive(Number.Singular), false, [], []), Parsed<Clause>("be.IMP  adjective:careful"));
        Assert.Equal(new Clause(new Being(new NounPhrase(new Noun("fool", Number.Singular, [], null, null, null, Definiteness.Indefinite, null), null)), new Directive(Number.Plural), true, [], []),
            Parsed<Clause>("be.IMP.NEG  subject:2PL  noun:fool.NDF"));
        Assert.Equal(new Clause(new Being(new Oblique(Role.Location, new NounPhrase(new Noun("house", Number.Singular, [], null, null, null, Definiteness.Definite, null), null), Configuration.At)), new Directive(Number.Singular), false, [], []),
            Parsed<Clause>("be.IMP  location:house.DEF"));
    }

    [Fact]
    public void Realize_CommandWithBe_KeepsTheCopulaEvenWhereThePresentDropsIt()
    {
        Assert.All(Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Clause(new Being(new Adjective("big", false)), new Directive(Number.Singular), false, [], []), "");
            var roots = sentence.Words.Select(w => w.Word.Root).ToList();

            Assert.Contains(l.Lexicon.Adjective("big").Root, roots);
            Assert.Equal(l.Predication.Adjectives != PredicativeAdjectives.Verbal, roots.Contains(l.Predication.Copula.Root));
            Assert.DoesNotContain(sentence.Words, w => w.Word.Root is { } r && l.Pronouns.Forms.Values.Any(p => p.Root == r));
        });
    }

    [Fact]
    public void Realize_CommandWithBe_IsTheSameImperativeAsAnyVerbs()
    {
        Assert.All(Sample.Where(l => l.Predication.Adjectives != PredicativeAdjectives.Verbal), l =>
        {
            foreach (var negative in new[] { false, true })
            {
                //leaving out the adjective, the copula's words are what any verb's command makes of it
                var be = Syntax.Realize(l, new Clause(new Being(new Adjective("big", false)), new Directive(Number.Plural), negative, [], []), "");
                var copula = be.Words.Where(w => w.Word.Root != l.Lexicon.Adjective("big").Root).Select(w => w.Gloss);
                var eat = Syntax.Realize(l, new Clause(new Doing("eat", null), new Directive(Number.Plural), negative, [], []), "").Words
                    .Select(w => w.Gloss.Replace("eat", l.Predication.Copula.Root.Meaning));

                Assert.Equal(eat, copula);
            }
        });
    }

    [Fact]
    public void Realize_CommandToBeSomewhere_TakesTheLocationalCopula()
    {
        Assert.All(Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Clause(new Being(new Oblique(Role.Location, BareNoun("house", Number.Singular), Configuration.At)), new Directive(Number.Singular), false, [], []), "");

            Assert.Contains(l.Predication.CopulaFor(locational: true).Root, sentence.Words.Select(w => w.Word.Root));
        });
    }
}
