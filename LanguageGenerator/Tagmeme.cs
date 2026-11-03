namespace LanguageGenerator;

//a value of one of the grammatical categories which morphemes might express. It's closed, so a
//switch that handles every category below needs no discard, and adding a category flags it
public closed record Tagmeme(string Name, string Gloss)
{
    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //person and number labels run together (3SG); everything else is
    //separated by a period (3SG.PST)
    public static string Separator(Tagmeme left, Tagmeme right) => (left, right) switch
    {
        (Person, Number) or (Number, Person) => "",
        _ => ".",
    };

    //the gloss of one morpheme expressing all of these, e.g. 3SG.PST
    public static string JoinGlosses(IReadOnlyList<Tagmeme> tagmemes)
    {
        var gloss = "";
        for (int i = 0; i < tagmemes.Count; i++)
        {
            if (i > 0)
            {
                gloss += Separator(tagmemes[i - 1], tagmemes[i]);
            }
            gloss += tagmemes[i].Gloss;
        }
        return gloss;
    }
}

public record Tense(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Tense Present { get; } = new("Present", "PRS");
    public static Tense Past { get; } = new("Past", "PST");
    public static Tense Future { get; } = new("Future", "FUT");
}

public record Aspect(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Aspect Imperfective { get; } = new("Imperfective", "IPFV");
    public static Aspect Perfective { get; } = new("Perfective", "PFV");
    public static Aspect Aorist { get; } = new("Aorist", "AOR");
}

public record Mood(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Mood Indicative { get; } = new("Indicative", "IND");
    public static Mood Subjunctive { get; } = new("Subjunctive", "SBJV");
    public static Mood Imperative { get; } = new("Imperative", "IMP");
}

public record Voice(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Voice Active { get; } = new("Active", "ACTV");
    public static Voice Passive { get; } = new("Passive", "PASS");
}

//If there are any cases at all, then nominative/accusative or ergative/absolutive
//is a must. After that, there's a chance of adding the genitive, the dative, etc.
//The other option, of course, is an adposition. So maybe those should be generated
//together?
public record Case(string Name, string Gloss) : Tagmeme(Name, Gloss)
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
    //the plain form in a language whose only cases are peripheral, covering everything they don't
    public static Case Direct { get; } = new("Direct", "DIR");
}

//this is a list of case uses that cases in the language cover. direct and indirect
//objects, direct address, etc.
//each of these is marked by exactly one RoleMarker: a case, an adposition, or nothing
public enum CaseUse
{
    //the subject is split in two because alignments group the core roles differently:
    //nominative/accusative marks both subjects alike, while ergative/absolutive marks
    //the intransitive subject like the direct object
    IntransitiveSubject,
    TransitiveSubject,
    DirectObject,
    Possessive,
    IndirectObject,
    Address,
    Location,
    MovementTo,
    MovementFrom,
    Accompaniment,
    Instrument,
    //maybe Likeness?
}

public enum MorphosyntacticAlignment
{
    NominativeAccusative,
    ErgativeAbsolutive,
    //split ergative? ugh, sounds like too much trouble
    None //do we need this?
}

public record Person(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Person First { get; } = new("First", "1");
    public static Person Second { get; } = new("Second", "2");
    public static Person Third { get; } = new("Third", "3");
}

public record Number(string Name, string Gloss) : Tagmeme(Name, Gloss)
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
public record Gender(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Gender Masculine { get; } = new("Masculine", "M");
    public static Gender Feminine { get; } = new("Feminine", "F");
    public static Gender Neuter { get; } = new("Neuter", "N");
    public static Gender Animate { get; } = new("Animate", "ANM");
    public static Gender Inanimate { get; } = new("Inanimate", "NANM");
}

//whether a first person plural includes the person spoken to: 'we, you among us' or 'we, but
//not you'. Only pronouns distinguish it here
public record Clusivity(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Clusivity Inclusive { get; } = new("Inclusive", "INCL");
    public static Clusivity Exclusive { get; } = new("Exclusive", "EXCL");
}

//whether a clause is asserted or denied. Affirmative is never marked, so it never shows in a gloss
public record Polarity(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Polarity Affirmative { get; } = new("Affirmative", "AFF");
    public static Polarity Negative { get; } = new("Negative", "NEG");
}

//what an affix agreeing with a possessor adds to its person and number, so that ház-am
//'house-1SG.POSS' isn't read as a verb's agreement
public record Possessive(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Possessive Possessed { get; } = new("Possessed", "POSS");
}

public record Definiteness(string Name, string Gloss) : Tagmeme(Name, Gloss)
{
    public static Definiteness Definite { get; } = new("Definite", "DEF");
    public static Definiteness Indefinite { get; } = new("Indefinite", "NDF");
}

//politeness?
