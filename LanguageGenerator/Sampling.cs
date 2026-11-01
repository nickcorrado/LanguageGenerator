namespace LanguageGenerator;

//the distributions the generator draws from, each used where it fits the feature
public static class Sampling
{
    //a value, with probability proportional to its weight: usually how many languages in a WALS
    //sample have it
    public static T Categorical<T>(Random rand, (T Value, int Weight)[] weights)
    {
        var roll = rand.Next(weights.Sum(w => w.Weight));
        foreach (var (value, weight) in weights)
        {
            if (roll < weight)
            {
                return value;
            }
            roll -= weight;
        }
        throw new System.Diagnostics.UnreachableException();
    }

    //an index below max, where 0 is likeliest and each index after it is (1 - p) times as likely:
    //a geometric distribution truncated at max. Wrapping past max instead of starting over keeps
    //the truncation exact. Borrowed from Mark Rosenfelder at view-source:http://www.zompist.com/gen.html,
    //which calls it a power law, though it decays exponentially rather than as a power
    public static int TruncatedGeometric(Random rand, int max, double p)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        //with p = 0 no draw could ever succeed
        if (!(p > 0 && p <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(p), p, "The probability must be in (0, 1].");
        }

        for (int r = 0; true; r = (r + 1) % max)
        {
            if (rand.NextDouble() < p)
            {
                return r;
            }
        }
    }
}
