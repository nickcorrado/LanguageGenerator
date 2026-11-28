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
/*
  the verb is bare, as the second verb of a serial construction is, and its phrase goes where
  obliques go. It's coined with the comparison, so a clause with the verb exceed itself would get
  the lexicon's own root, not this one
 */
public record ExceedComparison(Lexeme Exceed);

/// <summary>How a language says one thing has more of a property than another, as WALS 121A types it.</summary>
public union Comparison(LocationalComparison, ParticleComparison, ConjoinedComparison, ExceedComparison);

/// <summary>Which kind of comparison a language has, as a spec names it.</summary>
public enum ComparisonKind
{
    Locational,
    Exceed,
    Conjoined,
    Particle,
}

/// <summary>Whether a comparative or a similative is an affix or a word of its own, as a spec names it.</summary>
public enum MarkerKind
{
    Affix,
    Word,
}

/// <summary>
/// How a language marks an adjective as comparative: an affix next to its root, inside any
/// agreement or tense, as English -er or Hungarian -bb, or a word beside it, as Turkish daha or
/// Japanese motto 'more'. Every language can say 'bigger' with no standard, as Turkish says daha
/// büyük 'more big', but only some mark the adjective when a standard is said too, as English does
/// in 'bigger than the house' and Turkish doesn't in evden büyük 'big from the house'.
/// </summary>
public record DegreeMarking(Exponent Marker, bool WithStandard);

/// <summary>
/// 'Like' as a word of its own, before or after what something is likened to as the language's
/// adpositions go, as English like and Turkish gibi, which is in the plain case a subject takes.
/// </summary>
public record SimilativeWord(Lexeme Like, bool BeforeStandard);

/// <summary>
/// 'Like' as an affix on the edge of the phrase, as Quechua -hina is, outside the plain case a
/// subject takes.
/// </summary>
public record SimilativeAffix(Bound Marker);

/// <summary>How a language says something is done like something else, as in 'it shines like water'.</summary>
public union Similative(SimilativeWord, SimilativeAffix);

/// <summary>
/// How much of its property an adjective says, as the degree words and markers of comparison say
/// it: much of it, as in 'very big', or more of it than something else has, as in 'bigger'.
/// </summary>
public union Grade(Intensified, Comparative);

/// <summary>Much of a property, as English very says, one of Bolinger's degree words.</summary>
public record Intensified;

/// <summary>
/// More of a property than a standard has, as in 'Marta is taller than the men', or with no
/// standard, more of it than something already known, as in 'Marta is taller', and by how much, if
/// it says, as in 'much taller'.
/// </summary>
//the comparee is what the adjective is said of, so it isn't written here
public record Comparative(Argument? Standard, Differential? Differential);

/// <summary>By how much a comparative's comparee has more of its property than the standard.</summary>
public union Differential(Much);

/// <summary>By much, as in 'much bigger', which many languages say apart from 'very', as German viel größer is.</summary>
public record Much;
