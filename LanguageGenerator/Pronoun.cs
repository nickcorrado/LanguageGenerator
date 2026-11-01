namespace LanguageGenerator;

//a personal pronoun as a clause asks for it, independent of any language: 1SG, or 1PL.INCL
public record Pronoun(Person Person, Number Number, Clusivity? Clusivity = null)
{
    public string Gloss => Person.Gloss + Number.Gloss + (Clusivity is { } c ? $".{c.Gloss}" : "");
}

//WALS 39A: how a language's pronouns say 'we'
public enum FirstPersonPlural
{
    //one 'we', like English
    Plain,
    //no separate 'we' at all: the pronoun for 'I' covers it
    SameAsSingular,
    //'we' including the hearer and 'we' excluding them, like Tok Pisin yumi and mipela
    InclusiveExclusive,
}

//a language's personal pronouns. Unlike the lexicon, they're a closed set, made along with the
//language. They inflect only for case, since person and number are part of the pronoun itself
public class PronounSystem
{
    public required FirstPersonPlural FirstPersonPlural { get; init; }
    //every pronoun the language has, by the person, number and clusivity it means
    public required IReadOnlyDictionary<Pronoun, Lexeme> Forms { get; init; }
    public required Paradigm Paradigm { get; init; }
    //WALS 101A: whether a subject pronoun is usually left out, as in Spanish veo 'I see'
    public required bool DropsSubjects { get; init; }

    private static readonly Number[] Numbers = [Number.Singular, Number.Plural];

    //the pronoun the language uses for this meaning: a dual falls back on the plural, and 'we'
    //is exclusive unless said otherwise, in a language that tells them apart
    public Lexeme For(Pronoun pronoun)
    {
        var number = (Number)(Syntax.Nearest(pronoun.Number, Numbers) ?? Number.Plural);
        var clusivity = pronoun.Person == Person.First && number != Number.Singular && FirstPersonPlural == FirstPersonPlural.InclusiveExclusive
            ? pronoun.Clusivity ?? Clusivity.Exclusive
            : null;
        return Forms[new Pronoun(pronoun.Person, number, clusivity)];
    }
}
