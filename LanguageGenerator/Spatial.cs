namespace LanguageGenerator;

/// <summary>
/// Where something is relative to the place a spatial phrase names, as 'in', 'on' and 'under' say,
/// apart from whether anything moves, which the phrase's role says: a location, a goal, a source or
/// a route. So 'into' is In with a goal, 'out of' In with a source, and 'from under' Under with a source.
/// </summary>
//At is the plain relation, 'at' or 'by', which says no more than the role does
public enum Configuration
{
    At,
    In,
    On,
    Under,
    Over,
    Behind,
    InFront,
    Beside,
    Between,
    Around,
    Against,
}

/// <summary>
/// A configuration expressed in the noun, as a category of its own beside case, as Lezgian and
/// Tsez have one, and as Finnish and Hungarian have fused with their local cases: Finnish talo-ssa
/// 'in the house', talo-lla 'at the house'.
/// </summary>
/*
  the glosses are the ones grammars of Daghestanian languages use, where these are commonest, except
  SUPRA, coined here for above without touching, which those grammars rarely need, and RETRO for
  behind, since their POST is already 'after' here. None, the form for everything that isn't spatial,
  has no gloss, so it never shows in one
 */
public record Localization(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Localization None { get; } = new("None", "");
    public static Localization In { get; } = new("In", "IN");
    public static Localization On { get; } = new("On", "SUPER");
    public static Localization Under { get; } = new("Under", "SUB");
    public static Localization Over { get; } = new("Over", "SUPRA");
    public static Localization Behind { get; } = new("Behind", "RETRO");
    public static Localization InFront { get; } = new("InFront", "ANTE");
    public static Localization Beside { get; } = new("Beside", "APUD");
    public static Localization Between { get; } = new("Between", "INTER");
    public static Localization Around { get; } = new("Around", "CIRC");
    public static Localization Against { get; } = new("Against", "CONT");

    public static Localization For(Configuration configuration) => configuration switch
    {
        Configuration.In => In,
        Configuration.On => On,
        Configuration.Under => Under,
        Configuration.Over => Over,
        Configuration.Behind => Behind,
        Configuration.InFront => InFront,
        Configuration.Beside => Beside,
        Configuration.Between => Between,
        Configuration.Around => Around,
        Configuration.Against => Against,
        _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration, "At is no localization: it's the plain role."),
    };
}

/// <summary>
/// Configurations that share one marker, named for the first of them. A region with At in it has
/// no marker at all: its configurations are said as plainly as 'at'.
/// </summary>
public record ConfigurationRegion(IReadOnlyList<Configuration> Configurations)
{
    public Configuration Head => Configurations[0];

    public bool Contains(Configuration configuration) => Configurations.Contains(configuration);
}

/// <summary>
/// A semantic map of configurations: which languages mark alike, and how often. Levinson and
/// Meira's map of topological relations puts At in the middle, with In, On and its kinds of contact
/// around it, and Under and the relations of front and back apart.
/// </summary>
//none of the weights is counted: they're guesses from the syncretisms languages are known for
public static class ConfigurationMap
{
    /// <summary>The configurations, At first, so a region with At in it is named for it.</summary>
    public static IReadOnlyList<Configuration> Configurations { get; } = Enum.GetValues<Configuration>();

    public static IReadOnlyList<(Configuration First, Configuration Second, double Weight)> Edges { get; } =
    [
        //a general locative covers in and on, as Japanese ni and Tzeltal ta do, leaving them to
        //context or to a word for 'inside' or 'top' when it matters
        (Configuration.At, Configuration.In, 0.2),
        (Configuration.At, Configuration.On, 0.2),
        //English at the fire, by the fire
        (Configuration.At, Configuration.Beside, 0.25),
        //Spanish en, Modern Greek se
        (Configuration.In, Configuration.On, 0.15),
        //among as in, as English in the trees
        (Configuration.In, Configuration.Between, 0.15),
        //Japanese ue, Mandarin shàng, for on and above alike
        (Configuration.On, Configuration.Over, 0.35),
        //German an, for what's attached or leaning: the picture on the wall, the ladder against it
        (Configuration.On, Configuration.Against, 0.3),
    ];

