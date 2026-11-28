namespace LanguageGenerator;

/*
  a clause in layers, from the inside out, as docs/clause-model.md lays them out: an event, what
  happens and who takes part; a state of affairs, an event with the operators that scope over it,
  each wrapping what it scopes over; a proposition, a state of affairs placed in time; and a
  clause, which states a proposition, asks one, or tells someone to bring a state of affairs about
 */

/// <summary>
/// What happens, and who and what take part: what's said of the subject, the subject, and the
/// obliques and adverbs saying more of it.
/// </summary>
//obliques and adverbs are each in the order they're written, which ModifierOrder keeps for those
//that go equally near the verb. A directive's subject is 'you' unless it says otherwise
public record Event(Predicate Predicate, Argument Subject, ValueList<Oblique> Obliques, ValueList<Adverbial> Adverbs)
{
    /// <summary>
    /// What fills each of the event's roles, in order: its subject, then its object, a passive's
    /// agent or where be says its subject is, then its obliques.
    /// </summary>
    public IEnumerable<Filler> Fillers =>
    [
        new Filler(Subject, Predicate.Value is Doing { Object: not null } ? Role.TransitiveSubject : Role.IntransitiveSubject, null),
        .. Predicate.Fillers,
        .. Obliques.Select(o => new Filler(o.Argument, o.Role, o)),
    ];

    /// <summary>
    /// Every argument the event says, in order: its fillers', with what be says its subject is after
    /// the subject, then the standards its manners are compared with.
    /// </summary>
    public IEnumerable<Argument> Arguments =>
    [
        Subject,
        .. Predicate.Arguments,
        .. Obliques.Select(o => o.Argument),
        .. Adverbs.SelectMany(a => a.Value is Manner manner ? manner.Adjective.Arguments : []),
    ];

    /// <summary>The adjectives it says outside its noun phrases: what be says its subject is, alone or joined, and its manners'.</summary>
    public IEnumerable<Adjective> Adjectives => [.. Attributed, .. Adverbs.Select(a => a.Value).OfType<Manner>().Select(m => m.Adjective)];

    private IEnumerable<Adjective> Attributed => Predicate.Value switch
    {
        Being { Attribute.Value: Adjective adjective } => [adjective],
        Being { Attribute.Value: AdjectiveCoordination together } => together.Adjectives,
        Doing or Done or Being => [],
    };

    /// <summary>The event with each of its roles filled with what this makes of what fills it now, and its manners' standards.</summary>
    public Event Refilled(Func<Argument, Argument> fill) => this with
    {
        Subject = fill(Subject),
        Predicate = Predicate.Refilled(fill),
        Obliques = [.. Obliques.Select(o => o with { Argument = fill(o.Argument) })],
        Adverbs = [.. Adverbs.Select(a => a.Value is Manner manner ? new Manner(manner.Adjective.Refilled(fill)) : a)],
    };
}

/// <summary>What fills one of a clause's roles, and the oblique it's in, where it's in one.</summary>
public record Filler(Argument Argument, Role Role, Oblique? Oblique);

/// <summary>
/// An event with the operators that scope over it: negation, a situational modal and aspect, each
/// wrapping the state of affairs it scopes over, so the order they nest in is their scope, as in
/// 'you must not sing', a modal over a negation, and 'you don't have to sing', a negation over a
/// modal; or states of affairs joined under one tense and sentence type, as in 'she turns and
/// walks away', which Haspelmath tells apart from clauses joined. One with no aspect has the
/// unmarked one, the imperfective.
/// </summary>
public union StateOfAffairs(Event, Coordinated<StateOfAffairs>, Negated, Modalized, Aspected)
{
    /// <summary>The events the operators scope over, in order: one, or each of those joined.</summary>
    public IEnumerable<Event> Events => this switch
    {
        Event happening => [happening],
        Coordinated<StateOfAffairs> joined => [.. joined.First.Events, .. joined.Second.Events],
        Negated negated => negated.StateOfAffairs.Events,
        Modalized modalized => modalized.StateOfAffairs.Events,
        Aspected aspected => aspected.StateOfAffairs.Events,
    };

