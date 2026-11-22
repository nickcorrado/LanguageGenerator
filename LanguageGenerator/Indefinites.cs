namespace LanguageGenerator;

/// <summary>
/// A word in a noun phrase saying how much or how many of something, as 'all', 'every' or 'few' do.
/// Some, No and Every are the series of indefinite pronouns too, as in 'someone', 'nobody' and
/// 'everyone'.
/// </summary>
public enum Quantifier
{
    All,
    /// <summary>Every one of something, taken one at a time, as 'every' and 'each' say.</summary>
    Every,
    Some,
    Many,
    Few,
    Most,
    /// <summary>None of something, as 'no' says in 'no traveler came'.</summary>
    No,
}

/// <summary>
/// What an indefinite pronoun stands for, as Haspelmath's ontological categories have it: a person,
/// as 'someone', a thing, as 'something', or a place, as 'somewhere'. These are the categories the
/// question words who, what and where ask about.
/// </summary>
public enum Ontology
{
    Person,
    Thing,
    Place,
}

/// <summary>How a language makes its indefinite pronouns, like 'someone' and 'nothing', as WALS 46A counts them.</summary>
public enum IndefinitePronouns
{
    /// <summary>From its question words, as Japanese dare-ka 'who-INDF' is 'someone'.</summary>
    InterrogativeBased,
    /// <summary>From nouns for 'person', 'thing' and 'place', as English some-body and some-thing.</summary>
    GenericNounBased,
    /// <summary>From words of their own, as Latin aliquis 'someone'.</summary>
    Special,
    /// <summary>
    /// Some one way and some the other, as Semelai's 'someone' is from a noun and its 'something' from
    /// a question word, and Maori's the other way around.
    /// </summary>
    Mixed,
}

/// <summary>What the indefinite pronouns of one kind, a person, a thing or a place, are made from.</summary>
public enum IndefiniteBase
{
    Interrogative,
    GenericNoun,
    Special,
}

/// <summary>
/// Whether a negative indefinite like 'nobody' takes the verb's negation too, as WALS 115A counts
/// languages.
/// </summary>
public enum NegativeIndefiniteNegation
{
    /// <summary>Always, as Russian nikto ne prishël 'nobody not came'.</summary>
    Always,
    /// <summary>Never, as English nobody came and German niemand kam.</summary>
    Never,
    /// <summary>Only where it comes after the verb, as Spanish no vino nadie but nadie vino.</summary>
    AfterVerb,
}

/// <summary>
/// A language's quantifiers and indefinite pronouns: a word for each quantifier, which goes before or
/// after its noun; what 'someone', 'nothing' and the rest are made from, for persons and for things,
/// places going as things do; for those made from question words, a marker for each series, as
/// Japanese -ka and -mo, which goes on the question word as a word or an affix; for those of their
/// own, a word for each; and whether a negative one takes the verb's negation too.
/// </summary>
/*
  an indefinite made from a noun is that noun with its quantifier, as 'some person', so its series'
  quantifiers are the language's quantifier words. The markers and words of their own are made
  whatever is drawn, so that a spec can choose. A marker is an Affix or a Particle glossed with
  its series
 */
public record Indefinites(IndefiniteBase PersonBase, IndefiniteBase ThingBase, ExponentPosition MarkerPosition, IReadOnlyDictionary<Quantifier, Morpheme> Markers,
    IReadOnlyDictionary<(Quantifier Series, Ontology Kind), Lexeme> Own, IReadOnlyDictionary<Quantifier, Lexeme> Quantifiers, bool QuantifiersBeforeNoun,
    NegativeIndefiniteNegation Negation)
{
    /// <summary>The quantifiers that make indefinite pronouns, as 'someone', 'nobody' and 'everyone'.</summary>
    public static IReadOnlyList<Quantifier> Series { get; } = [Quantifier.Some, Quantifier.No, Quantifier.Every];

    public static IReadOnlyList<Ontology> Kinds { get; } = Enum.GetValues<Ontology>();

    /// <summary>
    /// An indefinite pronoun, as a noun phrase of its kind's noun with its series, so that 'nobody' is
    /// 'no person', said as the language says its indefinite pronouns.
    /// </summary>
    public static NounPhrase Pronoun(Quantifier series, Ontology kind) => new(NounFor(kind), Number.Singular, Quantifier: series, Indefinite: kind);

    /// <summary>The noun an indefinite of this kind is made from, where it's made from one, as 'some-body' is from a noun for a person.</summary>
    public static string NounFor(Ontology kind) => kind switch
    {
        Ontology.Person => "person",
        Ontology.Thing => "thing",
        Ontology.Place => "place",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public IndefinitePronouns Type => (PersonBase, ThingBase) switch
    {
        (IndefiniteBase.Interrogative, IndefiniteBase.Interrogative) => IndefinitePronouns.InterrogativeBased,
        (IndefiniteBase.GenericNoun, IndefiniteBase.GenericNoun) => IndefinitePronouns.GenericNounBased,
        (IndefiniteBase.Special, IndefiniteBase.Special) => IndefinitePronouns.Special,
        _ => IndefinitePronouns.Mixed,
    };

    public bool MarkersAreAffixes => MarkerPosition is ExponentPosition.Prefix or ExponentPosition.Suffix;

    /// <summary>What a person's, a thing's or a place's indefinite pronouns are made from.</summary>
    public IndefiniteBase BaseOf(Ontology kind) => kind == Ontology.Person ? PersonBase : ThingBase;

    /// <summary>The question word an indefinite of this kind is made from, where it's made from one.</summary>
    public static Interrogative QuestionWordFor(Ontology kind) => kind switch
    {
        Ontology.Person => Interrogative.Who,
        Ontology.Thing => Interrogative.What,
        Ontology.Place => Interrogative.Where,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// Whether this is a negative indefinite, as 'nobody' and 'no traveler' are, or has one joined in
    /// it, as 'nobody and nothing' does.
    /// </summary>
    public static bool IsNegative(Argument argument) => argument.Value switch
    {
        NounPhrase { Quantifier: Quantifier.No } => true,
        Coordination c => c.Items.Any(IsNegative),
        _ => false,
    };
}
