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
        Assert.Equal(new NounPhrase("dog", Number.Plural, [new("big")]), new NounPhrase("dog", Number.Plural, [new("big")]));
        Assert.NotEqual(new NounPhrase("dog", Number.Plural, [new("big")]), new NounPhrase("dog", Number.Plural, [new("small")]));
        Assert.Equal(new NounPhrase("dog", Number.Plural), new NounPhrase("dog", Number.Plural, []));
    }
}
