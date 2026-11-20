namespace LanguageGenerator.Tests;

//the settings of copulas and of how adjectives predicate and agree
public class PredicationSettingTests
{
    [Fact]
    public void Generate_PredicationSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""{ "predicative-adjectives": "Nonverbal", "zero-copula": true, "locational-copula": false, "adjective-agreement": "Phrasal" }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal(PredicativeAdjectives.Nonverbal, l.Predication.Adjectives);
            Assert.True(l.Predication.ZeroCopula);
            Assert.Null(l.Predication.LocationalCopula);
            Assert.Equal(AdjectiveAgreement.Phrasal, l.AdjectiveAgreement);
        });
    }

    [Fact]
    public void Generate_AgreeingVerbalAdjectives_AreWrittenIn()
    {
        //verbal adjectives never agree in a draw, but nothing stops them, so agreement is written in
        var spec = LanguageSpec.Free.Fix(Settings.PredicativeAdjectives, PredicativeAdjectives.Verbal).Fix(Settings.AdjectiveAgreement, AdjectiveAgreement.Agreement);

        Assert.All(Enumerable.Range(0, 5).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal(PredicativeAdjectives.Verbal, l.Predication.Adjectives);
            Assert.Equal(AdjectiveAgreement.Agreement, l.AdjectiveAgreement);
        });
    }

    [Fact]
    public void Generate_LocationalCopulaFixed_HasOneApartFromTheCopula()
    {
        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, LanguageSpec.Free.Fix(Settings.LocationalCopula, true))), l =>
        {
            Assert.NotNull(l.Predication.LocationalCopula);
            Assert.NotEqual(l.Predication.Copula.Root.Form, l.Predication.LocationalCopula.Root.Form);
        });
    }

    [Fact]
    public void Write_LocationalCopula_IsTakenAwayButNeverMade()
    {
        var with = GeneratedLanguages.Sample.Select(l => l.Predication).First(p => p.LocationalCopula is not null);
        var without = with with { LocationalCopula = null };

        Assert.Null(Settings.LocationalCopula.Write(with, [false]).LocationalCopula);
        Assert.Contains("can't be written in", Assert.Throws<InvalidOperationException>(() => Settings.LocationalCopula.Write(without, [true])).Message);
    }
}
