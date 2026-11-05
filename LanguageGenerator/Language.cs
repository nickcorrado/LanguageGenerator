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
    public required Dictionary<PartOfSpeech, Paradigm> Paradigms { get; init; }
    public required MorphologicalProfile MorphologicalProfile { get; init; }
    public required WordOrder WordOrder { get; init; }
    public required bool HasArticles { get; init; }
    public required MorphosyntacticAlignment MorphosyntacticAlignment { get; init; }
    public required List<Case> Cases { get; init; }
    public required Dictionary<Role, RoleMarker> RoleMarkers { get; init; }
    public required PronounSystem Pronouns { get; init; }
    public required Negation Negation { get; init; }
    public required Predication Predication { get; init; }
    public required AdjectiveAgreement AdjectiveAgreement { get; init; }
    public required GenderSystem Genders { get; init; }
    public required NumeralSystem Numerals { get; init; }
    public required Possession Possession { get; init; }

    /// <summary>
    /// What the language calls itself: its word for 'people', spelled its own way and capitalized
    /// like a name.
    /// </summary>
    //the plural of 'person', as Bantu is the plural of -ntu 'person' and Inuit of inuk. A language
    //that doesn't mark the plural calls itself just 'person', as many do. It's read off the
    //lexicon, so it's always the word the language itself would say
    public string Name => Capitalized(Endonym.Spell(Orthography));

    /// <summary>The word Name spells, for its gloss.</summary>
    public SurfaceForm Endonym => Phonology.Realize(Syntax.Cited(this, new NounPhrase("person", Number.Plural)).First(w => w.Root is not null));

    private static string Capitalized(string spelling) => spelling.Length == 0 ? spelling : char.ToUpperInvariant(spelling[0]) + spelling[1..];

    /// <summary>
    /// The classic three-way label, for display only, read off what the paradigms actually do.
    /// </summary>
    //analytic if markers stand alone, fusional if any marker expresses more than one category
    //(person and number together don't count, since nearly every language fuses those)
    public MorphologicalType MorphologicalType =>
        MorphologicalProfile.Fusion == Fusion.Isolating ? MorphologicalType.Analytic
        : Paradigms.Values.SelectMany(p => p.Slots).Any(s => s.Categories.Count(c => !(c.Is<Number>() && s.Categories.Has<Person>())) > 1) ? MorphologicalType.Fusional
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
public record Exponence(bool CaseWithNumber, bool TamWithAgreement, bool TenseWithAspect);

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
    //whether this combination of values, all in one slot, has no form. Every value in it has to be
    //one the language leaves unmarked, so a fused SG.NOM is zero only if both singular and the core
    //case are, and a plural is never zero except as part of a zero third person
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
    /// <summary>How often a new marker is a separate word rather than an affix.</summary>
    public double ParticleChance => Fusion switch
    {
        Fusion.Concatenative => 0,
        Fusion.Mixed => 0.5,
        Fusion.Isolating => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(Fusion), Fusion, null),
    };

    //the middle of each 26A band, which WALS defines by the share of affixes that are prefixes
    public double PrefixChance => Affixation switch
    {
        Affixation.StronglySuffixing => 0.1,
        Affixation.WeaklySuffixing => 0.3,
        Affixation.Equal => 0.5,
        Affixation.WeaklyPrefixing => 0.7,
        Affixation.StronglyPrefixing => 0.9,
        _ => throw new ArgumentOutOfRangeException(nameof(Affixation), Affixation, null),
    };

    public override string ToString()
    {
        (bool Fused, string Name)[] pairs = [(Exponence.CaseWithNumber, "case+number"), (Exponence.TamWithAgreement, "TAM+agreement"), (Exponence.TenseWithAspect, "tense+aspect")];
        var fused = pairs.Where(p => p.Fused).Select(p => p.Name).ToList();
        return $"{Fusion}, {Affixation}, fuses {(fused.Count == 0 ? "nothing" : string.Join(", ", fused))}, {ZeroMarking}";
    }
}
