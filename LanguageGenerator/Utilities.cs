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

    //implementation from SO https://stackoverflow.com/a/69455054/8429180
    public static T[] Shuffle<T>(Random rand, T[] items)
    {
        for (int i = 0; i < items.Length - 1; i++)
        {
            int pos = rand.Next(i, items.Length);
            (items[pos], items[i]) = (items[i], items[pos]);
        }
        return items;
    }

    public static IList<T> Shuffle<T>(Random rand, IList<T> items)
    {
        for (int i = 0; i < items.Count - 1; i++)
        {
            int pos = rand.Next(i, items.Count);
            (items[pos], items[i]) = (items[i], items[pos]);
        }
        return items;
    }
}
