namespace LanguageGenerator;

public class Word
{
    public Word()
    {
        Syllables = [];
    }

    public List<Syllable> Syllables { get; set; }
    public PartOfSpeech PartOfSpeech { get; set; }

    //this passes through a bunch of intermediate steps, which sorta bothers me
    //We may want to override ToString() instead
    public string Transcription => string.Concat(Syllables.Select(s => s.Transcription));

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //I'm going to give every grammatical feature its own gloss. In-morpheme features are separated by
    //periods and in-word features are separated by hyphens. Meaning is first, separated by hyphen if
    //it's a stem or by period if it's all one morpheme. Person and number labels are not separated by
    //periods
    public string? Gloss { get; set; }
}

//n.b. not every language has all these. A language might not have articles!
//I'm thinking I make a closed class of conjunctions, and probably adpositions,
//corresponding to what's found in English
public enum PartOfSpeech
{
    Noun,
    Verb,
    Adjective,
    Adverb,
    Pronoun,
    Adposition,
    Conjunction,
    Interjection,
    Article,
}

public enum AdpositionType
{
    Preposition,
    Postposition,
}

//all super tentative
public class AnalyticWord : Word
{
    //analytic words may be compounds, but they don't contain a bunch of functional morphemes
}

public class FusionalWord : Word
{
    //this design is silly, since fusional words can have prefixes, too, and extra suffixes
    public required Morpheme Stem { get; set; }
    //nullable, because not all parts of speech have an ending, even if the language generally is fusional
    public Morpheme? Ending { get; set; }
    
    //may have number, gender, case, animacy, person.... but endings need not carry any of this info
    //shouldn't this be decided at the language level? Also, what carries what is determined by part of speech
    
}

public class FusionalVerb : FusionalWord
{
    public FusionalVerb()
    {
        PartOfSpeech = PartOfSpeech.Verb;
    }

    //any of these may be null!
    public List<Tense>? Tenses { get; set; }
    public List<Aspect>? Aspects { get; set; }
    public List<Mood>? Moods { get; set; }
    public List<Voice>? Voices { get; set; }
    public List<Person>? Persons { get; set; }
    public List<Number>? Numbers { get; set; }
}

public class FusionalNoun : FusionalWord
{
    public FusionalNoun()
    {
        PartOfSpeech = PartOfSpeech.Noun;
    }

    //any of these may be null!
    public List<Number>? Numbers { get; set; }
    public List<Case>? Cases { get; set; }
    public List<Gender>? Genders { get; set; }
}

public class FusionalAdposition : FusionalWord
{
    //preposition or postposition
    public AdpositionType Adposition { get; set; }
    //if the language has cases, what case does it govern
    public Case? Case { get; set; }
}

public class AgglutinativeWord : Word
{
    public List<Morpheme> Affixes { get; set; } = [];
}
