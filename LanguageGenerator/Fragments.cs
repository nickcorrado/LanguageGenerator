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
/// What a fragment says: a phrase standing alone, an oblique, as in 'for your ears', or a heading,
/// as in 'Item: Rusty Key'.
/// </summary>
public union FragmentPart(Standalone, Oblique, Heading);

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
