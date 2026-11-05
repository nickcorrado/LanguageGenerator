using System.Text;

namespace LanguageGenerator;

/// <summary>
/// The distributions the generator draws from, each used where it fits the feature.
/// </summary>
public static class Sampling
{
    //a Random for one key, like a word, that depends only on the seed and the key, so drawing for
    //one key never shifts what's drawn for another. It hashes with FNV-1a because .NET randomizes
    //string.GetHashCode per process, and these have to come out the same on every run
    public static Random For(int seed, string key)
    {
        const ulong Offset = 14695981039346656037, Prime = 1099511628211;
        var hash = Offset;
        foreach (var b in Encoding.UTF8.GetBytes($"{seed}:{key}"))
        {
            hash = (hash ^ b) * Prime;
        }
        return new Random((int)(hash ^ (hash >> 32)));
    }

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

    //1 plus a Poisson count with mean lambda, redrawn until it's at most max. The usual model of
    //word length in syllables: a small lambda is commonest at 1, like English, and a larger one
    //peaks at 2 or 3, like Hawaiian or Japanese
    public static int DisplacedPoisson(Random rand, double lambda, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        if (!(lambda >= 0 && double.IsFinite(lambda)))
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "The mean must be finite and not negative.");
        }

        while (true)
        {
            //Knuth's: count uniform draws until their product falls below e^-lambda
            var limit = Math.Exp(-lambda);
            var count = 0;
            for (var product = rand.NextDouble(); product > limit; product *= rand.NextDouble())
            {
                count++;
            }
            if (1 + count <= max)
            {
                return 1 + count;
            }
        }
    }
}