    /// <summary>The English gloss of the adposition a region would have, by its head.</summary>
    public static string GlossFor(Configuration head) => head switch
    {
        Configuration.In => "in",
        Configuration.On => "on",
        Configuration.Under => "under",
        Configuration.Over => "over",
        Configuration.Behind => "behind",
        Configuration.InFront => "in.front.of",
        Configuration.Beside => "beside",
        Configuration.Between => "between",
        Configuration.Around => "around",
        Configuration.Against => "against",
        _ => throw new ArgumentOutOfRangeException(nameof(head), head, "At has no marker of its own."),
    };

    /// <summary>
    /// The nouns a region's relational noun can be, by its head, each with its weight. Most are words
    /// for parts of objects, and the rest the body parts that most often become them, as Heine and
    /// Svorou find: 'back' for behind, 'face' for in front, 'head' for on top and 'belly' for inside.
    /// </summary>
    //the weights are guesses
    public static IReadOnlyList<(string Meaning, int Weight)> NounsFor(Configuration head) => head switch
    {
        Configuration.In => [("inside", 6), ("belly", 2), ("heart", 1)],
        Configuration.On => [("top", 6), ("head", 2), ("back", 1)],
        Configuration.Under => [("bottom", 6), ("foot", 2), ("belly", 1)],
        Configuration.Over => [("top", 4), ("head", 2)],
        Configuration.Behind => [("back", 7)],
        Configuration.InFront => [("front", 5), ("face", 3), ("eye", 1)],
        Configuration.Beside => [("side", 6), ("flank", 1)],
        Configuration.Between => [("middle", 7), ("waist", 1)],
        Configuration.Around => [("surroundings", 4), ("edge", 1)],
        Configuration.Against => [("face", 2), ("side", 1)],
        _ => throw new ArgumentOutOfRangeException(nameof(head), head, "At has no marker of its own."),
    };

    /// <summary>The regions the map falls into when these edges join their configurations.</summary>
    public static List<ConfigurationRegion> Regions(IEnumerable<(Configuration First, Configuration Second)> joined) =>
        [.. SemanticMap.Regions(Configurations, joined).Select(c => new ConfigurationRegion(c))];
}

/// <summary>
/// A word for a configuration, beside the noun, as English 'under' and Latin sub are. It takes the
/// case its role takes, if the role has one, as sub mensā 'under the table' takes the ablative for
/// where and sub mensam the accusative for where to, and Governs otherwise.
/// </summary>
public record SpatialAdposition(Adposition Adposition, Case? Governs);

/// <summary>
/// A noun naming part of the place, possessed by it, which then takes the role's marker as any noun
/// would, as Japanese tsukue no shita ni is 'desk GEN bottom LOC' for 'under the desk'.
/// </summary>
public record RelationalNoun(string Meaning);

/// <summary>A configuration as a localization in the noun, beside its case.</summary>
public record SpatialCase(Localization Localization);

/// <summary>How a language marks a configuration.</summary>
public union ConfigurationMarker(SpatialAdposition, RelationalNoun, SpatialCase)
{
    public override string ToString() => this switch
    {
        SpatialAdposition a when a.Governs is { } governed => $"{a.Adposition} (+ {governed.Gloss} where the role has no case)",
        SpatialAdposition a => a.Adposition.ToString(),
        RelationalNoun n => $"relational noun '{n.Meaning}'",
        SpatialCase c => $"localization {c.Localization.Gloss}",
    };
}

/// <summary>
/// How a language marks a configuration's region: as plainly as At, by an adposition, by a relational
/// noun or by a localization in the noun.
/// </summary>
public enum ConfigurationStrategy
{
    At,
    Adposition,
    RelationalNoun,
    Case,
}

/// <summary>The edges of the configuration map a language joins, and the regions that leaves it.</summary>
public record ConfigurationRegions(IReadOnlyList<(Configuration First, Configuration Second)> Joined)
{
    //worked out once for each set of joined edges there is, since searching for regions that meet
    //a spec's conditions asks for the same ones many times over
    private static readonly SemanticMap.RegionCache<Configuration, ConfigurationRegion> Cache = new(ConfigurationMap.Edges, ConfigurationMap.Regions);

