namespace LanguageGenerator;

/// <summary>
/// A language's passive: a marker on the verb, an affix next to its root, as Turkish -il in
/// gör-ül-dü 'was seen', or in an isolating language a word beside it.
/// </summary>
//WALS 107A counts passives without telling them apart by form, and the generator has no
//participles, so an English-style passive with be and a participle is made this way too
public record Passive(Exponent Exponent)
{
    /// <summary>The verb's words with the passive marked on them.</summary>
    //voice sits closer to the root than tense or agreement, as it nearly always does
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> verb) => Exponent.Place(verb, Exponent.Host(verb), Reach.Root);
}

/// <summary>
/// A clause about what's done to its patient, as in 'each light is carried by a small figure', with
/// who does it, if anyone says.
/// </summary>
public record PassiveClause(string Verb, Tense Tense, Aspect Aspect, Argument Patient, Argument? Agent, bool Negative, ValueList<Oblique> Obliques)
{
    public required ValueList<Adverbial> Adverbs { get; init; }
}
