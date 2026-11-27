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
/// as 'someone', a thing, as 'something', a place, as 'somewhere', or a time, as 'sometime' and
/// 'never'. These are the categories the question words who, what, where and when ask about.
/// </summary>
public enum Ontology
{
    Person,
    Thing,
    Place,
    Time,
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

/// <summary>
/// What the indefinite pronouns of persons, or of things, places and times, are made from: question
/// words, a generic noun, or words of their own.
/// </summary>
public union IndefiniteSource(InterrogativeIndefinites, GenericNounIndefinites, SpecialIndefinites);

/// <summary>
/// Question words, each with a marker for its series, as Japanese dare-ka 'who-INDF' and dare-mo
/// 'who-every', which goes on the question word as an affix or a word.
/// </summary>
public record InterrogativeIndefinites(IReadOnlyDictionary<Quantifier, Exponent> Markers);

/// <summary>A generic noun with its series' quantifier, as 'some person' is 'somebody'.</summary>
public record GenericNounIndefinites;

/// <summary>A word of its own for each series of each kind, as Latin aliquis 'someone'.</summary>
public record SpecialIndefinites(IReadOnlyDictionary<(Quantifier Series, Ontology Kind), Lexeme> Words);

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
/// A language's quantifiers and indefinite pronouns: what 'someone' and 'nobody' are made from, and
/// what 'something', 'nowhere' and 'never' are, places and times going as things do; a word for each
/// quantifier, which goes before or after its noun; and whether a negative indefinite takes the
/// verb's negation too.
/// </summary>
//an indefinite made from a noun is that noun with its quantifier, so its series' quantifiers are the language's quantifier words
public record Indefinites(IndefiniteSource Persons, IndefiniteSource Things, IReadOnlyDictionary<Quantifier, Lexeme> Quantifiers, bool QuantifiersBeforeNoun,
    NegativeIndefiniteNegation Negation)
{
    /// <summary>The quantifiers that make indefinite pronouns, as 'someone', 'nobody' and 'everyone'.</summary>
    public static IReadOnlyList<Quantifier> Series { get; } = [Quantifier.Some, Quantifier.No, Quantifier.Every];

    public static IReadOnlyList<Ontology> Kinds { get; } = Enum.GetValues<Ontology>();

    /// <summary>The noun an indefinite of this kind is made from, where it's made from one, as 'some-body' is from a noun for a person.</summary>
    public static string NounFor(Ontology kind) => kind switch
    {
        Ontology.Person => "person",
        Ontology.Thing => "thing",
        Ontology.Place => "place",
        Ontology.Time => "time",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>What the indefinite pronouns are made from, as a spec names it.</summary>
    public IndefinitePronouns Type => (Persons.Value, Things.Value) switch
    {
        (InterrogativeIndefinites, InterrogativeIndefinites) => IndefinitePronouns.InterrogativeBased,
        (GenericNounIndefinites, GenericNounIndefinites) => IndefinitePronouns.GenericNounBased,
        (SpecialIndefinites, SpecialIndefinites) => IndefinitePronouns.Special,
        _ => IndefinitePronouns.Mixed,
    };

    /// <summary>The indefinite pronouns' words of their own, where they have any.</summary>
    public IEnumerable<Lexeme> Own => new[] { Persons.Value, Things.Value }.Distinct().OfType<SpecialIndefinites>().SelectMany(s => s.Words.Values);

    /// <summary>
    /// The roles an indefinite of this kind can fill, or null for any a noun can: a place says where,
    /// to where or from where, as 'somewhere' does, and a time when, as 'never' does.
    /// </summary>
    public static IReadOnlyList<Role>? RolesOf(Ontology kind) => kind switch
    {
        Ontology.Place => [Role.Location, Role.Goal, Role.Source],
        Ontology.Time => [Role.Time],
        Ontology.Person or Ontology.Thing => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>What the indefinite pronouns of a kind are made from, places and times going as things do.</summary>
    public IndefiniteSource SourceOf(Ontology kind) => kind switch
    {
        Ontology.Person => Persons,
        Ontology.Thing or Ontology.Place or Ontology.Time => Things,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The question word an indefinite of this kind is made from, where it's made from one.</summary>
    public static Interrogative QuestionWordFor(Ontology kind) => kind switch
    {
        Ontology.Person => Interrogative.Who,
        Ontology.Thing => Interrogative.What,
        Ontology.Place => Interrogative.Where,
        Ontology.Time => Interrogative.When,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// Whether this is a negative indefinite, as 'nobody' and 'no traveler' are, or has one joined in
    /// it, as 'nobody and nothing' does.
    /// </summary>
    public static bool IsNegative(Argument argument) => argument.Value switch
    {
        NounPhrase { Head.Value: Noun { Quantifier: Quantifier.No } or IndefinitePronoun { Series: Quantifier.No } } => true,
        Coordination c => c.Items.Any(IsNegative),
        _ => false,
    };
}
