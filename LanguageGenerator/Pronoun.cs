namespace LanguageGenerator;

//a personal pronoun as a clause asks for it, independent of any language: 1SG, 1PL.INCL or 3SG.F
public record Pronoun(Person Person, Number Number, Clusivity? Clusivity = null, Gender? Gender = null)
{
    public string Gloss => Person.Gloss + Number.Gloss + (Clusivity is { } c ? $".{c.Gloss}" : "") + (Gender is { } g ? $".{g.Gloss}" : "");
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

//WALS 44A: which pronouns distinguish gender. Its 2 languages with gender only in the first or
//second person, and 1 with it only in the third person plural, are left out
public enum PronounGender
{
    None,
    //he and she, but one they, like English
    ThirdSingular,
    //he and she, and masculine and feminine they, like French il, elle, ils and elles
    Third,
    //the third person, and the second too, like Arabic's masculine and feminine 'you'. WALS counts
    //languages with gender in the first person here as well, which is rarer
    ThirdAndSecond,
}

//a language's personal pronouns. Unlike the lexicon, they're a closed set, made along with the
//language. They inflect only for case, since person and number are part of the pronoun itself
public class PronounSystem
{
    public required FirstPersonPlural FirstPersonPlural { get; init; }
    public required PronounGender GenderedPronouns { get; init; }
    //the genders gendered pronouns distinguish: masculine and feminine, and maybe a neuter, which
    //only the third person has
    public required IReadOnlyList<Gender> Genders { get; init; }
    //every pronoun the language has, by the person, number, clusivity and gender it means
    public required IReadOnlyDictionary<Pronoun, Lexeme> Forms { get; init; }
    public required Paradigm Paradigm { get; init; }
    //WALS 101A: whether a subject pronoun is usually left out, as in Spanish veo 'I see'
    public required bool DropsSubjects { get; init; }

    private static readonly Number[] Numbers = [Number.Singular, Number.Plural];

    //the pronoun the language uses for this meaning: a dual falls back on the plural, 'we' is
    //exclusive unless said otherwise, in a language that tells them apart, and a gendered pronoun
    //is masculine unless said otherwise, as it is by default in most languages that have one. So
    //is a gender the pronoun doesn't have, like a neuter 'you'
    public Lexeme For(Pronoun pronoun)
    {
        var number = (Number)(Syntax.Nearest(pronoun.Number, Numbers) ?? Number.Plural);
        var clusivity = pronoun.Person == Person.First && number != Number.Singular && FirstPersonPlural == FirstPersonPlural.InclusiveExclusive
            ? pronoun.Clusivity ?? Clusivity.Exclusive
            : null;
        var gender = Distinguishes(GenderedPronouns, pronoun.Person, number) ? pronoun.Gender ?? Gender.Masculine : (Gender?)null;
        var meaning = new Pronoun(pronoun.Person, number, clusivity, gender);
        return Forms.TryGetValue(meaning, out var form) ? form : Forms[meaning with { Gender = Gender.Masculine }];
    }

    //whether pronouns of this person and number come in genders
    public static bool Distinguishes(PronounGender gender, Person person, Number number) => gender switch
    {
        PronounGender.None => false,
        PronounGender.ThirdSingular => person == Person.Third && number == Number.Singular,
        PronounGender.Third => person == Person.Third,
        PronounGender.ThirdAndSecond => person == Person.Third || person == Person.Second,
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };
}
