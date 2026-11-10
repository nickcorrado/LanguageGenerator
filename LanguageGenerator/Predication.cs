namespace LanguageGenerator;

/// <summary>
/// Whether an adjective said of something, as in 'the gate is locked', acts like a verb.
/// </summary>
//WALS 118A, leaving out its 103 languages that do this with some adjectives and not others
public enum PredicativeAdjectives
{
    //the adjective inflects like a verb, with no copula, as in Mandarin māo dà 'the cat is big'
    Verbal,
    //the adjective needs a copula to carry tense and agreement, like English
    Nonverbal,
}

/// <summary>
/// How a language says that something is a thing, has a property or is somewhere. The copula is a
/// short verb, glossed COP, that inflects for tense, aspect and agreement like any other.
/// </summary>
public record Predication(PredicativeAdjectives Adjectives, bool ZeroCopula, Lexeme Copula)
{
    /// <summary>
    /// The language's own verb for where something is, glossed be.at, like Spanish estar beside
    /// ser. Null where the copula does both, as in English.
    /// </summary>
    //WALS 119A, where most languages have one, as Japanese has iru beside da. Drawn after the rest,
    //so it isn't part of the record's constructor
    public Lexeme? LocationalCopula { get; init; }

    public Lexeme CopulaFor(bool locational) => locational ? LocationalCopula ?? Copula : Copula;

    //WALS 120A counts zero copulas with predicate nouns. Where one is possible, the copula is left
    //out here in the present tense, as in Russian ty durak 'you are a fool', where it's most often
    //dropped; a past or a negative keeps it to carry the tense or the negative, and nonverbal
    //adjectives follow the same rule as nouns. So does a location said with the same copula, as in
    //Russian ja doma 'I'm at home', but a locational verb of its own is never dropped
    public bool Drops(Tense tense, bool negative, bool locational = false) =>
        ZeroCopula && tense == Tense.Present && !negative && !(locational && LocationalCopula is not null);
}