    /// <summary>The first event, which is the only one where nothing's joined.</summary>
    public Event Event => Events.First();

    /// <summary>The same operators over what this makes of each event.</summary>
    public StateOfAffairs Rebuilt(Func<Event, Event> rebuild) => this switch
    {
        Event happening => rebuild(happening),
        Coordinated<StateOfAffairs> joined => joined with { First = joined.First.Rebuilt(rebuild), Second = joined.Second.Rebuilt(rebuild) },
        Negated negated => negated with { StateOfAffairs = negated.StateOfAffairs.Rebuilt(rebuild) },
        Modalized modalized => modalized with { StateOfAffairs = modalized.StateOfAffairs.Rebuilt(rebuild) },
        Aspected aspected => aspected with { StateOfAffairs = aspected.StateOfAffairs.Rebuilt(rebuild) },
    };
}

/// <summary>A state of affairs that isn't so, as in 'the dog doesn't sleep'.</summary>
public record Negated(StateOfAffairs StateOfAffairs);

/// <summary>A state of affairs that's possible or necessary, as in 'you can swim' or 'you must find the key'.</summary>
public record Modalized(SituationalModality Modality, StateOfAffairs StateOfAffairs);

/// <summary>A state of affairs seen with an aspect, as in 'the dog has eaten' or 'the dog was eating'.</summary>
//the imperfective is the unmarked aspect, which a state of affairs with none has, so the parser
//never wraps one in it
public record Aspected(Aspect Aspect, StateOfAffairs StateOfAffairs);

/// <summary>A state of affairs placed in time, which can be true or false.</summary>
public record Proposition(StateOfAffairs StateOfAffairs, Tense Tense);

/// <summary>
/// A proposition with what scopes over it: how sure the speaker is of it, as in 'he must have
/// left', and a denial of that, as in 'he can't have left', each wrapping what it scopes over, as
/// the state of affairs' operators do.
/// </summary>
public union Propositional(Proposition, Judged, Denied)
{
    /// <summary>The proposition its operators scope over.</summary>
    public Proposition Proposition => this switch
    {
        Proposition proposition => proposition,
        Judged judged => judged.Propositional.Proposition,
        Denied denied => denied.Propositional.Proposition,
    };

    /// <summary>The same operators over what this makes of the proposition.</summary>
    public Propositional Rebuilt(Func<Proposition, Proposition> rebuild) => this switch
    {
        Proposition proposition => rebuild(proposition),
        Judged judged => judged with { Propositional = judged.Propositional.Rebuilt(rebuild) },
        Denied denied => denied with { Propositional = denied.Propositional.Rebuilt(rebuild) },
    };
}

/// <summary>A proposition the speaker judges possibly or surely so, as in 'he might have left'.</summary>
public record Judged(EpistemicModality Modality, Propositional Propositional);

/// <summary>A judgment denied, as in 'he can't have left', which says it isn't possible that he left.</summary>
public record Denied(Propositional Propositional);

/// <summary>
/// What an independent clause does: state a proposition, ask about one, or tell someone to bring
/// a state of affairs about, after König and Siemund's sentence types.
/// </summary>
public union Clause(Declarative, Interrogative, Directive)
{
    /// <summary>What the clause holds below its sentence type.</summary>
    public StateOfAffairs StateOfAffairs => this switch
    {
        Declarative stated => stated.Propositional.Proposition.StateOfAffairs,
        Interrogative asked => asked.Propositional.Proposition.StateOfAffairs,
        Directive told => told.StateOfAffairs,
    };

    /// <summary>The event at the clause's heart, the first where events are joined.</summary>
    public Event Event => StateOfAffairs.Event;
}

/// <summary>A proposition stated, as in 'the dog sleeps'.</summary>
public record Declarative(Propositional Propositional);

/// <summary>
/// A proposition asked about: whether it's so, as in 'did you see them?', or, with a question word
/// in one of its roles or among its adverbs, what that is, as in 'who saw you?' or 'why didn't you
/// come?'.
/// </summary>
public record Interrogative(Propositional Propositional);

