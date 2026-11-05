namespace LanguageGenerator;

//forms are underlying: the phonemes before sandhi applies at morpheme boundaries

//a free morpheme with its own meaning, glossed by that meaning
public record Root(IReadOnlyList<Phoneme> Form, string Meaning);
//a bound morpheme expressing grammatical features. Several grammemes make it fusional
public record Affix(IReadOnlyList<Phoneme> Form, IReadOnlyList<Grammeme> Grammemes);
//a free morpheme expressing grammatical features, standing as a word of its own
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
