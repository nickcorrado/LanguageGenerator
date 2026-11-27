namespace LanguageGenerator;

/*
  the discourse level, above the sentence: what a line says, and who it's said to. The addressee is
  the utterance's, as Hengeveld and Mackenzie make the addressee a participant of the discourse act
  rather than part of what it says, so a sentence and a fragment are said to someone alike, and
  nothing inside a sentence can be
 */
/// <summary>What a line of text says: a message, said to someone or not, or a call to someone with nothing else.</summary>
public union Utterance(Message, Call);

/// <summary>
/// Something said, as in 'the wolves are coming' or 'an old iron key', and who it's said to, if it's
/// said to someone, as in 'Marta, the wolves are coming'.
/// </summary>
public record Message(Content Content, NounPhrase? Addressee);

/// <summary>Someone called with nothing else said, as in 'stranger!'.</summary>
//the addressee is a noun phrase, never a pronoun, and bare, since a vocative takes no article in
//nearly every language
public record Call(NounPhrase Addressee);

/// <summary>What a message says: a statement, or a fragment, which has no clause.</summary>
public union Content(Statement, Fragment);
