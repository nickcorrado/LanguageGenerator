namespace LanguageGenerator.Tests;

//the settings of numerals and articles, and the rules articles have against isolating morphology
//and missing genders
public class ArticleNumeralSettingTests
{
    [Fact]
    public void Generate_NumeralSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""{ "numeral-base": "Vigesimal", "numerals-before-noun": false, "ordinals": "FirstThenCardinal" }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal(NumeralBase.Vigesimal, l.Numerals.Base);
            Assert.Contains(20, l.Numerals.Words.Keys);
            Assert.False(l.Numerals.BeforeNoun);
            Assert.IsType<FirstThenCardinalOrdinals>(l.Numerals.Ordinals.Value);
        });
    }

    [Fact]
    public void Generate_AgreeingAffixArticle_DrawsMorphologyAndGendersThatAllowIt()
    {
        var spec = SpecFile.Parse("""{ "definite-article": "Affix", "indefinite-article": "One", "article-gender-agreement": true }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            Assert.NotEqual(NounGenders.None, l.Genders.Genders);
            Assert.True(l.Articles.Definite is { Position: ExponentPosition.Prefix or ExponentPosition.Suffix, AgreesInGender: true });
            Assert.Equal(l.Numerals.Words[1].Root, Assert.Single(l.Articles.Indefinite!.Forms.Values));
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_AgreementWithoutADefiniteArticle_HasAnIndefiniteOneThatCanAgree()
    {
        var spec = LanguageSpec.Free.Fix(Settings.ArticleGenderAgreement, true).Fix(Settings.DefiniteArticle, DefiniteArticle.None);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Null(l.Articles.Definite);
            Assert.True(l.Articles.Indefinite?.AgreesInGender);
        });
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "definite-article": "Affix", "fusion": "Isolating" }""", "A definite article can only be an affix" },
        { """{ "indefinite-article": "Affix", "fusion": "Isolating" }""", "An indefinite article can only be an affix" },
        { """{ "article-gender-agreement": true, "genders": "None" }""", "where nouns have genders" },
        { """{ "article-gender-agreement": true, "definite-article": "None", "indefinite-article": ["None", "One"] }""", "where there's one that can" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }

    [Fact]
    public void Write_NoGenderAgreement_KeepsOneGendersFormsWithoutTheGender()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Articles).First(a => a.Definite is { AgreesInGender: true });

        var written = Settings.ArticleGenderAgreement.Write(drawn, [false]);

        Assert.False(written.Definite!.AgreesInGender);
        Assert.Equal(drawn.Definite!.Forms.Count / drawn.Definite.Forms.Keys.Select(k => k.Gender).Distinct().Count(), written.Definite.Forms.Count);
        Assert.All(written.Definite.Forms.Values, m => Assert.DoesNotContain(m.Value switch
        {
            Affix a => a.Grammemes,
            Particle p => p.Grammemes,
            _ => [],
        }, g => g is Gender));
    }

    [Fact]
    public void Write_NoDefiniteArticle_TakesItAway()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Articles).First(a => a.Definite is not null);

        Assert.Null(Settings.DefiniteArticle.Write(drawn, [DefiniteArticle.None]).Definite);
        Assert.Same(drawn.Indefinite, Settings.DefiniteArticle.Write(drawn, [DefiniteArticle.None]).Indefinite);
    }
}
