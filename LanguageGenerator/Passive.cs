namespace LanguageGenerator;

/// <summary>
/// A language's passive: a marker on the verb, an affix next to its root, as Turkish -il in
/// gör-ül-dü 'was seen', or in an isolating language a word beside it.
/// </summary>
//WALS 107A counts passives without telling them apart by form, and the generator has no
//participles, so an English-style passive with be and a participle is made this way too
public record Passive(Exponent Exponent);

/// <summary>
/// What's done to a clause's subject, its patient, as in 'each light is carried by a small figure',
/// with who does it, if anyone says.
/// </summary>
public record Done(string Verb, Argument? Agent);
