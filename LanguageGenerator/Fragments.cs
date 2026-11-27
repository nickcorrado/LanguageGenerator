namespace LanguageGenerator;

/// <summary>What a line of text says: a statement, or a fragment, which has no clause.</summary>
public union Utterance(Statement, Fragment);

/// <summary>
/// What a line says with no clause, as game text is full of: a phrase on its own, as in 'an old
/// iron key', 'for your ears' or 'Item: Rusty Key', said to someone or not, or someone called with
/// nothing else, as in 'stranger!'. It has a part, an addressee or both.
/// </summary>
public record Fragment(FragmentPart? Part, NounPhrase? Addressee);

/// <summary>
/// What a fragment says: a phrase standing alone, an oblique, as in 'for your ears', a heading, as
/// in 'Item: Rusty Key', an interjection, as in 'hello', or an answer, 'yes' or 'no'.
/// </summary>
public union FragmentPart(Standalone, Oblique, Heading, Interjection, Answer);

/// <summary>A word said on its own, as 'hello', 'oh' or 'alas', which has no part in a clause.</summary>
public record Interjection(string Meaning);

/// <summary>
/// 'Yes' or 'no', answering a question, or saying that something is so or isn't, as in '"No," she
/// says'.
/// </summary>
/*
  each is a word of its own here, as English yes and no are. Sadock and Zwicky find two other ways
  of answering: agreeing or disagreeing with how the question was put, as Japanese hai agrees with
  a negative question, and repeating its verb, as Welsh and Finnish do, which would need the
  question answered
 */
public record Answer(Polarity Polarity)
{
    /// <summary>What it's glossed as, and coined for: 'yes' or 'no'.</summary>
    public string Meaning => Polarity == Polarity.Negative ? "no" : "yes";
}

/// <summary>
/// A noun phrase, a pronoun, a demonstrative or several of them joined, said on its own, as in 'an
/// old iron key', in the case a noun is cited in.
/// </summary>
public record Standalone(Argument Argument);

/// <summary>
/// A heading: a label saying what kind of thing follows, as 'item' or 'new quest' do, and its
/// title, as in 'Item: Rusty Key', each said as a noun is cited.
/// </summary>
public record Heading(Argument Label, Argument Title);
