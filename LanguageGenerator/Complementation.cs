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
/// How a language marks one kind of complement clause: its complementizer and where it goes,
/// whether its verb is deranked, and the role, if any, whose case or adposition marks it in place of
/// the complementizer when it's deranked, as Turkish puts a nominalized complement in the
/// accusative and English marks 'to go' like a goal.
/// </summary>
//the marker is coined even where Position is None, so that a spec can write one in
public record ComplementMarking(Morpheme Marker, ComplementizerPosition Position, Balance Balance, Role? MarkedAs);

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

/// <summary>
/// How a language marks a quotation: not at all, with the complementizer, or with a word of its own
/// on one side of it.
/// </summary>
//the word is coined whatever the kind, so that a spec can write one in, and BeforeQuote is only for it
public record Quotative(QuotativeKind Kind, Particle Word, bool BeforeQuote);

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
/// How a language says 'want': as a verb, or as a marker on the verb of what's wanted, which then
/// takes the tense and agreement, as Japanese tabe-ta-i 'eat-DESID-PRS' is 'wants to eat': an affix
/// next to the root, on the side AffixPosition says, or a word beside the verb, before it if
/// BeforeVerb, as modals are.
/// </summary>
//the marker's form is coined whatever the kind, so that a spec can write one in
public record Desiderative(DesiderativeKind Kind, IReadOnlyList<Phoneme> Form, ExponentPosition AffixPosition, bool BeforeVerb)
{
    /// <summary>The marker on the verb of what's wanted, or null where 'want' is a verb.</summary>
    public ModalMarking? Marking => Kind switch
    {
        DesiderativeKind.Verb => null,
        DesiderativeKind.Affix => new ModalAffix(new Affix(Form, [Volition.Desiderative]), AffixPosition),
        DesiderativeKind.Particle => new ModalParticle(new Particle(Form, [Volition.Desiderative]), BeforeVerb),
        _ => throw new InvalidOperationException(),
    };
}

/// <summary>
/// How a language marks complement clauses and quotations: each kind of complement's marking, how
/// a quotation is marked, whether a complement that keeps its verb goes last in its clause, and
/// how 'want' is said.
/// </summary>
//a deranked complement is a noun phrase, and goes where the object does whatever Last says
public record Complementation(ComplementMarking Proposition, ComplementMarking Event, Quotative Quotative, bool Last, Desiderative Desiderative)
{
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
    public (Morpheme Marker, ExponentPosition Position)? QuotativeMarker => Quotative.Kind switch
    {
        QuotativeKind.None => null,
        QuotativeKind.Word => (Quotative.Word, Quotative.BeforeQuote ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle),
        QuotativeKind.Complementizer => Proposition.Position switch
        {
            ComplementizerPosition.Suffix => (new Affix(Proposition.Marker.Form, [Linkage.Quotative]), ExponentPosition.Suffix),
            ComplementizerPosition.Initial => (new Particle(Proposition.Marker.Form, [Linkage.Quotative]), ExponentPosition.PrecedingParticle),
            ComplementizerPosition.Final => (new Particle(Proposition.Marker.Form, [Linkage.Quotative]), ExponentPosition.FollowingParticle),
            _ => throw new InvalidOperationException("A quotation can only be marked with the complementizer where a proposition has one."),
        },
        _ => throw new InvalidOperationException(),
    };

    /// <summary>Where a complementizer in this position goes, as an exponent does.</summary>
    public static ExponentPosition? PositionOf(ComplementizerPosition position) => position switch
    {
        ComplementizerPosition.None => null,
        ComplementizerPosition.Initial => ExponentPosition.PrecedingParticle,
        ComplementizerPosition.Final => ExponentPosition.FollowingParticle,
        ComplementizerPosition.Suffix => ExponentPosition.Suffix,
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
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
/// says'. It's as it was said, so its 'I' is the speaker's, and it can be a command or a question.
/// </summary>
public record Quotation(Statement Statement);
