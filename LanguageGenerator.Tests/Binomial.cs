namespace LanguageGenerator.Tests;

/*
  bounds on how many of n independent trials succeed at rate p, for the few tests that check a share.
  A bound is set so that trials at exactly rate p fall past it with at most the chance given, so a
  test states its false-failure rate instead of picking a range by eye. The seeds are fixed, so a
  test never flakes; the rate says how likely a change that leaves the odds alone, like drawing in
  another order, is to fail it anyway
 */
internal static class Binomial
{
    //the most successes there are, but for a chance of at most falseFailure
    public static int Most(int n, double p, double falseFailure)
    {
        var pmf = Pmf(n, p);
        var tail = 0.0;
        for (int k = n; k >= 0; k--)
        {
            tail += pmf(k);
            if (tail > falseFailure)
            {
                return k;
            }
        }
        throw new InvalidOperationException("The probabilities sum to less than the false-failure rate.");
    }

    //the fewest successes there are, but for a chance of at most falseFailure
    public static int Least(int n, double p, double falseFailure)
    {
        var pmf = Pmf(n, p);
        var tail = 0.0;
        for (int k = 0; k <= n; k++)
        {
            tail += pmf(k);
            if (tail > falseFailure)
            {
                return k;
            }
        }
        throw new InvalidOperationException("The probabilities sum to less than the false-failure rate.");
    }

    //both bounds, each taking half the false-failure rate
    public static (int Least, int Most) Range(int n, double p, double falseFailure) =>
        (Least(n, p, falseFailure / 2), Most(n, p, falseFailure / 2));

    //in logs, since a single outcome of thousands of trials is too unlikely for a double
    private static Func<int, double> Pmf(int n, double p)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(p);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(p, 1);
        var logFactorial = new double[n + 1];
        for (int i = 1; i <= n; i++)
        {
            logFactorial[i] = logFactorial[i - 1] + Math.Log(i);
        }
        return k => Math.Exp(logFactorial[n] - logFactorial[k] - logFactorial[n - k] + k * Math.Log(p) + (n - k) * Math.Log(1 - p));
    }
}
