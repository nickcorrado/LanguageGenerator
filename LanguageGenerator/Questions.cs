namespace LanguageGenerator;

/// <summary>Where a polar question particle goes in its clause.</summary>
public enum QuestionParticlePosition
{
    /// <summary>First in the clause, as Polish czy.</summary>
    Initial,
    /// <summary>Last in the clause, as Mandarin ma or Japanese ka.</summary>
    Final,
    /// <summary>After the clause's first word, as Russian li or Latin -ne.</summary>
    Second,
}

/// <summary>A yes-no question marked by a word of its own, as Mandarin ma.</summary>
public record QuestionParticle(Particle Marker, QuestionParticlePosition Position);

/// <summary>
/// A yes-no question marked by an affix on the finite verb, outside everything else on it, as
/// West Greenlandic marks its interrogative mood.
/// </summary>
//Position is Prefix or Suffix
public record QuestionAffix(Affix Marker, ExponentPosition Position);

/// <summary>A yes-no question marked by putting the finite verb first, as German does.</summary>
public record QuestionInversion;

/// <summary>A yes-no question said like a statement, told apart only by its intonation.</summary>
public record QuestionIntonation;

/// <summary>How a language marks a yes-no question.</summary>
public union PolarQuestionMarking(QuestionParticle, QuestionAffix, QuestionInversion, QuestionIntonation);

/// <summary>A question word that fills a role, as 'who' does in 'who saw you?'.</summary>
public enum Interrogative
{
    Who,
    What,
    /// <summary>
    /// A place, in the role of where, to where or from where, as Turkish nere-de, nere-ye and
    /// nere-den are one word in three cases.
    /// </summary>
    Where,
}

/// <summary>A question word in a role, which inflects for that role as a noun does.</summary>
public record QuestionWord(Interrogative Word);

/// <summary>
/// How a language asks: how it marks a yes-no question, whether its question words go first in a
/// content question, and the question words themselves.
/// </summary>
//Why is a word of its own, since it fills no role, but takes the place a question word does
public record Questions(PolarQuestionMarking Polar, bool FrontsQuestionWords, IReadOnlyDictionary<Interrogative, Lexeme> Words, Lexeme Why);

/// <summary>
/// A statement asked rather than said. With a question word in one of its roles, or Why, it asks
/// for what that is, as in 'who saw you?' or 'why didn't you come?'; with neither, it asks whether
/// it's true, as in 'did you see them?'.
/// </summary>
//a clause with a verb, a passive, a copular clause or a comparison, with any modal, and at most one
//question word, as a direct argument of a role
public record Question(Statement Statement, bool Why);
