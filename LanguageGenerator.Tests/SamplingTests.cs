namespace LanguageGenerator.Tests;

public class SamplingTests
{
    [Fact]
    public void TruncatedGeometric_ManyDraws_DecayByAConstantRatio()
    {
        var rand = new Random(1);
        var counts = new int[3];
        for (int i = 0; i < 30000; i++)
        {
            counts[Sampling.TruncatedGeometric(rand, 3, 0.2)]++;
        }

        //each index is 0.8 times as likely as the one before, normalized over the three: 1, 0.8, 0.64
        double[] expected = [1 / 2.44, 0.8 / 2.44, 0.64 / 2.44];
        Assert.All(Enumerable.Range(0, 3), i => Assert.InRange(counts[i] / 30000.0, expected[i] - 0.015, expected[i] + 0.015));
    }

    [Fact]
    public void TruncatedGeometric_ProbabilityOne_AlwaysGivesZero()
    {
        var rand = new Random(1);

        Assert.All(Enumerable.Range(0, 100), _ => Assert.Equal(0, Sampling.TruncatedGeometric(rand, 5, 1)));
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(3, 0)]
    [InlineData(3, -0.1)]
    [InlineData(3, 1.5)]
    [InlineData(3, double.NaN)]
    public void TruncatedGeometric_OutOfRange_Throws(int max, double p)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sampling.TruncatedGeometric(new Random(1), max, p));
    }
}
