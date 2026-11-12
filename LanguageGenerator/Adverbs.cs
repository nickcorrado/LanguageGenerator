namespace LanguageGenerator;

/// <summary>A word of its own saying when, how often or how, like 'now', 'again' or 'suddenly'.</summary>
//each is a root of the lexicon's, coined like any other. Words for where, like 'here' and 'there',
//aren't, since they're demonstratives in most languages
public record Adverb(string Meaning);

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
