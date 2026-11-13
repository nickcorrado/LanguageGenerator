namespace LanguageGenerator;

/// <summary>
/// Roles that share one marker, a case or an adposition, named for the highest ranked of them.
/// </summary>
//Roles is in rank order, so Head is always the first
public record RoleRegion(IReadOnlyList<Role> Roles)
{
    public Role Head => Roles[0];

    /// <summary>
    /// The case the region would have, named for its head, as a dative covering goals is still DAT.
    /// Null for a region of agents or times alone, which only an adposition marks.
    /// </summary>
    public Case? Case => RoleMap.CaseFor(Head);

    /// <summary>The English gloss of the adposition the region would have.</summary>
    //'with' covers instruments along with companions, as English with does, and 'using' them alone
    public string Gloss => Head == Role.Instrument && Roles.Contains(Role.Companion) ? "with" : RoleMap.GlossFor(Head);

    public bool Contains(Role role) => Roles.Contains(role);
}

/// <summary>
/// A semantic map of the oblique roles: which can share a marker, and how often. Any one marker covers
/// a connected region of it, as Haspelmath's connectivity hypothesis has it.
/// </summary>
//the core roles are left to alignment, and the addressee to the vocative. A standard of comparison
//hangs off the source, goal or location without heading a region of its own, which is what a
//LocationalComparison's role says
public static class RoleMap
{
    /// <summary>
    /// The oblique roles, in the order of Blake's case hierarchy: a language with a case lower down
    /// tends to have the ones above it, and a region is named for its highest.
    /// </summary>
    public static IReadOnlyList<Role> Roles { get; } =
        [Role.Possessor, Role.Recipient, Role.Location, Role.Source, Role.Instrument, Role.Companion, Role.Goal, Role.Route, Role.Agent, Role.Time];

    /// <summary>
    /// Each pair of roles languages mark alike, with the chance that a language does. Only the first
    /// is counted, by WALS 52A; the others are guesses from the syncretisms languages are known for.
    /// </summary>
    public static IReadOnlyList<(Role First, Role Second, double Weight)> Edges { get; } =
    [
        //76 of 289 languages, as English with
        (Role.Companion, Role.Instrument, 76.0 / 289),
        //English to, French à, Spanish a
        (Role.Recipient, Role.Goal, 0.5),
        //Japanese ni
        (Role.Goal, Role.Location, 0.35),
        //Greek's genitive, Romance de, English of from off
        (Role.Source, Role.Possessor, 0.2),
        //Modern Greek's genitive, Hungarian -nak
        (Role.Possessor, Role.Recipient, 0.15),
        //Japanese de, for where something is done and with what
        (Role.Instrument, Role.Location, 0.15),
        //rare: a source is usually kept apart
        (Role.Location, Role.Source, 0.1),
        //an ablative of means, as in Latin
        (Role.Instrument, Role.Source, 0.1),
        //a passive's agent is marked like an instrument, as in Russian, like a source, as German von
        //and Latin ab mark it, or like a place, as Japanese ni does
        (Role.Agent, Role.Instrument, 0.3),
        (Role.Agent, Role.Source, 0.3),
        (Role.Agent, Role.Location, 0.15),
        //a time is marked like a place in most languages, as English at night, Turkish gece-de
        //'night-LOC' and Japanese ni; a guess, since WALS doesn't count it
        (Role.Time, Role.Location, 0.6),
        //a route is marked like a place in many languages, like an instrument in Russian, which
        //says shel lesom 'went forest-INS' for 'went through the forest', and like a source in some
        //Australian languages; guesses, since WALS doesn't count it
        (Role.Route, Role.Location, 0.2),
        (Role.Route, Role.Instrument, 0.15),
        (Role.Route, Role.Source, 0.15),
    ];

    public static int Rank(Role role) => Roles.ToList().IndexOf(role) is var rank and >= 0
        ? rank
        : throw new ArgumentOutOfRangeException(nameof(role), role, "Only oblique roles are on the map.");

    //an agent has no case of its own here: an agentive case is rare, and a passive's agent alone in
    //its region is marked by an adposition, as English by is. So is a time apart from places, which a
    //temporal case marks in only a few languages
    public static Case? CaseFor(Role head) => head switch
    {
        Role.Possessor => Case.Genitive,
        Role.Recipient => Case.Dative,
        Role.Location => Case.Locative,
        Role.Source => Case.Ablative,
        Role.Instrument => Case.Instrumental,
        Role.Companion => Case.Comitative,
        Role.Goal => Case.Allative,
        Role.Route => Case.Perlative,
        Role.Agent => null,
        Role.Time => null,
        _ => throw new ArgumentOutOfRangeException(nameof(head), head, "Only oblique roles are on the map."),
    };

    public static string GlossFor(Role head) => head switch
    {
        Role.Possessor => "of",
        Role.Recipient => "to",
        Role.Location => "at",
        Role.Source => "from",
        Role.Instrument => "using",
        Role.Companion => "with",
        Role.Goal => "toward",
        Role.Route => "via",
        Role.Agent => "by",
        Role.Time => "during",
        _ => throw new ArgumentOutOfRangeException(nameof(head), head, "Only oblique roles are on the map."),
    };

    /// <summary>The regions the map falls into when these edges join their roles: its connected parts.</summary>
    //in rank order of their heads, and each region's roles in rank order
    public static List<RoleRegion> Regions(IEnumerable<(Role First, Role Second)> joined) =>
        [.. SemanticMap.Regions(Roles, joined).Select(roles => new RoleRegion(roles))];
}
