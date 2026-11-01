namespace LanguageGenerator;

public static class Utilities
{
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
