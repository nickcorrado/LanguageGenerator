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
    IReadOnlyDictionary<(Quantifier Series, string Kind), Lexeme> Own, IReadOnlyDictionary<Quantifier, Lexeme> Quantifiers, bool QuantifiersBeforeNoun,
    NegativeIndefiniteNegation Negation)
{
    /// <summary>The quantifiers that make indefinite pronouns, as 'someone', 'nobody' and 'everyone'.</summary>
    public static IReadOnlyList<Quantifier> Series { get; } = [Quantifier.Some, Quantifier.No, Quantifier.Every];

    //what an indefinite pronoun is, as the noun of a NounPhrase that's Pronominal
    public const string Person = "person";
    public const string Thing = "thing";
    public const string Place = "place";
    public static IReadOnlyList<string> Kinds { get; } = [Person, Thing, Place];

    public IndefinitePronouns Type => (PersonBase, ThingBase) switch
    {
        (IndefiniteBase.Interrogative, IndefiniteBase.Interrogative) => IndefinitePronouns.InterrogativeBased,
        (IndefiniteBase.GenericNoun, IndefiniteBase.GenericNoun) => IndefinitePronouns.GenericNounBased,
        (IndefiniteBase.Special, IndefiniteBase.Special) => IndefinitePronouns.Special,
        _ => IndefinitePronouns.Mixed,
    };

    public bool MarkersAreAffixes => MarkerPosition is ExponentPosition.Prefix or ExponentPosition.Suffix;

    /// <summary>What a person's, a thing's or a place's indefinite pronouns are made from.</summary>
    public IndefiniteBase BaseOf(string kind) => kind == Person ? PersonBase : ThingBase;

    /// <summary>The question word an indefinite of this kind is made from, where it's made from one.</summary>
    public static Interrogative QuestionWordFor(string kind) => kind switch
    {
        Person => Interrogative.Who,
        Thing => Interrogative.What,
        Place => Interrogative.Where,
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
