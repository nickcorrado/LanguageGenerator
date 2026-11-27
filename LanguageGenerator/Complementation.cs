namespace LanguageGenerator;

/// <summary>
/// What a complement clause says, which decides how a language marks it, after Noonan's split by
/// time reference: a proposition has a time of its own, and an event's time is the main verb's.
/// </summary>
public enum ComplementKind
{
    /// <summary>
    /// Something said, known, thought or seen, with a time of its own, as in 'she said that you were
    /// late' or 'I know that they came'.
    /// </summary>
    Proposition,
    /// <summary>
    /// Something wanted, tried, begun or told to be done, whose time the main verb sets, as in 'they
    /// want to go home' or 'I told you to wait'.
    /// </summary>
    Event,
}

/// <summary>
/// Where a complement clause's marker goes: nowhere, as Mandarin sets a complement beside its verb
/// with nothing between, a word at its start, as English that, a word at its end, as Japanese to,
/// or a suffix on its verb, as Korean -ko.
/// </summary>
public enum ComplementizerPosition
{
    None,
    Initial,
    Final,
    Suffix,
}

/// <summary>
/// How a language marks one kind of complement clause: its complementizer, a word at its start or
/// end or a suffix on its verb, or none, whether its verb is deranked, and the role, if any, whose
/// case or adposition marks it in place of the complementizer when it's deranked, as Turkish puts a
/// nominalized complement in the accusative and English marks 'to go' like a goal.
/// </summary>
public record ComplementMarking(Exponent? Marker, Balance Balance, Role? MarkedAs)
{
    /// <summary>Where the complementizer goes, as a spec names it.</summary>
    public ComplementizerPosition Position => Marker switch
    {
        null => ComplementizerPosition.None,
        Bound => ComplementizerPosition.Suffix,
        Free { Side: Side.Before } => ComplementizerPosition.Initial,
        Free => ComplementizerPosition.Final,
    };
}

/// <summary>How a language marks a quotation, as a spec names it.</summary>
public enum QuotativeKind
{
    /// <summary>Not at all, as English sets 'you're late' beside 'she says'.</summary>
    None,
    /// <summary>
    /// With the complementizer of a proposition, as Japanese to and Ewe bé mark both, since words
    /// for 'say' so often become complementizers.
    /// </summary>
    Complementizer,
    /// <summary>With a word of its own, as Turkish diye 'saying'.</summary>
    Word,
}

/// <summary>A quotation marked with the complementizer of a proposition, glossed QUOT in this use.</summary>
public record QuotativeComplementizer;

/// <summary>
/// How a language marks a quotation: with a word of its own on one side of it, or with the
/// complementizer.
/// </summary>
public union Quotative(Free, QuotativeComplementizer);

/// <summary>How a language says 'want': WALS 124A's types.</summary>
public enum DesiderativeKind
{
    /// <summary>A verb, which takes what's wanted as a complement, as English want does.</summary>
    Verb,
    /// <summary>An affix on the verb of what's wanted, as Japanese -tai.</summary>
    Affix,
    /// <summary>A word of its own beside the verb of what's wanted, as Itzaj tak.</summary>
    Particle,
}

/// <summary>
/// How a language marks complement clauses and quotations: each kind of complement's marking, how
/// a quotation is marked, or null where it isn't, whether a complement that keeps its verb goes last
/// in its clause, and how 'want' is said: as a marker on the verb of what's wanted, which then takes
/// the tense and agreement, as Japanese tabe-ta-i 'eat-DESID-PRS' is 'wants to eat', an affix next
/// to the root or a word beside the verb, as modals are, or null where 'want' is a verb.
/// </summary>
//a deranked complement is a noun phrase, and goes where the object does whatever Last says
public record Complementation(ComplementMarking Proposition, ComplementMarking Event, Quotative? Quotative, bool Last, Exponent? Desiderative)
{
    /// <summary>The desiderative as a modal on the verb of what's wanted, or null where 'want' is a verb.</summary>
    public ModalMarking? DesiderativeMarking => Desiderative is not { } desiderative ? null : desiderative switch
    {
        Bound b => b,
        Free f => f,
    };

    /// <summary>The kind the desiderative is, as a spec names it.</summary>
    public DesiderativeKind DesiderativeKind => Desiderative is not { } desiderative ? DesiderativeKind.Verb : desiderative switch
    {
        Bound => DesiderativeKind.Affix,
        Free => DesiderativeKind.Particle,
    };

    /// <summary>The kind the quotative is, as a spec names it.</summary>
    public QuotativeKind QuotativeKind => Quotative is not { } quotative ? QuotativeKind.None : quotative switch
    {
        Free => QuotativeKind.Word,
        QuotativeComplementizer => QuotativeKind.Complementizer,
    };

    public ComplementMarking MarkingOf(ComplementKind kind) => kind switch
    {
        ComplementKind.Proposition => Proposition,
        ComplementKind.Event => Event,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// The marker of a quotation and where it goes, or null where it has none. One that's the
    /// complementizer is glossed QUOT in this use.
    /// </summary>
    public (Morpheme Marker, ExponentPosition Position)? QuotativeMarker => Quotative is not { } quotative ? null : quotative switch
    {
        Free word => (word.Particle, ((Exponent)word).Position),
        QuotativeComplementizer => Proposition.Marker switch
        {
            Bound b => (new Affix(b.Affix.Form, [Linkage.Quotative], b.Affix.Accent), ExponentPosition.Suffix),
            Free f => (new Particle(f.Particle.Form, [Linkage.Quotative]), ((Exponent)f).Position),
            null => throw new InvalidOperationException("A quotation can only be marked with the complementizer where a proposition has one."),
        },
    };
}

/// <summary>
/// A clause as the object of a verb, saying what's said, known or wanted, as in 'I told you that
/// you were late' or 'they want to go home'. SharesSubject says its subject wasn't said, but taken
/// from the main clause, as an event's often is.
/// </summary>
public record ComplementClause(ComplementKind Kind, Statement Clause, bool SharesSubject);

/// <summary>
/// What someone said, word for word, as the object of a verb of saying, as in '"you're late," she
/// says'. It's as it was said, so its 'I' is the speaker's, and it can be a command, a question or a
/// fragment, as in '"for your ears," he says', though not a heading.
/// </summary>
public record Quotation(Utterance Utterance);
