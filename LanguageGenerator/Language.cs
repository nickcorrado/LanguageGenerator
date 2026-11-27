namespace LanguageGenerator;

/// <summary>
/// A plain description of a language: everything Generator chose, and nothing about how.
/// </summary>
//except that the lexicon can coin roots for new meanings the way Generator would have
public class Language
{
    public required Phonology Phonology { get; init; }
    public required Orthography Orthography { get; init; }
    public required Lexicon Lexicon { get; init; }
    /// <summary>How each part of speech inflects.</summary>
    public required Paradigms Paradigms { get; init; }
    public required MorphologicalProfile MorphologicalProfile { get; init; }
    public required WordOrder WordOrder { get; init; }
    public required Articles Articles { get; init; }
    /// <summary>How 'this' and 'that' are said: beside a noun, standing alone, and as 'here' and 'there'.</summary>
    public required Demonstratives Demonstratives { get; init; }
    public required Indefinites Indefinites { get; init; }
    public required MorphosyntacticAlignment MorphosyntacticAlignment { get; init; }
    public required RoleRegions RoleRegions { get; init; }
    public required IReadOnlyList<Case> Cases { get; init; }
    public required IReadOnlyDictionary<Role, RoleMarker> RoleMarkers { get; init; }
    /// <summary>How the language says in, on, under and the rest, beside the role a place is in.</summary>
    public required Configurations Configurations { get; init; }
    public required PronounSystem Pronouns { get; init; }
    public required Negation Negation { get; init; }
    public required Predication Predication { get; init; }
    public required AdjectiveAgreement AdjectiveAgreement { get; init; }
    public required GenderSystem Genders { get; init; }
    public required NumeralSystem Numerals { get; init; }
    public required Possession Possession { get; init; }
    public required Commands Commands { get; init; }
    public required Comparison Comparison { get; init; }
    public required DegreeMarking Degree { get; init; }
    public required Similative Similative { get; init; }
    /// <summary>The word for 'very', beside the adjective.</summary>
    public required Free Intensifier { get; init; }
    /// <summary>How an adverb saying how something is done is made from an adjective.</summary>
    public required MannerMarking Manner { get; init; }
    /// <summary>How the language marks a passive verb, or null where it has no passive.</summary>
    public required Passive? Passive { get; init; }
    public required Conjunctions Conjunctions { get; init; }
    public required Subordination Subordination { get; init; }
    /// <summary>How clauses are marked as objects, and quotations as what was said.</summary>
    public required Complementation Complementation { get; init; }
    /// <summary>How relative clauses are made: where they go, how their noun's role is said, and what marks them.</summary>
    public required Relativization Relativization { get; init; }
    public required Modals Modals { get; init; }
    /// <summary>How the language marks the perfect, the progressive and the habitual, and beginning and ceasing.</summary>
    public required Aspects Aspects { get; init; }
    public required Questions Questions { get; init; }

    /// <summary>
    /// The verb an aspect's auxiliary is: the language's own 'have', 'finish' or 'sit', or its copula,
    /// which for a progressive is the one that says where, as Spanish estar is, and otherwise the
    /// one that says what.
    /// </summary>
    //the lexicon's verbs are coined when the language is generated, so that a frozen lexicon has them
    public Lexeme AuxiliaryVerb(AspectAuxiliary auxiliary, Aspect aspect) => auxiliary.Verb switch
    {
        AspectVerb.Have or AspectVerb.Finish or AspectVerb.Sit => Lexicon.Verb(AuxiliaryMeaning(auxiliary.Verb)!),
        AspectVerb.Be => Predication.CopulaFor(locational: aspect == Aspect.Progressive),
        _ => throw new ArgumentOutOfRangeException(nameof(auxiliary), auxiliary.Verb, null),
    };

    /// <summary>The meaning of the lexicon's verb an auxiliary is, or null for the copula.</summary>
    public static string? AuxiliaryMeaning(AspectVerb verb) => verb switch
    {
        AspectVerb.Have => "have",
        AspectVerb.Finish => "finish",
        AspectVerb.Sit => "sit",
        AspectVerb.Be => null,
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, null),
    };

    public Gender? GenderOf(Lexeme noun) => Genders.Of(noun, Lexicon.ClassOf(noun.Root.Meaning));

    /// <summary>
    /// What the language calls itself: its word for 'people', spelled its own way and capitalized
    /// like a name.
    /// </summary>
    /*
      the plural of 'person', as Bantu is the plural of -ntu 'person' and Inuit of inuk. A language
      that doesn't mark the plural calls itself just 'person', as many do. It's read off the
      lexicon, so it's always the word the language itself would say
     */
    public string Name => Capitalized(Endonym.Spell(Orthography));

    /// <summary>The word Name spells, for its gloss, said with the clitics its phrase leans on it.</summary>
    public SentenceWord Endonym => Phonology.RealizePhrase(Syntax.Cited(this, new NounPhrase(new Noun("person", Number.Plural, [], null, null, null, null, null), null)))
        .SelectMany(g => g.Words.Select(w => new SentenceWord(g, w))).First(w => w.Word.Root is not null);

    private static string Capitalized(string spelling) => spelling.Length == 0 ? spelling : char.ToUpperInvariant(spelling[0]) + spelling[1..];

    /// <summary>
    /// The classic three-way label, for display only, read off what the paradigms actually do.
    /// </summary>
    //analytic if markers stand alone, fusional if any marker expresses more than one category
    //(person and number together don't count, since nearly every language fuses those)
    public MorphologicalType MorphologicalType =>
        MorphologicalProfile.Fusion == Fusion.Isolating ? MorphologicalType.Analytic
        : Paradigms.All.SelectMany(p => p.Slots).Any(s => s.Categories.Count(c => !(c.Is<Number>() && s.Categories.Has<Person>())) > 1) ? MorphologicalType.Fusional
        : MorphologicalType.Agglutinative;
}

