namespace LanguageGenerator;

/// <summary>
/// One value of a grammatical category, such as Past for tense or Plural for number.
/// </summary>
//closed, so a switch that handles every category below needs no discard, and adding a category
//flags it
public closed record Grammeme(string Name, string Gloss)
{
    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //person and number labels run together (3SG); everything else is
    //separated by a period (3SG.PST)
    public static string Separator(Grammeme left, Grammeme right) => (left, right) switch
    {
        (Person, Number) or (Number, Person) => "",
        _ => ".",
    };

    //the gloss of one morpheme expressing all of these, e.g. 3SG.PST
    public static string JoinGlosses(IReadOnlyList<Grammeme> grammemes)
    {
        var gloss = "";
        for (int i = 0; i < grammemes.Count; i++)
        {
            if (i > 0)
            {
                gloss += Separator(grammemes[i - 1], grammemes[i]);
            }
            gloss += grammemes[i].Gloss;
        }
        return gloss;
    }
}

public record Tense(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Tense Present { get; } = new("Present", "PRS");
    public static Tense Past { get; } = new("Past", "PST");
    public static Tense Future { get; } = new("Future", "FUT");
}

public record Aspect(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Aspect Imperfective { get; } = new("Imperfective", "IPFV");
    public static Aspect Perfective { get; } = new("Perfective", "PFV");
    public static Aspect Aorist { get; } = new("Aorist", "AOR");
}

public record Mood(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Mood Indicative { get; } = new("Indicative", "IND");
    public static Mood Subjunctive { get; } = new("Subjunctive", "SBJV");
    public static Mood Imperative { get; } = new("Imperative", "IMP");
    /// <summary>A negative command, as in 'don't go', where a language marks it apart.</summary>
    public static Mood Prohibitive { get; } = new("Prohibitive", "PROH");
}

public record Voice(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Voice Active { get; } = new("Active", "ACTV");
    public static Voice Passive { get; } = new("Passive", "PASS");
}

public record Case(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Case Nominative { get; } = new("Nominative", "NOM");
    public static Case Accusative { get; } = new("Accusative", "ACC");
    public static Case Genitive { get; } = new("Genitive", "GEN");
    public static Case Dative { get; } = new("Dative", "DAT");
    public static Case Ablative { get; } = new("Ablative", "ABL");
    public static Case Vocative { get; } = new("Vocative", "VOC");
    public static Case Locative { get; } = new("Locative", "LOC");
    public static Case Instrumental { get; } = new("Instrumental", "INS");
    public static Case Ergative { get; } = new("Ergative", "ERG");
    public static Case Absolutive { get; } = new("Absolutive", "ABS");
    /// <summary>
    /// The plain form in a language with only peripheral cases, covering everything they don't.
    /// </summary>
    public static Case Direct { get; } = new("Direct", "DIR");
}

/// <summary>
/// A role a phrase can play, which a language marks with a case, an adposition or nothing.
/// </summary>
public enum Role
{
    //the subject is split in two because alignments group the core roles differently:
    //nominative/accusative marks both subjects alike, while ergative/absolutive marks
    //the intransitive subject like the direct object
    IntransitiveSubject,
    TransitiveSubject,
    DirectObject,
    Possessor,
    Recipient,
    Addressee,
    Location,
    Goal,
    Source,
    Companion,
    Instrument,
    //maybe Likeness?
}

/// <summary>
/// Which core roles a language marks alike: both subjects (nominative-accusative), or the
/// intransitive subject and the object (ergative-absolutive).
/// </summary>
public enum MorphosyntacticAlignment
{
    NominativeAccusative,
    ErgativeAbsolutive,
    //TODO split ergativity, where a language is ergative in some tenses, aspects or persons and
    //accusative in the rest, as Hindi is ergative only in the perfective
    None
}

public record Person(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Person First { get; } = new("First", "1");
    public static Person Second { get; } = new("Second", "2");
    public static Person Third { get; } = new("Third", "3");
}

public record Number(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Number Singular { get; } = new("Singular", "SG");
    public static Number Dual { get; } = new("Dual", "DU");
    public static Number Plural { get; } = new("Plural", "PL");
    public static Number Paucal { get; } = new("Paucal", "PA");
    public static Number Collective { get; } = new("Collective", "CL");
}


//there's a more general form of gender found in languages
//like Swahili, which has 20 noun classes. That will not
//be supported. Also, animacy might want to be split off
public record Gender(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Gender Masculine { get; } = new("Masculine", "M");
    public static Gender Feminine { get; } = new("Feminine", "F");
    public static Gender Neuter { get; } = new("Neuter", "N");
    public static Gender Animate { get; } = new("Animate", "ANM");
    public static Gender Inanimate { get; } = new("Inanimate", "NANM");
}

/// <summary>
/// Whether a first person plural includes the person spoken to: 'you and I' or 'we, but not you'.
/// </summary>
//only pronouns distinguish it here
public record Clusivity(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Clusivity Inclusive { get; } = new("Inclusive", "INCL");
    public static Clusivity Exclusive { get; } = new("Exclusive", "EXCL");
}

/// <summary>Whether a clause is asserted or denied.</summary>
//affirmative is never marked, so it never shows in a gloss
public record Polarity(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Polarity Affirmative { get; } = new("Affirmative", "AFF");
    public static Polarity Negative { get; } = new("Negative", "NEG");
}

/// <summary>
/// Marks an affix as agreeing with a possessor, as in Hungarian ház-am 'house-1SG.POSS', so that it
/// isn't read as a verb's agreement.
/// </summary>
public record Possessive(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Possessive Possessed { get; } = new("Possessed", "POSS");
}

/// <summary>Marks a numeral as an ordinal, as English -th makes seventh of seven.</summary>
public record Ordinality(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Ordinality Ordinal { get; } = new("Ordinal", "ORD");
}

public record Definiteness(string Name, string Gloss) : Grammeme(Name, Gloss)
{
    public static Definiteness Definite { get; } = new("Definite", "DEF");
    public static Definiteness Indefinite { get; } = new("Indefinite", "NDF");
}

//TODO politeness