    public IReadOnlyList<ConfigurationRegion> Regions => Cache.Regions(Joined);
}

/// <summary>A spatial adposition before its word is coined: just the case it governs.</summary>
public record PlannedAdposition(Case? Governs);

/// <summary>How a language will mark a configuration, decided before any word for it is coined.</summary>
public union PlannedMarker(PlannedAdposition, RelationalNoun, SpatialCase)
{
    public ConfigurationStrategy Strategy => this switch
    {
        PlannedAdposition => ConfigurationStrategy.Adposition,
        RelationalNoun => ConfigurationStrategy.RelationalNoun,
        SpatialCase => ConfigurationStrategy.Case,
    };
}

/// <summary>
/// Everything decided about a language's configurations: which it tells apart, the way of marking
/// them it prefers, and how it marks each, as a marker for every configuration of a region that
/// has one. A configuration missing from Markers is said like At. CasesAllowed is whether the
/// language could mark them by case at all.
/// </summary>
public record ConfigurationPlan(ConfigurationRegions Map, ConfigurationStrategy Preferred, IReadOnlyDictionary<Configuration, PlannedMarker> Markers, bool CasesAllowed)
{
    public ConfigurationStrategy StrategyFor(Configuration configuration) =>
        Markers.TryGetValue(configuration, out var marker) ? marker.Strategy : ConfigurationStrategy.At;

    /// <summary>
    /// This plan with a configuration's region marked this way, as a spec writes in a strategy no
    /// draw gave. Whether a region is said like At is up to its edges, so At changes nothing, and
    /// neither does a marker for a region that has At, nor a case where the language can't have one;
    /// the spec's rules keep what came before agreeing.
    /// </summary>
    /*
      an adposition governs what the language's other spatial adpositions do, or nothing; a relational
      noun is the region's commonest one no other region has, or its commonest if they all do, as
      'face' says both in front and against in many languages
     */
    public ConfigurationPlan WithStrategy(Configuration configuration, ConfigurationStrategy strategy)
    {
        var region = Map.Regions.First(r => r.Contains(configuration));
        if (strategy == ConfigurationStrategy.At || region.Contains(Configuration.At) || (strategy == ConfigurationStrategy.Case && !CasesAllowed))
        {
            return this;
        }
        var others = Markers.Where(m => !region.Contains(m.Key)).Select(m => m.Value).ToList();
        var nouns = ConfigurationMap.NounsFor(region.Head).Select(n => n.Meaning).ToList();
        PlannedMarker marker = strategy switch
        {
            ConfigurationStrategy.Adposition => new PlannedAdposition(others.Select(m => m.Value).OfType<PlannedAdposition>().FirstOrDefault()?.Governs),
            ConfigurationStrategy.RelationalNoun => new RelationalNoun(nouns.FirstOrDefault(n => !others.Any(m => m.Value is RelationalNoun r && r.Meaning == n)) ?? nouns[0]),
            _ => new SpatialCase(Localization.For(region.Head)),
        };
        var markers = new Dictionary<Configuration, PlannedMarker>(Markers);
        foreach (var c in region.Configurations)
        {
            markers[c] = marker;
        }
        return this with { Markers = markers };
    }
}

/// <summary>
/// How a language says where something is relative to a place: which configurations it tells
/// apart, and how it marks each, with the words its plan called for. A configuration missing from
/// Markers is said like At.
/// </summary>
public record Configurations(ConfigurationPlan Plan, IReadOnlyDictionary<Configuration, ConfigurationMarker> Markers)
{
    public IReadOnlyList<ConfigurationRegion> Regions => Plan.Map.Regions;

    /// <summary>The localizations the language's nouns inflect for, beside None.</summary>
    public IReadOnlyList<Localization> Localizations =>
        [.. Regions.Where(r => Markers.ContainsKey(r.Head)).Select(r => Markers[r.Head].Value).OfType<SpatialCase>().Select(c => c.Localization)];
}
