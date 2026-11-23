namespace LanguageGenerator.Tests;

//the settings of what nouns and verbs inflect for, and of pronouns: drawn to match, read back from a
//spec file, and written in only where that needs no new forms
public class InflectionSettingTests
{
    [Fact]
    public void Generate_ParadigmSettingsFixed_HaveThoseCategories()
    {
        var spec = SpecFile.Parse("""{ "noun-number": "SingularDualPlural", "tense": "None", "aspect": true, "verb-agreement": false }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal([Number.Singular, Number.Dual, Number.Plural], l.Paradigms.Nouns.Categories.Single(c => c.Is<Number>()).Values);
            Assert.False(l.Paradigms.Verbs.Categories.Has<Tense>());
            Assert.True(l.Paradigms.Verbs.Categories.Has<Aspect>());
            Assert.False(l.Paradigms.Verbs.Categories.Has<Person>());
        });
    }

    [Fact]
    public void Generate_NoNumberOnNounsAndThreeTenses_HaveThose()
    {
        var spec = LanguageSpec.Free.Fix(Settings.NounNumber, NounNumber.None).Fix(Settings.Tenses, Tenses.PresentPastFuture);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.False(l.Paradigms.Nouns.Categories.Has<Number>());
            Assert.Equal([Tense.Present, Tense.Past, Tense.Future], l.Paradigms.Verbs.Categories.Single(c => c.Is<Tense>()).Values);
        });
    }

    [Fact]
    public void Generate_PronounSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""{ "first-person-plural": "InclusiveExclusive", "gendered-pronouns": "ThirdAndSecond", "drops-subjects": false }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal(FirstPersonPlural.InclusiveExclusive, l.Pronouns.FirstPersonPlural);
            Assert.Contains(new Pronoun(Person.First, Number.Plural, Clusivity.Inclusive), l.Pronouns.Forms.Keys);
            Assert.Equal(PronounGender.ThirdAndSecond, l.Pronouns.GenderedPronouns);
            Assert.Contains(new Pronoun(Person.Second, Number.Singular, Gender: Gender.Feminine), l.Pronouns.Forms.Keys);
            Assert.False(l.Pronouns.DropsSubjects);
        });
    }

    [Fact]
    public void Generate_SettingsTheSeedAlreadyHas_KeepItsLanguage()
    {
        var free = Generator.Generate(3, LanguageSpec.Free);
        var spec = LanguageSpec.Free
            .Fix(Settings.Tenses, Settings.Tenses.Get(free.Paradigms.Verbs))
            .Fix(Settings.FirstPersonPlural, free.Pronouns.FirstPersonPlural);

        var language = Generator.Generate(3, spec);

        static string Spelled(IEnumerable<Phoneme> form) => string.Concat(form.Select(p => p.IpaSymbol));
        static IEnumerable<string> Markers(Paradigm p) => p.Slots.SelectMany(s => s.Exponents.Select(e => Spelled(e.Exponent.Form)));
        static IEnumerable<string> Pronouns(PronounSystem p) => p.Forms.Select(f => $"{f.Key.Gloss} {Spelled(f.Value.Root.Form)}");
        Assert.Equal(Markers(free.Paradigms.Verbs), Markers(language.Paradigms.Verbs));
        Assert.Equal(Pronouns(free.Pronouns), Pronouns(language.Pronouns));
    }

    [Fact]
    public void Write_DropsSubjects_KeepsTheForms()
    {
        var drawn = GeneratedLanguages.Sample[0].Pronouns;

        var written = Settings.DropsSubjects.Write(drawn, [!drawn.DropsSubjects]);

        Assert.Equal(!drawn.DropsSubjects, written.DropsSubjects);
        Assert.Same(drawn.Forms, written.Forms);
    }

    [Fact]
    public void Write_ValueNeedingNewForms_SaysItCant()
    {
        var language = GeneratedLanguages.Sample[0];
        var other = language.Pronouns.FirstPersonPlural == FirstPersonPlural.Plain ? FirstPersonPlural.InclusiveExclusive : FirstPersonPlural.Plain;

        Assert.Contains("can't be written in", Assert.Throws<NoFormException>(() => Settings.FirstPersonPlural.Write(language.Pronouns, [other])).Message);
        Assert.Contains("can't be written in", Assert.Throws<NoFormException>(() => Settings.VerbAgreement.Write(language.Paradigms.Verbs, [!language.Paradigms.Verbs.Categories.Has<Person>()])).Message);
    }
}
