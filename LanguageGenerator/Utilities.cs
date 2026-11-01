namespace LanguageGenerator;

public static class Utilities
{
    //implementation borrowed from Mark Rosenfelder at view-source:http://www.zompist.com/gen.html
    public static int PowerLaw(Random rand, int max, double percent)
    {
        for (int r = 0; true; r = (r + 1) % max)
        {
            if (rand.NextDouble() < percent)
            {
                return r;
            }
        }
    }
}
