namespace LanguageGenerator;

/// <summary>What an adverb of its own says: when, as 'now' does, or how often or whether still, as 'again' does.</summary>
//a clause file says which, so that nothing has to know English adverbs by name
public enum AdverbKind
{
    When,
    Often,
}

/// <summary>A word of its own saying when or how often, like 'now' or 'again'.</summary>
/*
  each is a root of the lexicon's, coined like any other. Words for where, like 'here' and 'there',
  aren't, since they're demonstratives in most languages, and words for how are made from adjectives,
  as Manner is
 */
public record Adverb(string Meaning, AdverbKind Kind);

/// <summary>How something is done, said with an adjective, as 'carefully' is said with 'careful'.</summary>
public record Manner(Adjective Adjective);

/// <summary>
/// Something said of a whole clause that fills no role: an adverb, how it's done, or 'why' or 'how'
/// asked.
/// </summary>
public union Adverbial(Adverb, Manner, QuestionAdverbial);
