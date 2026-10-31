namespace LanguageGenerator;

//a word is a sequence of morphemes, and everything else about it is derived from them.
//its spellings here are of the underlying form, the morphemes simply run together;
//Phonology.Realize gives the surface form, after sandhi and syllabification. Covert holds
//features the word has but no morpheme expresses, like the singular of most nouns
public record Word(IReadOnlyList<Morpheme> Morphemes, IReadOnlyList<Tagmeme>? Covert = null)
{
    public string Spell(Orthography orthography) => string.Concat(Morphemes.Select(m => m.Spell(orthography)));

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //I'm going to give every grammatical feature its own gloss. In-morpheme features are separated by
    //periods and in-word features are separated by hyphens. Meaning is first, separated by hyphen if
    //it's a stem or by period if it's all one morpheme. Person and number labels are not separated by
    //periods
    //Segmented and Gloss are the second and third lines of an interlinear gloss, and line up
    //morpheme for morpheme
    public string Segmented(Orthography orthography) => string.Join("-", Morphemes.Select(m => m.Spell(orthography)));
    //Leipzig rule 6: features with no form of their own go in brackets on the root, as in boy[NOM.SG]
    public string Gloss => string.Join("-", Morphemes.Select(m =>
        m.Value is Root && Covert is { Count: > 0 } covert ? $"{m.Gloss}[{Tagmeme.JoinGlosses(covert)}]" : m.Gloss));
}

//a dictionary entry: a root, and what it can be inflected as
public record Lexeme(Root Root, PartOfSpeech PartOfSpeech);

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
