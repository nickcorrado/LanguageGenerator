namespace LanguageGenerator;

/// <summary>
/// How near its head a modifier goes, nearest first: what a verb's meaning calls for, how, where, how
/// often or how long, when, and why.
/// </summary>
//languages largely agree on which modifiers go nearest their head, and differ mostly in which side
//of it they put them, so the order on one side is the mirror of the order on the other. English
//says 'she sang beautifully in the hall every night last year' after the verb, and an OV language
//says 'last year every night in the hall beautifully sang' before it, as Japanese and Turkish do,
//which is Cinque's hierarchy of adverbs. Adjectives work the same way, with colour and material
//nearer the noun than size, and size nearer than opinion, as Dixon and Scontras et al. find, so
//'big gray wolf' is 'wolf gray big' after the noun in most languages, though not in all. A clause
//file writes adjectives in English order, before the noun, which already says how near each goes,
//so they need no ranks. Modifiers of a rank keep the order they're written in
public enum Closeness
{
    //what the verb's meaning calls for, as giving does a recipient and going a goal
    Complement,
    Manner,
    Place,
    //how often or how long, as 'again' and 'for a year'
    Extent,
    Time,
    Reason,
}

public static class ModifierOrder
{
    /// <summary>How near the verb a phrase in this role goes.</summary>
    public static Closeness Of(Role role) => role switch
    {
        Role.Recipient or Role.Goal or Role.Source or Role.Agent => Closeness.Complement,
        Role.Similative or Role.Instrument or Role.Companion => Closeness.Manner,
        Role.Location => Closeness.Place,
        Role.Duration => Closeness.Extent,
        Role.Time => Closeness.Time,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Only an oblique role goes where obliques go."),
    };

    /// <summary>How near the verb an adverb goes, by what it says.</summary>
    public static Closeness Of(Adverbial adverbial) => adverbial switch
    {
        Manner => Closeness.Manner,
        Adverb { Kind: AdverbKind.Often } => Closeness.Extent,
        Adverb => Closeness.Time,
    };

    /// <summary>
    /// Modifiers in the order they're said, on one side of their head: nearest first after it, and
    /// mirrored before it, so the nearest is always beside it. Those of a rank keep their order.
    /// </summary>
    public static IReadOnlyList<Word> Arranged(IEnumerable<(Closeness Rank, IReadOnlyList<Word> Words)> modifiers, bool beforeHead)
    {
        var ranked = modifiers.Where(m => m.Words.Count > 0).GroupBy(m => m.Rank).OrderBy(g => g.Key).ToList();
        if (beforeHead)
        {
            ranked.Reverse();
        }
        return [.. ranked.SelectMany(g => g.SelectMany(m => m.Words))];
    }
}
