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

/// <summary>
/// The standard of comparison is the object of a verb meaning 'exceed' or 'pass', said after the
/// property, as Yoruba says 'he is big, exceeds the elephant'.
/// </summary>
//the verb is bare, as the second verb of a serial construction is, and its phrase goes where
//obliques go. It's coined with the comparison, so a clause with the verb exceed itself would get
//the lexicon's own root, not this one
public record ExceedComparison(Lexeme Exceed);

/// <summary>How a language says one thing has more of a property than another, as WALS 121A types it.</summary>
public union Comparison(LocationalComparison, ParticleComparison, ConjoinedComparison, ExceedComparison);

/// <summary>A comparative marked by an affix on the adjective, as English -er or Hungarian -bb.</summary>
//Position is Prefix or Suffix; it goes next to the root, inside any agreement or tense
public record DegreeAffix(Affix Marker, ExponentPosition Position);

/// <summary>A comparative marked by a word beside the adjective, as Turkish daha or Japanese motto 'more'.</summary>
public record DegreeWord(Particle Marker, bool BeforeAdjective);

public union DegreeMarker(DegreeAffix, DegreeWord);

/// <summary>
/// How a language marks an adjective as comparative. Every language can say 'bigger' with no
/// standard, as Turkish says daha büyük 'more big', but only some mark the adjective when a
/// standard is said too, as English does in 'bigger than the house' and Turkish doesn't in evden
/// büyük 'big from the house'.
/// </summary>
public record DegreeMarking(DegreeMarker Marker, bool WithStandard);

/// <summary>
/// 'Like' as a word of its own, before or after what something is likened to as the language's
/// adpositions go, as English like and Turkish gibi, which is in the plain case a subject takes.
/// </summary>
public record SimilativeWord(Lexeme Like, bool BeforeStandard);

/// <summary>
/// 'Like' as an affix on the edge of the phrase, as Quechua -hina is, outside the plain case a
/// subject takes.
/// </summary>
//Position is Prefix or Suffix
public record SimilativeAffix(Affix Marker, ExponentPosition Position);

/// <summary>How a language says something is done like something else, as in 'it shines like water'.</summary>
public union Similative(SimilativeWord, SimilativeAffix);

/// <summary>
/// A copular clause saying its subject has more of a property than a standard, as in 'Marta is
/// taller than the men', or with no standard, more of it than something already known, as in
/// 'Marta is taller'.
/// </summary>
//the clause's complement is always an adjective
public record Comparative(CopularClause Clause, Argument? Standard);
