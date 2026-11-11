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
/// word at its end, or a suffix on its verb, as Turkish -sA; whether its verb is deranked; and
/// whether it comes before the main clause or after it.
/// </summary>
//Position is PrecedingParticle for a word at the start, FollowingParticle for one at the end, or Suffix
public record SubordinatorMarking(Morpheme Marker, ExponentPosition Position, Balance Balance, bool BeforeMain);

/// <summary>How a language marks each kind of subordinate clause.</summary>
public record Subordination(IReadOnlyDictionary<Subordinator, SubordinatorMarking> Markers);

/// <summary>
/// A statement with a clause subordinate to it, as in 'if you hear the song, cover your ears'.
/// SharesSubject says the subordinate clause's subject wasn't said, but taken from the main clause,
/// as a purpose clause's is in 'to cross the river, you need a rope'.
/// </summary>
public record Subordinated(Statement Main, Subordinator Subordinator, Statement Clause, bool SharesSubject);
