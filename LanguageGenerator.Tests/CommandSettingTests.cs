namespace LanguageGenerator.Tests;

//the settings of imperatives and prohibitives, and how an imperative is written in from the forms
//a draw has
public class CommandSettingTests
{
    [Fact]
    public void Generate_CommandSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""{ "imperatives": "OneForBoth", "prohibitive-uses-imperative": false, "prohibitive-negator": true }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.NotNull(l.Commands.Singular);
            Assert.Equal(l.Commands.Singular, l.Commands.Plural);
            Assert.False(l.Commands.ProhibitiveUsesImperative);
            Assert.NotNull(l.Commands.Prohibitive);
        });
    }

    [Fact]
    public void Generate_RarestImperatives_HaveThem()
    {
        //only 2 of WALS's 548 languages have an imperative only for the plural
        Assert.All(Enumerable.Range(0, 5).Select(seed => Generator.Generate(seed, LanguageSpec.Free.Fix(Settings.Imperatives, ImperativeForms.PluralOnly))), l =>
        {
            Assert.Null(l.Commands.Singular);
            Assert.True(l.Commands.Plural is { Form.Count: > 0 });
        });
    }

    [Theory]
    [InlineData(ImperativeForms.None)]
    [InlineData(ImperativeForms.SingularOnly)]
    [InlineData(ImperativeForms.PluralOnly)]
    [InlineData(ImperativeForms.OneForBoth)]
    public void Write_ImperativesFromTwoOfTheirOwn_ReuseTheirForms(ImperativeForms forms)
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Commands).First(c => Settings.Imperatives.Get(c) == ImperativeForms.SingularAndPlural);

        var written = Settings.Imperatives.Write(drawn, [forms]);

        Assert.Equal(forms, Settings.Imperatives.Get(written));
        Assert.All(new[] { written.Singular, written.Plural }.OfType<Morpheme>(),
            m => Assert.Contains(m.Form, new[] { drawn.Singular!.Value.Form, drawn.Plural!.Value.Form }));
    }

    [Fact]
    public void Write_TwoImperativesFromOne_SaysItCant()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Commands).First(c => Settings.Imperatives.Get(c) == ImperativeForms.OneForBoth);

        Assert.Contains("can't be written in", Assert.Throws<NoFormException>(() => Settings.Imperatives.Write(drawn, [ImperativeForms.SingularAndPlural])).Message);
    }

    [Fact]
    public void Write_ProhibitiveNegator_IsTakenAwayButNeverMade()
    {
        var with = GeneratedLanguages.Sample.Select(l => l.Commands).First(c => c.Prohibitive is not null);

        Assert.Null(Settings.ProhibitiveNegator.Write(with, [false]).Prohibitive);
        Assert.Contains("can't be written in", Assert.Throws<NoFormException>(() =>
            Settings.ProhibitiveNegator.Write(with with { Prohibitive = null }, [true])).Message);
    }
}
