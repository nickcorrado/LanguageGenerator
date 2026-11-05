namespace LanguageGenerator.Tests;

public class GrammemeTests
{
    [Fact]
    public void JoinGlosses_PersonThenNumber_RunsThemTogether()
    {
        Assert.Equal("3SG.PST", Grammeme.JoinGlosses([Person.Third, Number.Singular, Tense.Past]));
    }

    [Fact]
    public void JoinGlosses_NumberThenPerson_RunsThemTogether()
    {
        Assert.Equal("SG3", Grammeme.JoinGlosses([Number.Singular, Person.Third]));
    }

    [Fact]
    public void JoinGlosses_OtherCategories_SeparatesThemWithPeriods()
    {
        Assert.Equal("NOM.PL.M", Grammeme.JoinGlosses([Case.Nominative, Number.Plural, Gender.Masculine]));
    }

    [Fact]
    public void JoinGlosses_PossessorAgreement_IsPersonAndNumberThenPOSS()
    {
        Assert.Equal("1SG.POSS", Grammeme.JoinGlosses([Person.First, Number.Singular, Possessive.Possessed]));
    }

    [Fact]
    public void JoinGlosses_NoGrammemes_IsEmpty()
    {
        Assert.Equal("", Grammeme.JoinGlosses([]));
    }

    [Fact]
    public void NameAndGloss_OfAnyCategory_ComeFromTheUnderlyingRecord()
    {
        Grammeme grammeme = Aspect.Perfective;

        Assert.Equal("Perfective", grammeme.Name);
        Assert.Equal("PFV", grammeme.Gloss);
    }
}
