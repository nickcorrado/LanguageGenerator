namespace LanguageGenerator;

/// <summary>How a subordinate clause relates to the clause it goes with.</summary>
public enum Subordinator
{
    If,
    When,
    Before,
    After,
    Until,
    Because,
    //'in order to', as in 'to cross the river, you need a rope'
    Purpose,
}

/// <summary>Whether a subordinate clause's verb keeps its ordinary form, as WALS 125A to 127A ask.</summary>
public enum Balance
{
    //the verb is as it would be in a clause of its own
    Balanced,
    //the verb loses its tense and agreement, as a converb or a nominalization does
    Deranked,
    //either, and here the verb is deranked where the two clauses share a subject, as it usually is
    Either,
}

/// <summary>
/// How a language marks one kind of subordinate clause: a word at its start, as English if, a
/// word at its end, or a suffix on its verb, as Turkish -sA; whether its verb is deranked; whether
/// it comes before the main clause or after it; and the role, if any, whose case or adposition
/// marks it in place of the marker when it's deranked, as a purpose clause can be 'for drinking'.
/// </summary>
//Position is PrecedingParticle for a word at the start, FollowingParticle for one at the end, or Suffix
public record SubordinatorMarking(Morpheme Marker, ExponentPosition Position, Balance Balance, bool BeforeMain, Role? MarkedAs);

/// <summary>
/// A semantic map of the subordinators: which share a marker in languages, as German wenn is both
/// 'if' and 'when' and English since both 'after' and 'because', and which roles' markers a reduced
/// clause of each kind takes, as the ways languages grammaticalize them go.
/// </summary>
//a subordinator sharing a marker keeps its own meaning in the gloss, so wenn is COND in one use and
//TEMP in the other, and its own balance and order. The paths are Heine and Kuteva's: a temporal
//marker becoming conditional or causal, 'after' becoming 'because', an allative becoming a purpose
//marker, an ablative a cause and a locative a time. WALS counts none of this, so every weight is a
//guess
public static class SubordinatorMap
{
    /// <summary>The subordinators, in no order that matters but the edges'.</summary>
    public static IReadOnlyList<Subordinator> Subordinators { get; } = Enum.GetValues<Subordinator>();

    public static IReadOnlyList<(Subordinator First, Subordinator Second, double Weight)> Edges { get; } =
    [
        //German wenn, Dutch als, Indonesian kalau
        (Subordinator.When, Subordinator.If, 0.4),
        //a general 'when' that covers 'after'
        (Subordinator.When, Subordinator.After, 0.25),
        //English since, German nachdem, French puisque from puis 'then'
        (Subordinator.After, Subordinator.Because, 0.2),
        //Turkish için, for both 'in order to' and 'because'
        (Subordinator.Purpose, Subordinator.Because, 0.2),
        //'until' as 'before' with a negation, as in Romance
        (Subordinator.Before, Subordinator.Until, 0.2),
        //Latin cum, temporal and causal
        (Subordinator.When, Subordinator.Because, 0.15),
    ];

    /// <summary>
    /// The roles whose marker a deranked clause of each kind can take, and how often, with null for
    /// none: purpose like a goal or recipient ('for drinking', English to drink), a reason like a
    /// source or instrument ('from his drinking'), 'when' like a place, 'after' like a source, and
    /// 'until' like a goal, as English till and Latin usque ad.
    /// </summary>
    public static (Role? Role, int Weight)[] AttachmentsOf(Subordinator subordinator) => subordinator switch
    {
        Subordinator.Purpose => [(Role.Goal, 25), (Role.Recipient, 15), (null, 60)],
        Subordinator.Because => [(Role.Source, 25), (Role.Instrument, 10), (null, 65)],
        Subordinator.When => [(Role.Location, 20), (null, 80)],
        Subordinator.After => [(Role.Source, 15), (null, 85)],
        Subordinator.Until => [(Role.Goal, 20), (null, 80)],
        Subordinator.If or Subordinator.Before => [(null, 1)],
        _ => throw new ArgumentOutOfRangeException(nameof(subordinator), subordinator, null),
    };
}

/// <summary>How a language marks each kind of subordinate clause.</summary>
public record Subordination(IReadOnlyDictionary<Subordinator, SubordinatorMarking> Markers);

/// <summary>
/// A statement with a clause subordinate to it, as in 'if you hear the song, cover your ears'.
/// SharesSubject says the subordinate clause's subject wasn't said, but taken from the main clause,
/// as a purpose clause's is in 'to cross the river, you need a rope'.
/// </summary>
public record Subordinated(Statement Main, Subordinator Subordinator, Statement Clause, bool SharesSubject);
