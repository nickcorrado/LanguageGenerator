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
public record QuestionAffix(Bound Marker);

/// <summary>A yes-no question marked by putting the finite verb first, as German does.</summary>
public record QuestionInversion;

/// <summary>A yes-no question said like a statement, told apart only by its intonation.</summary>
public record QuestionIntonation;

/// <summary>How a language marks a yes-no question.</summary>
public union PolarQuestionMarking(QuestionParticle, QuestionAffix, QuestionInversion, QuestionIntonation);

/// <summary>
/// How a language marks polar questions, as a spec names it: a particle and where it goes, an
/// affix and which side, inversion or intonation.
/// </summary>
public enum PolarQuestion
{
    InitialParticle,
    FinalParticle,
    SecondParticle,
    Prefix,
    Suffix,
    Inversion,
    Intonation,
}

/// <summary>A question word that fills a role, as 'who' does in 'who saw you?'.</summary>
public enum InterrogativeWord
{
    Who,
    What,
    /// <summary>
    /// A place, in the role of where, to where or from where, as Turkish nere-de, nere-ye and
    /// nere-den are one word in three cases.
    /// </summary>
    Where,
    /// <summary>A time, in the role of when, as Turkish ne zaman 'what time' is marked as a time is.</summary>
    When,
}

/// <summary>A question word that fills no role, but asks why or how something happened.</summary>
public enum QuestionAdverb
{
    Why,
    How,
}

/// <summary>A question word in a role, which inflects for that role as a noun does.</summary>
public record QuestionWord(InterrogativeWord Word);

/// <summary>
/// How a language asks: how it marks a yes-no question, whether its question words go first in a
/// content question, and the question words themselves.
/// </summary>
//why and how are words of their own, since they fill no role, but take the place a question word does
public record Questions(PolarQuestionMarking Polar, bool FrontsQuestionWords, IReadOnlyDictionary<InterrogativeWord, Lexeme> Words, IReadOnlyDictionary<QuestionAdverb, Lexeme> Adverbs);

/// <summary>'Why' or 'how' asked, among a clause's adverbs, as in 'why didn't you come?'.</summary>
public record QuestionAdverbial(QuestionAdverb Word);
