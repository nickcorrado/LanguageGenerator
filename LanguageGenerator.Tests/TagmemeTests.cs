namespace LanguageGenerator.Tests;

public class TagmemeTests
{
    [Fact]
    public void JoinGlosses_PersonThenNumber_RunsThemTogether()
    {
        Assert.Equal("3SG.PST", Tagmeme.JoinGlosses([Person.Third, Number.Singular, Tense.Past]));
    }

    [Fact]
    public void JoinGlosses_NumberThenPerson_RunsThemTogether()
    {
        Assert.Equal("SG3", Tagmeme.JoinGlosses([Number.Singular, Person.Third]));
    }

    [Fact]
    public void JoinGlosses_OtherCategories_SeparatesThemWithPeriods()
    {
        Assert.Equal("NOM.PL.M", Tagmeme.JoinGlosses([Case.Nominative, Number.Plural, Gender.Masculine]));
    }

    [Fact]
    public void JoinGlosses_NoTagmemes_IsEmpty()
    {
        Assert.Equal("", Tagmeme.JoinGlosses([]));
    }

    [Fact]
    public void NameAndGloss_OfAnyCategory_ComeFromTheUnderlyingRecord()
    {
        Tagmeme tagmeme = Aspect.Perfective;

        Assert.Equal("Perfective", tagmeme.Name);
        Assert.Equal("PFV", tagmeme.Gloss);
    }
}
