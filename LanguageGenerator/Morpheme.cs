namespace LanguageGenerator;

public class Morpheme
{
    public MorphemeType Type { get; set; }
    //n.b. when this collides with other sounds, weird things happen!
    public List<Phoneme> IsolatedMorpheme { get; set; } = [];
    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //if this morpheme has its own meaning, that comes first. It and
    //grammatical features are separated by periods. Person and number
    //labels are not separated by periods.
    public string Gloss { get; protected set; } = "";
}

//crazy iffy
public class FusionalMorpheme : Morpheme
{
    List<Tagmeme> Tagmemes { get; set; }
    public FusionalMorpheme(List<Tagmeme> tagmemes)
    {
        Tagmemes = tagmemes;
        for (int i = 0; i < Tagmemes.Count; i++)
        {
            if (i > 0)
            {
                Gloss += Tagmeme.Separator(Tagmemes[i - 1], Tagmemes[i]);
            }
            Gloss += Tagmemes[i].Gloss;
        }
    }
}

public class AgglutinativeMorpheme : Morpheme
{
    Tagmeme Tagmeme { get; set; }
    public AgglutinativeMorpheme(Tagmeme tagmeme)
    {
        Tagmeme = tagmeme;
        Gloss += Tagmeme.Gloss;
    }
}

//are Stem and Ending subclasses of Morpheme? and Affix?

public enum MorphemeType
{
    Free,
    Bound,
}

//I think all of these should be classes and inherit from Tagmeme

