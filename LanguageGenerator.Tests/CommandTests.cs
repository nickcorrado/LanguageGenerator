namespace LanguageGenerator.Tests;

public class CommandTests
{
    private static readonly Command Eat = new("eat", Number.Singular);
    private static IEnumerable<Language> Sample => GeneratedLanguages.Sample;

    //a language's own lexicon, since coining a word into a shared one is off limits
    private static Sentence Say(int seed, Command command) => Syntax.Realize(Generator.Generate(new Random(seed)), command, "");

    private static int SeedWhere(Func<Language, bool> keep) => Enumerable.Range(0, 300).First(seed => keep(GeneratedLanguages.Sample[seed]));

    private static bool Bare(Morpheme? marker) => marker.HasValue && marker.GetValueOrDefault().Form.Count == 0;
    private static bool Sounded(Morpheme? marker) => marker.HasValue && marker.GetValueOrDefault().Form.Count > 0;

    private static string Glosses(Sentence sentence) => string.Join(" ", sentence.Words.Select(w => w.Gloss));

    private static double Share(Func<Commands, bool> keep)
    {
        var language = GeneratedLanguages.Sample[0];
        var rand = new Random(1);
        return Enumerable.Range(0, 3000).Count(_ => keep(Generator.GenerateCommands(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology, 0.15))) / 3000.0;
    }

    [Fact]
    public void GenerateCommands_ManyDraws_FollowWals70A()
    {
        Assert.InRange(Share(c => !c.Singular.HasValue && !c.Plural.HasValue), 122 / 548.0 - 0.03, 122 / 548.0 + 0.03);
        Assert.InRange(Share(c => c.Singular.HasValue && !c.Plural.HasValue), 43 / 548.0 - 0.02, 43 / 548.0 + 0.02);
        Assert.InRange(Share(c => c.Singular.HasValue && c.Singular.Equals(c.Plural)), 89 / 548.0 - 0.03, 89 / 548.0 + 0.03);
    }

    [Fact]
    public void GenerateCommands_ManyDraws_FollowWals71A()
    {
        Assert.InRange(Share(c => c.ProhibitiveUsesImperative), 295 / 496.0 - 0.03, 295 / 496.0 + 0.03);
        Assert.InRange(Share(c => c.Prohibitive is not null), 328 / 496.0 - 0.03, 328 / 496.0 + 0.03);
    }

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
        var seed = SeedWhere(l => Sounded(l.Commands.Singular) && l.Paradigms.Verbs.Categories.Has<Person>());

        var gloss = Glosses(Say(seed, Eat));

        Assert.Contains("IMP", gloss);
        Assert.DoesNotContain("PRS", gloss);
        Assert.DoesNotContain("2", gloss);
    }

    [Fact]
    public void Realize_BareStemImperative_GlossesWithNoFormOfItsOwn()
    {
        var seed = SeedWhere(l => Bare(l.Commands.Singular));

        var words = Say(seed, Eat).Words;

        Assert.Equal("eat", Assert.Single(words).Word.Morphemes.Single().Gloss);
        Assert.Contains("[IMP", words[0].Gloss);
    }

    [Fact]
    public void Realize_NoImperativeOfItsOwn_UsesTheSecondPerson()
    {
        var seed = SeedWhere(l => !l.Commands.Singular.HasValue && l.Paradigms.Verbs.Categories.Has<Person>());

        var gloss = Glosses(Say(seed, Eat));

        Assert.DoesNotContain("IMP", gloss);
        Assert.Contains("2SG", gloss);
    }

    [Fact]
    public void Realize_SingularAndPlural_SpeakToDifferentNumbers()
    {
        var seed = SeedWhere(l => l.Commands is { Singular: not null, Plural: not null } && !l.Commands.Singular.Equals(l.Commands.Plural));

        Assert.Contains("IMP.SG", Glosses(Say(seed, Eat)));
        Assert.Contains("IMP.PL", Glosses(Say(seed, Eat with { Number = Number.Plural })));
    }

    [Fact]
    public void Realize_DualCommand_TakesThePlural()
    {
        var seed = SeedWhere(l => l.Commands is { Singular: not null, Plural: not null } && !l.Commands.Singular.Equals(l.Commands.Plural));

        Assert.Contains("IMP.PL", Glosses(Say(seed, Eat with { Number = Number.Dual })));
    }

    [Fact]
    public void Realize_ProhibitiveWithItsOwnNegator_SaysThatInsteadOfNegation()
    {
        var seed = SeedWhere(l => l.Commands.Prohibitive is not null);

        var gloss = Glosses(Say(seed, Eat with { Negative = true }));

        Assert.Contains("PROH", gloss);
        Assert.DoesNotContain("NEG", gloss);
    }

    [Fact]
    public void Realize_ProhibitiveWithOrdinaryNegation_SaysNegation()
    {
        var seed = SeedWhere(l => l.Commands.Prohibitive is null);

        Assert.Contains("NEG", Glosses(Say(seed, Eat with { Negative = true })));
    }

    [Fact]
    public void Realize_ProhibitiveNotUsingTheImperative_UsesTheSecondPerson()
    {
        var seed = SeedWhere(l => !l.Commands.ProhibitiveUsesImperative && l.Commands.Singular.HasValue && l.Paradigms.Verbs.Categories.Has<Person>());

        var (command, prohibitive) = (Glosses(Say(seed, Eat)), Glosses(Say(seed, Eat with { Negative = true })));

        Assert.Contains("IMP", command);
        Assert.DoesNotContain("IMP", prohibitive);
        Assert.Contains("2SG", prohibitive);
    }

    [Fact]
    public void Realize_ProhibitiveUsingTheImperative_KeepsIt()
    {
        var seed = SeedWhere(l => l.Commands.ProhibitiveUsesImperative && l.Commands.Singular.HasValue);

        Assert.Contains("IMP", Glosses(Say(seed, Eat with { Negative = true })));
    }

    [Fact]
    public void Realize_Command_SaysNoSubjectEvenWhereSubjectsAreSaid()
    {
        var seed = SeedWhere(l => !l.Pronouns.DropsSubjects);
        var language = Generator.Generate(new Random(seed));

        var words = Syntax.Realize(language, Eat, "").Words;

        Assert.DoesNotContain(words, w => w.Word.Root is { } root && language.Pronouns.Forms.Values.Any(p => p.Root == root));
    }

    [Fact]
    public void Realize_CommandWithAnObjectAndOblique_MarksThemAsUsual()
    {
        var language = Generator.Generate(new Random(3));
        var hunter = new NounPhrase("hunter", Number.Singular);
        var inn = new NounPhrase("inn", Number.Singular);

        var told = Syntax.Realize(language, new Command("give", Number.Singular, hunter, Obliques: [new(Role.Goal, inn)]), "");
        var said = Syntax.Realize(language, new Clause("give", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular), hunter, Obliques: [new(Role.Goal, inn)]), "");

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
                    Assert.NotEmpty(Syntax.Realize(language, new Command("eat", number, new NounPhrase("fish", Number.Plural), negative, [new(Role.Location, new NounPhrase("house", Number.Singular))]), "").Text);
                }
            }
        }
    }
}
