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

//how a language says that something is a thing or has a property. The copula is a short verb,
//glossed COP, that inflects for tense, aspect and agreement like any other
public record Predication(PredicativeAdjectives Adjectives, bool ZeroCopula, Lexeme Copula)
{
    //WALS 120A counts zero copulas with predicate nouns. Where one is possible, the copula is left
    //out here in the present tense, as in Russian ty durak 'you are a fool', where it's most often
    //dropped; a past or a negative keeps it to carry the tense or the negative, and nonverbal
    //adjectives follow the same rule as nouns
    public bool Drops(Tense tense, bool negative) => ZeroCopula && tense == Tense.Present && !negative;
}
