namespace LanguageGenerator;

/// <summary>
/// What a semantic map of grammatical meanings does with its edges: a language joins some of the
/// meanings an edge connects, and each connected part it's left with shares one marker.
/// </summary>
//any one marker so covers a connected region of the map, as Haspelmath's connectivity hypothesis has it
public static class SemanticMap
{
    /// <summary>The edges a language joins, each with its own chance.</summary>
    public static List<(T First, T Second)> Joined<T>(Random rand, IEnumerable<(T First, T Second, double Weight)> edges) =>
        [.. edges.Where(e => rand.NextDouble() < e.Weight).Select(e => (e.First, e.Second))];

    /// <summary>
    /// The connected parts a map falls into when these edges join its nodes, given in rank order:
    /// each part in rank order, and the parts in the order of their first.
    /// </summary>
    public static List<List<T>> Regions<T>(IReadOnlyList<T> nodes, IEnumerable<(T First, T Second)> joined) where T : notnull
    {
        var rank = nodes.Select((node, i) => (node, i)).ToDictionary(x => x.node, x => x.i);
        var region = nodes.ToDictionary(n => n, n => n);
        T Find(T node) => EqualityComparer<T>.Default.Equals(region[node], node) ? node : region[node] = Find(region[node]);
        foreach (var (first, second) in joined)
        {
            var (a, b) = (Find(first), Find(second));
            if (!EqualityComparer<T>.Default.Equals(a, b))
            {
                region[rank[a] < rank[b] ? b : a] = rank[a] < rank[b] ? a : b;
            }
        }
        return [.. nodes.GroupBy(Find).Select(g => g.ToList()).OrderBy(g => rank[g[0]])];
    }
}
