namespace LanguageGenerator;

//forms are underlying: the phonemes before sandhi applies at morpheme boundaries

/// <summary>A free morpheme with its own meaning, glossed by that meaning.</summary>
public record Root(IReadOnlyList<Phoneme> Form, string Meaning);
/// <summary>A bound morpheme expressing one or more grammemes. Several make it fusional.</summary>
public record Affix(IReadOnlyList<Phoneme> Form, IReadOnlyList<Grammeme> Grammemes);
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
