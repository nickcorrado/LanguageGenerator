namespace LanguageGenerator;

//I figure this is the top-level object
//a plain description of a language: everything Generator chose, and nothing about how
public class Language
{
    public string? Name { get; init; }
    public required List<Vowel> Vowels { get; init; }
    public required List<Consonant> Consonants { get; init; }
    //only one rule now!
    public required SyllableRule SyllableRule { get; init; }
    public required List<Word> Words { get; init; }
    public required Morphology Morphology { get; init; }
    public required WordOrder WordOrder { get; init; }
    public required bool HasArticles { get; init; }
    public required MorphosyntacticAlignment MorphosyntacticAlignment { get; init; }
    public required List<Case> Cases { get; init; }
    public required Dictionary<CaseUse, RoleMarker> RoleMarkers { get; init; }
}

public enum Morphology
{
    Analytic,
    Fusional,
    Agglutinative,
    //planning to leave out polysynthetic or the dubious oligosynthetic
}
