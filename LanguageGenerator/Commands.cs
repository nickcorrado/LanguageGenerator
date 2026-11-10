namespace LanguageGenerator;

/// <summary>How a language tells someone to do something, or not to.</summary>
//WALS 70A and 71A. Singular and Plural mark the imperative; where one is missing, the language has
//no form of its own for that number, and says the ordinary second person present instead. A marker
//with no form is a bare stem, the imperative of many languages. Prohibitive is the negator a language
//keeps for commands, as Latin has nē and Greek μή, and without one it uses its ordinary negation
public record Commands(Morpheme? Singular, Morpheme? Plural, ExponentPosition Position, bool ProhibitiveUsesImperative, Negation? Prohibitive)
{
    /// <summary>What marks a command to this many people. Anything but one person is plural here.</summary>
    public Morpheme? MarkerFor(Number number) => number == Number.Singular ? Singular : Plural;

    //an imperative takes no tense or agreement, so it's only the root and its marker
    public IReadOnlyList<Word> Imperative(Lexeme verb, Morpheme marker) => marker.Form.Count == 0
        ? [new Word([verb.Root], marker switch
        {
            Affix a => a.Grammemes,
            Particle p => p.Grammemes,
            Root => [],
        })]
        : Position switch
        {
            ExponentPosition.Prefix => [new Word([marker, verb.Root])],
            ExponentPosition.Suffix => [new Word([verb.Root, marker])],
            ExponentPosition.PrecedingParticle => [new Word([marker]), new Word([verb.Root])],
            _ => [new Word([verb.Root]), new Word([marker])],
        };
}
