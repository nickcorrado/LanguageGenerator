namespace LanguageGenerator;

//I figure this is the top-level object
//a plain description of a language: everything Generator chose, and nothing about how
public class Language
{
    public string? Name { get; init; }
    public required Phonology Phonology { get; init; }
    public required List<Lexeme> Lexicon { get; init; }
    //how each part of speech inflects
    public required Dictionary<PartOfSpeech, Paradigm> Paradigms { get; init; }
    public required MorphologicalProfile MorphologicalProfile { get; init; }
    public required WordOrder WordOrder { get; init; }
    public required bool HasArticles { get; init; }
    public required MorphosyntacticAlignment MorphosyntacticAlignment { get; init; }
    public required List<Case> Cases { get; init; }
    public required Dictionary<CaseUse, RoleMarker> RoleMarkers { get; init; }

    //the classic three-way label, for display only, read off what the paradigms actually do:
    //analytic if markers stand alone, fusional if any marker expresses more than one category
    //(person and number together don't count, since nearly every language fuses those)
    public Morphology Morphology =>
        MorphologicalProfile.Fusion == Fusion.Isolating ? Morphology.Analytic
        : Paradigms.Values.SelectMany(p => p.Slots).Any(s => s.Categories.Count(c => !(c.Name == "Number" && s.Categories.Any(x => x.Name == "Person"))) > 1) ? Morphology.Fusional
        : Morphology.Agglutinative;

    //the lexeme with this meaning, like "dog"
    public Lexeme FindLexeme(string meaning) => Lexicon.Single(l => l.Root.Meaning == meaning);
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

//how a language builds words, along the dimensions typologists actually measure
public record MorphologicalProfile(Fusion Fusion, Exponence Exponence, Affixation Affixation)
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
    //TODO Dryer's chapter says suffixing is commoner in OV languages, but the 26A counts alone
    //don't say by how much; that needs WALS's dataset, cross-tabulated with word order
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
        return $"{Fusion}, {Affixation}, fuses {(fused.Count == 0 ? "nothing" : string.Join(", ", fused))}";
    }
}
