namespace LanguageGenerator;

//I figure this is the top-level object
//a plain description of a language: everything Generator chose, and nothing about how, except
//that the lexicon can coin roots for new meanings the way Generator would have
public class Language
{
    public string? Name { get; init; }
    public required Phonology Phonology { get; init; }
    public required Orthography Orthography { get; init; }
    public required Lexicon Lexicon { get; init; }
    //how each part of speech inflects
    public required Dictionary<PartOfSpeech, Paradigm> Paradigms { get; init; }
    public required MorphologicalProfile MorphologicalProfile { get; init; }
    public required WordOrder WordOrder { get; init; }
    public required bool HasArticles { get; init; }
    public required MorphosyntacticAlignment MorphosyntacticAlignment { get; init; }
    public required List<Case> Cases { get; init; }
    public required Dictionary<CaseUse, RoleMarker> RoleMarkers { get; init; }
    public required PronounSystem Pronouns { get; init; }
    public required Negation Negation { get; init; }
    public required Predication Predication { get; init; }
    public required AdjectiveAgreement AdjectiveAgreement { get; init; }
    public required GenderSystem Genders { get; init; }

    //the classic three-way label, for display only, read off what the paradigms actually do:
    //analytic if markers stand alone, fusional if any marker expresses more than one category
    //(person and number together don't count, since nearly every language fuses those)
    public Morphology Morphology =>
        MorphologicalProfile.Fusion == Fusion.Isolating ? Morphology.Analytic
        : Paradigms.Values.SelectMany(p => p.Slots).Any(s => s.Categories.Count(c => !(c.Name == "Number" && s.Categories.Any(x => x.Name == "Person"))) > 1) ? Morphology.Fusional
        : Morphology.Agglutinative;
}

//WALS doesn't use these labels, because they lump together properties that vary independently;
//MorphologicalProfile holds those properties
public enum Morphology
{
    Analytic,
    Fusional,
    Agglutinative,
    //planning to leave out polysynthetic or the dubious oligosynthetic
}

//WALS 20A: whether grammatical markers are bound to words or stand alone. Not to be confused
//with fusional morphology, which is about exponence
public enum Fusion
{
    Concatenative,
    Mixed,
    Isolating,
}

//WALS 26A: how much of a language's inflection goes before the stem rather than after it
public enum Affixation
{
    StronglySuffixing,
    WeaklySuffixing,
    Equal,
    WeaklyPrefixing,
    StronglyPrefixing,
}

//WALS 21A and 21B: which categories share a single marker. Person and number agreement aren't
//here because they always do
public record Exponence(bool CaseWithNumber, bool TamWithAgreement, bool TenseWithAspect);

//WALS 103A: whether third person agreement is marked at all
public enum ThirdPersonZero
{
    None,
    //zero in the third person singular only, as in Hungarian lát 'sees'
    Singular,
    //zero throughout the third person
    All,
}

//which values a language leaves unmarked, expressed by no form at all
public record ZeroMarking(bool Singular, bool CoreCase, bool Present, ThirdPersonZero ThirdPerson)
{
    //whether this combination of values, all in one slot, has no form. Every value in it has to be
    //one the language leaves unmarked, so a fused SG.NOM is zero only if both singular and the core
    //case are, and a plural is never zero except as part of a zero third person
    public bool IsZero(IReadOnlyList<Tagmeme> values)
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

//how a language builds words, along the dimensions typologists actually measure
public record MorphologicalProfile(Fusion Fusion, Exponence Exponence, Affixation Affixation, ZeroMarking ZeroMarking)
{
    //how often a new marker is a separate word rather than an affix
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
