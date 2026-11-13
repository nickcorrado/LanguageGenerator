namespace LanguageGenerator;

/// <summary>
/// A word before or after a noun phrase saying its role, glossed by its English meaning.
/// </summary>
public record Adposition(Lexeme Lexeme, AdpositionType Type)
{
    public string Gloss => Lexeme.Root.Meaning;

    public override string ToString() => $"{Type} '{Gloss}'";
}

/// <summary>
/// How a language marks one role: the case its noun takes, and the adposition beside it, if it has
/// one. An adposition governs the case beside it, as German mit takes the dative, and a role with
/// neither is recovered from word order, as English's core arguments are, or from context.
/// </summary>
//a null case is the plain one a noun is cited in
//TODO cases are a fixed list named for one use each, and a role with no case of its own gets an
//adposition. Real cases cover connected regions of a semantic map of roles (Haspelmath): Latin's
//ablative covers source, instrument and location, and Greek, with no ablative, puts more on its
//dative. Drawing a language's cases as a partition of that map would let a missing case fall back on
//its neighbor's, as Features does for number and tense. Merging neighbors is also how a case is lost
//over time, as with Late Latin's locative, if languages ever get histories
public record RoleMarker(Case? Case, Adposition? Adposition)
{
    public override string ToString() => (Case, Adposition) switch
    {
        (null, null) => "unmarked",
        ({ } c, null) => c.Gloss,
        (null, { } a) => a.ToString(),
        ({ } c, { } a) => $"{a} + {c.Gloss}",
    };
}
