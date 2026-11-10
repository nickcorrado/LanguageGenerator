namespace LanguageGenerator;

/// <summary>
/// The standard of comparison is marked like a place, as Turkish says 'Marta is tall from the men'
/// and others 'tall to the men' or 'tall at the men', with that role's case or adposition.
/// </summary>
public record LocationalComparison(Role Role);

/// <summary>
/// The standard of comparison follows a word of its own, as English than, before or after it as the
/// language's adpositions go, and is in the plain case a subject takes, as in 'taller than I'.
/// </summary>
public record ParticleComparison(Lexeme Than, bool BeforeStandard);

/// <summary>
/// Two clauses side by side, one saying the property of the compared and one denying it of the
/// standard, as in 'Marta is tall, the men are not tall'.
/// </summary>
public record ConjoinedComparison;

/// <summary>How a language says one thing has more of a property than another, as WALS 121A types it.</summary>
//its fourth type, with a verb meaning 'exceed', as in Swahili or Yoruba, is left out for now. None
//of these marks the adjective itself, as English -er does: WALS doesn't count that, and most
//languages of these types don't
public union Comparison(LocationalComparison, ParticleComparison, ConjoinedComparison);

/// <summary>
/// A copular clause saying its subject has more of a property than a standard, as in 'Marta is
/// taller than the men'.
/// </summary>
//the clause's complement is always an adjective
public record Comparative(CopularClause Clause, Argument Standard);
