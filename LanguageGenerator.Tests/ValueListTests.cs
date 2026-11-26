using static LanguageGenerator.Tests.Phrases;
namespace LanguageGenerator.Tests;

public class ValueListTests
{
    [Fact]
    public void Equals_SameItems_IsEqualWithTheSameHashCode()
    {
        ValueList<string> a = ["big", "old"];
        ValueList<string> b = ["big", "old"];

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_OtherItemsOrOrder_IsNotEqual()
    {
        ValueList<string> a = ["big", "old"];

        Assert.NotEqual(a, ["old", "big"]);
        Assert.NotEqual(a, ["big"]);
    }

    [Fact]
    public void Default_IsTheEmptyList()
    {
        Assert.Empty(default(ValueList<string>));
        Assert.Equal(default, (ValueList<string>)[]);
    }

    //the reason it exists: records holding one compare by their items
    [Fact]
    public void InARecord_GivesItValueEquality()
    {
        Assert.Equal(new NounPhrase(new Noun("dog", Number.Plural, [new("big", false)], null, null, null, null, null), null), new NounPhrase(new Noun("dog", Number.Plural, [new("big", false)], null, null, null, null, null), null));
        Assert.NotEqual(new NounPhrase(new Noun("dog", Number.Plural, [new("big", false)], null, null, null, null, null), null), new NounPhrase(new Noun("dog", Number.Plural, [new("small", false)], null, null, null, null, null), null));
        Assert.Equal(BareNoun("dog", Number.Plural), new NounPhrase(new Noun("dog", Number.Plural, [], null, null, null, null, null), null));
    }
}
