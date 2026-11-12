namespace LanguageGenerator;

/// <summary>What an adverb of its own says: when, as 'now' does, or how often or whether still, as 'again' does.</summary>
//a clause file says which, so that nothing has to know English adverbs by name
public enum AdverbKind
{
    When,
    Often,
}

/// <summary>A word of its own saying when or how often, like 'now' or 'again'.</summary>
//each is a root of the lexicon's, coined like any other. Words for where, like 'here' and 'there',
//aren't, since they're demonstratives in most languages, and words for how are made from adjectives,
//as Manner is
public record Adverb(string Meaning, AdverbKind Kind);

/// <summary>How something is done, said with an adjective, as 'carefully' is said with 'careful'.</summary>
public record Manner(Adjective Adjective);

/// <summary>Something said of a whole clause that fills no role: an adverb, or how it's done.</summary>
public union Adverbial(Adverb, Manner);

/// <summary>A manner adverb that's the adjective itself, as German schnell is 'quick' and 'quickly'.</summary>
public record MannerBare;

/// <summary>A manner adverb made from the adjective by an affix, as English -ly and Turkish -ce make them.</summary>
//Position is Prefix or Suffix
public record MannerAffix(Affix Marker, ExponentPosition Position);

/// <summary>
/// A manner adverb marked by a word beside the adjective, as Mandarin de follows it in man-man de
/// 'slowly' and Japanese ni in shizuka ni 'quietly'.
/// </summary>
public record MannerWord(Particle Marker, bool BeforeAdjective);

/// <summary>How a language makes an adverb saying how something is done from an adjective.</summary>
public union MannerMarking(MannerBare, MannerAffix, MannerWord);