/// <summary>
/// Telling someone to bring a state of affairs about, as in 'go home' or 'don't drink the wine'. It
/// has no tense, since its time is the telling's, and its subject is whoever's told.
/// </summary>
public record Directive(StateOfAffairs StateOfAffairs);

/// <summary>What a clause says of its subject: what it does, what's done to it, or what or where it is.</summary>
public union Predicate(Doing, Done, Being)
{
    /// <summary>What fills the roles it has besides the subject: an object, an agent, or where be says the subject is.</summary>
    public IEnumerable<Filler> Fillers => this switch
    {
        Doing { Object: { } o } => [new Filler(o, Role.DirectObject, null)],
        Doing => [],
        Done { Agent: { } agent } => [new Filler(agent, Role.Agent, null)],
        Done => [],
        Being { Attribute.Value: Oblique location } => [new Filler(location.Argument, location.Role, location)],
        Being => [],
    };

    /// <summary>The arguments it says: its fillers', what be says its subject is, or what its comparisons' standards are.</summary>
    public IEnumerable<Argument> Arguments => this switch
    {
        Being { Attribute.Value: NounPhrase noun } => [noun],
        Being { Attribute.Value: Adjective adjective } => adjective.Arguments,
        Being { Attribute.Value: AdjectiveCoordination together } => together.Adjectives.SelectMany(a => a.Arguments),
        Doing or Done or Being => Fillers.Select(f => f.Argument),
    };

    /// <summary>It with each of its fillers what this makes of it.</summary>
    public Predicate Refilled(Func<Argument, Argument> fill) => this switch
    {
        Doing doing => doing with { Object = doing.Object is { } o ? fill(o) : (Argument?)null },
        Done done => done with { Agent = done.Agent is { } agent ? fill(agent) : (Argument?)null },
        Being { Attribute.Value: Oblique location } being => being with { Attribute = location with { Argument = fill(location.Argument) } },
        Being { Attribute.Value: Adjective adjective } being => being with { Attribute = adjective.Refilled(fill) },
        Being { Attribute.Value: AdjectiveCoordination together } being => being with { Attribute = together with { Adjectives = [.. together.Adjectives.Select(a => a.Refilled(fill))] } },
        Being being => being,
    };
}

/// <summary>A verb, with its object if it has one.</summary>
public record Doing(string Verb, Argument? Object);

/// <summary>
/// A property, like 'big', said of something, with its grade if it has one, as 'very big' and
/// 'bigger than the house' have, wherever it is.
/// </summary>
public record Adjective(string Meaning, Grade? Grade)
{
    /// <summary>The argument it says, if it says one: a comparative's standard, where that's a noun phrase or another argument.</summary>
    public IEnumerable<Argument> Arguments => Grade is { Value: Comparative { Standard.Value: Argument standard } } ? [standard] : [];

    /// <summary>The clause it says, if it says one: a comparative's standard, where that's a clause.</summary>
    public IEnumerable<Propositional> Clauses => Grade is { Value: Comparative { Standard.Value: Propositional standard } } ? [standard] : [];

    /// <summary>It with its standard, if that's an argument, what this makes of it.</summary>
    public Adjective Refilled(Func<Argument, Argument> fill) => Grade is { Value: Comparative { Standard.Value: Argument standard } compared }
        ? this with { Grade = compared with { Standard = fill(standard) } }
        : this;
}

/// <summary>
/// What be says of its subject: a noun phrase, as in 'you are a fool', an adjective, as in 'the
/// gate is locked' or 'Marta is taller than the men', adjectives together, as in 'the inn is warm
/// and crowded', or a location, as in 'the dog is in the house'.
/// </summary>
public union Attribute(NounPhrase, Adjective, AdjectiveCoordination, Oblique);

/// <summary>
/// What or where a clause's subject is, as English says with be, or told, what or where to be, as
/// in 'be careful', 'don't be a fool' or 'be in the house'.
/// </summary>
//a clause with one has obliques saying only when and for how long
public record Being(Attribute Attribute);

/// <summary>A clause, or clauses joined or one under another, as a line says them.</summary>
public union Sentence(Clause, Coordinated<Sentence>, Subordinated);
