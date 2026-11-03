namespace LanguageGenerator;

//WALS 118A: whether an adjective said of something, as in 'the gate is locked', acts like a verb.
//Its 103 languages that do this with some adjectives and not others are left out
public enum PredicativeAdjectives
{
    //the adjective inflects like a verb, with no copula, as in Mandarin māo dà 'the cat is big'
    Verbal,
    //the adjective needs a copula to carry tense and agreement, like English
    Nonverbal,
}

//how a language says that something is a thing, has a property or is somewhere. The copula is a
//short verb, glossed COP, that inflects for tense, aspect and agreement like any other
public record Predication(PredicativeAdjectives Adjectives, bool ZeroCopula, Lexeme Copula)
{
    //WALS 119A: most languages say where something is with a verb of its own, glossed be.at, like
    //Spanish estar beside ser, or Japanese iru beside da. Null where the copula does both, as in
    //English. Drawn after the rest, so it isn't part of the record's constructor
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