//WALS doesn't use these labels, because they lump together properties that vary independently;
//MorphologicalProfile holds those properties
public enum MorphologicalType
{
    Analytic,
    Fusional,
    Agglutinative,
}

/// <summary>
/// Whether grammatical markers are bound to words or stand alone. Not to be confused with fusional
/// morphology, which is about exponence.
/// </summary>
//WALS 20A
public enum Fusion
{
    Concatenative,
    Mixed,
    Isolating,
}

/// <summary>
/// How much of a language's inflection goes before the stem rather than after it.
/// </summary>
//WALS 26A
public enum Affixation
{
    StronglySuffixing,
    WeaklySuffixing,
    Equal,
    WeaklyPrefixing,
    StronglyPrefixing,
}

/// <summary>Which categories share a single marker.</summary>
//WALS 21A and 21B. Person and number agreement aren't here because they always do
public record Exponence(bool CaseWithNumber, bool TamWithAgreement, bool TenseWithAspect, bool CaseWithLocalization);

/// <summary>Whether third person agreement is marked at all.</summary>
//WALS 103A
public enum ThirdPersonZero
{
    None,
    //zero in the third person singular only, as in Hungarian lát 'sees'
    Singular,
    //zero throughout the third person
    All,
}

/// <summary>Which values a language leaves unmarked, expressed by no form at all.</summary>
public record ZeroMarking(bool Singular, bool CoreCase, bool Present, ThirdPersonZero ThirdPerson)
{
    /*
      whether this combination of values, all in one slot, has no form. Every value in it has to be
      one the language leaves unmarked, so a fused SG.NOM is zero only if both singular and the core
      case are, and a plural is never zero except as part of a zero third person
     */
    public bool IsZero(IReadOnlyList<Grammeme> values)
    {
        var agreement = values.Any(v => v is Person);
        var thirdPersonZero = values.Contains(Person.Third) && ThirdPerson switch
        {
            ThirdPersonZero.Singular => values.Contains(Number.Singular),
            ThirdPersonZero.All => true,
            _ => false,
        };
        return values.All(v => v switch
        {
            Person => thirdPersonZero,
            //number in agreement goes with the person it's fused with
            Number n => agreement ? thirdPersonZero : Singular && n == Number.Singular,
            //the case for the intransitive subject: nominative, absolutive, or direct
            Case c => CoreCase && (c == Case.Nominative || c == Case.Absolutive || c == Case.Direct),
            Tense t => Present && t == Tense.Present,
            //a noun that isn't spatial has no localization to mark
            Localization l => l == Localization.None,
            //no aspect, mood or voice value is reliably the unmarked one
            _ => false,
        });
    }

    public override string ToString()
    {
        (bool Zero, string Name)[] values = [(Singular, "SG"), (CoreCase, "core case"), (Present, "PRS"), (ThirdPerson == ThirdPersonZero.Singular, "3SG"), (ThirdPerson == ThirdPersonZero.All, "3rd person")];
        var zero = values.Where(v => v.Zero).Select(v => v.Name).ToList();
        return zero.Count == 0 ? "marks everything" : $"leaves {string.Join(", ", zero)} unmarked";
    }
}

/// <summary>
/// How a language builds words, along the dimensions typologists actually measure.
/// </summary>
public record MorphologicalProfile(Fusion Fusion, Exponence Exponence, Affixation Affixation, ZeroMarking ZeroMarking)
{
    public override string ToString()
    {
        (bool Fused, string Name)[] pairs = [(Exponence.CaseWithNumber, "case+number"), (Exponence.TamWithAgreement, "TAM+agreement"), (Exponence.TenseWithAspect, "tense+aspect")];
        var fused = pairs.Where(p => p.Fused).Select(p => p.Name).ToList();
        return $"{Fusion}, {Affixation}, fuses {(fused.Count == 0 ? "nothing" : string.Join(", ", fused))}, {ZeroMarking}";
    }
}
