namespace LanguageGenerator;

//forms are underlying: the phonemes before sound changes apply where morphemes meet

/// <summary>
/// A free morpheme with its own meaning, glossed by that meaning. Where stress is free, Accent is
/// which of its vowels it stores as stressed, counting from 0, or null if it stores none.
/// </summary>
public record Root(IReadOnlyList<Phoneme> Form, string Meaning, int? Accent);
/// <summary>
/// A bound morpheme expressing one or more grammemes. Several make it fusional. Where stress is
/// free, Accent is where it puts the stress, if anywhere.
/// </summary>
public record Affix(IReadOnlyList<Phoneme> Form, IReadOnlyList<Grammeme> Grammemes, AffixAccent Accent);

public enum AffixAccentKind
{
    //it leaves the stress to the rest of the word
    None,
    //it stores one of its own vowels as stressed, as Russian -á in ruk-á 'hand' does
    Own,
    //it stresses the syllable before it, as Turkish negative -mA does in gél-me-di 'didn't come'
    Before,
}

/// <summary>
/// Where an affix puts the stress. Like a root's accent, it's one accent among the word's
/// morphemes, and the first of them in the word is the one that's stressed.
/// </summary>
/*
  so an unaccented root takes its stress from its affixes where they have one and from the rule
  where they don't, which is how Russian's nouns with stress that moves through the paradigm come
  about: ruk-á 'hand' but rúk-u 'hand (ACC)'
 */
public sealed record AffixAccent
{
    private AffixAccent(AffixAccentKind kind, int vowel) => (Kind, Vowel) = (kind, vowel);

    public AffixAccentKind Kind { get; }
    /// <summary>Which of the affix's vowels it stores as stressed, counting from 0, for an Own accent.</summary>
    public int Vowel { get; }

    public static AffixAccent None { get; } = new(AffixAccentKind.None, 0);
    public static AffixAccent Before { get; } = new(AffixAccentKind.Before, 0);
    public static AffixAccent Own(int vowel) => new(AffixAccentKind.Own, vowel);
}
/// <summary>A free morpheme expressing grammemes, standing as a word of its own.</summary>
public record Particle(IReadOnlyList<Phoneme> Form, IReadOnlyList<Grammeme> Grammemes);

public union Morpheme(Root, Affix, Particle)
{
    public IReadOnlyList<Phoneme> Form => this switch
    {
        Root r => r.Form,
        Affix a => a.Form,
        Particle p => p.Form,
    };

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //a root is glossed by its meaning, and grammatical morphemes by their grammemes
    public string Gloss => this switch
    {
        Root r => r.Meaning,
        Affix a => Grammeme.JoinGlosses(a.Grammemes),
        Particle p => Grammeme.JoinGlosses(p.Grammemes),
    };

    public string Spell(Orthography orthography) => orthography.Spell(Form);
}
