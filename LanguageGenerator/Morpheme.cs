namespace LanguageGenerator;

//forms are underlying: the phonemes before sandhi applies at morpheme boundaries
//n.b. when this collides with other sounds, weird things happen!

//a free morpheme with its own meaning, glossed by that meaning
public record Root(IReadOnlyList<Phoneme> Form, string Meaning);
//a bound morpheme expressing grammatical features. Several tagmemes make it fusional
public record Affix(IReadOnlyList<Phoneme> Form, IReadOnlyList<Tagmeme> Tagmemes);
//a free morpheme expressing grammatical features, standing as a word of its own
public record Particle(IReadOnlyList<Phoneme> Form, IReadOnlyList<Tagmeme> Tagmemes);

public union Morpheme(Root, Affix, Particle)
{
    public IReadOnlyList<Phoneme> Form => this switch
    {
        Root r => r.Form,
        Affix a => a.Form,
        Particle p => p.Form,
    };

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //a root is glossed by its meaning, and grammatical morphemes by their tagmemes
    public string Gloss => this switch
    {
        Root r => r.Meaning,
        Affix a => Tagmeme.JoinGlosses(a.Tagmemes),
        Particle p => Tagmeme.JoinGlosses(p.Tagmemes),
    };

    public string Transcription => string.Concat(Form.Select(p => p.Transcription));
}
