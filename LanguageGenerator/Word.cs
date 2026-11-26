namespace LanguageGenerator;

/// <summary>
/// A word as a sequence of morphemes, in its underlying form; Phonology.Realize gives the surface
/// form. Covert holds features the word has but no morpheme expresses, like the singular of most
/// nouns.
/// </summary>
public record Word(IReadOnlyList<Morpheme> Morphemes, IReadOnlyList<Grammeme>? Covert = null)
{
    /// <summary>The word's root, if it has one: a particle's word doesn't.</summary>
    public Root? Root => Morphemes.Select(m => m.Value).OfType<Root>().FirstOrDefault();

    /// <summary>
    /// Which way a word without a root leans for its stress and sound, as a clitic: onto the word
    /// after it or the one before. A word with a root never leans.
    /// </summary>
    /*
      only a word without a root leans, and every such word has to, which Phonology.RealizePhrase
      checks: syntax knows which side of its head it put a particle on, and nothing else does
     */
    public Lean? Leans { get; init; }

    /// <summary>
    /// A word of one morpheme that marks the word next to it this way: a clitic leaning on it, unless
    /// the morpheme is a root, which stands on its own, as an indefinite article that's 'one' does.
    /// </summary>
    public static Word Marker(Morpheme morpheme, Lean leans) => morpheme.Value is Root ? new([morpheme]) : new([morpheme]) { Leans = leans };

    public string Spell(Orthography orthography) => string.Concat(Morphemes.Select(m => m.Spell(orthography)));

    /*
       https://www.eva.mpg.de/lingua/resources/glossing-rules.php
      I'm going to give every grammatical feature its own gloss. In-morpheme features are separated by
      periods and in-word features are separated by hyphens. Meaning is first, separated by hyphen if
      it's a stem or by period if it's all one morpheme. Person and number labels are not separated by
      periods
      Segmented and Gloss are the second and third lines of an interlinear gloss, and line up
      morpheme for morpheme
     */
    public string Segmented(Orthography orthography) => string.Join("-", Morphemes.Select(m => m.Spell(orthography)));
    //Leipzig rule 6: features with no form of their own go in brackets on the root, as in boy[NOM.SG]
    public string Gloss => string.Join("-", Morphemes.Select(m =>
        m.Value is Root && Covert is { Count: > 0 } covert ? $"{m.Gloss}[{Grammeme.JoinGlosses(covert)}]" : m.Gloss));
}

/// <summary>Which word a clitic leans on: the one after it, a proclitic, or the one before, an enclitic.</summary>
public enum Lean
{
    Forward,
    Back,
}

/// <summary>A dictionary entry: a root, and what it can be inflected as.</summary>
/*
  TODO stems: a word is inflected from its root, so there's no derivation yet. Stem can mean two
  things: a base built up from morphemes, as in Latin am-ā-t-us 'loved', or one of a lexeme's
  alternate stems, chosen by what's being formed, as with fer-, tul- and lāt-. Wait for the first
  derivation to show which shape is needed, most likely agent nouns (hunt → hunt-AGT), which need no
  voice or new paradigms. Participles need more: an adjective paradigm like the pronouns' borrowed
  slots, which would replace AgreeingAdjective's relabeling, inflection classes, since amātus and
  amāns decline differently, and syntax for words that agree like adjectives but take objects like
  verbs. Until then, avoid adding more placement relative to the root, as in Gender.Agree and
  Possession.Agree, since a marker there would land inside a derived stem
 */
public record Lexeme(Root Root, PartOfSpeech PartOfSpeech);

public enum PartOfSpeech
{
    Noun,
    Verb,
    Adjective,
    Pronoun,
    Adposition,
    Numeral,
    Conjunction,
    Adverb,
}

public enum AdpositionType
{
    Preposition,
    Postposition,
}
