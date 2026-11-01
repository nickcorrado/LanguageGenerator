namespace LanguageGenerator.Tests;

public class SamplingTests
{
    [Fact]
    public void Categorical_ManyDraws_FollowTheWeights()
    {
        var rand = new Random(1);
        (string Value, int Weight)[] weights = [("a", 3), ("b", 1)];

        var share = Enumerable.Range(0, 20000).Count(_ => Sampling.Categorical(rand, weights) == "a") / 20000.0;

        Assert.InRange(share, 0.735, 0.765);
    }

    [Fact]
    public void Categorical_ZeroWeight_IsNeverDrawn()
    {
        var rand = new Random(1);
        (string Value, int Weight)[] weights = [("a", 1), ("never", 0), ("b", 1)];

        Assert.DoesNotContain("never", Enumerable.Range(0, 2000).Select(_ => Sampling.Categorical(rand, weights)));
    }

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

    [Fact]
    public void DisplacedPoisson_ManyDraws_MatchOnePlusAPoissonCount()
    {
        var rand = new Random(1);
        var counts = new int[21];
        for (int i = 0; i < 30000; i++)
        {
            counts[Sampling.DisplacedPoisson(rand, 1, 20)]++;
        }

        //with a mean of 1, P(0) = P(1) = 1/e and P(2) = 1/2e
        Assert.InRange(counts[1] / 30000.0, 0.353, 0.383);
        Assert.InRange(counts[2] / 30000.0, 0.353, 0.383);
        Assert.InRange(counts[3] / 30000.0, 0.169, 0.199);
    }

    [Fact]
    public void DisplacedPoisson_MeanZero_AlwaysGivesOne()
    {
        var rand = new Random(1);

        Assert.All(Enumerable.Range(0, 100), _ => Assert.Equal(1, Sampling.DisplacedPoisson(rand, 0, 5)));
    }

    [Fact]
    public void DisplacedPoisson_LargeMean_NeverExceedsTheMax()
    {
        var rand = new Random(1);

        Assert.All(Enumerable.Range(0, 1000), _ => Assert.InRange(Sampling.DisplacedPoisson(rand, 4, 3), 1, 3));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, -0.5)]
    [InlineData(3, double.NaN)]
    [InlineData(3, double.PositiveInfinity)]
    public void DisplacedPoisson_OutOfRange_Throws(int max, double lambda)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sampling.DisplacedPoisson(new Random(1), lambda, max));
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
