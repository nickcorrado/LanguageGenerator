namespace LanguageGenerator;

/// <summary>What joins two things: 'and', 'or', or between clauses also 'but'.</summary>
public enum Conjunction
{
    And,
    Or,
    But,
}

/// <summary>
/// Noun phrases or pronouns joined by 'and' or 'or', as in 'an empty purse and a sword' or 'a rope
/// or a boat', in the order said.
/// </summary>
//two or more, each a whole argument, so one can be modified or coordinated in turn
public record Coordination(Conjunction Conjunction, ValueList<Argument> Items);

/// <summary>Adjectives said of a subject together, as in 'the inn is warm and crowded'.</summary>
public record AdjectiveCoordination(Conjunction Conjunction, ValueList<Adjective> Adjectives);

/// <summary>
/// Two things joined: statements, as in 'I'm leaving, but will you come?', or states of affairs
/// under one tense and sentence type, as in 'she turns and walks away'.
/// </summary>
//a third is joined to the first two, so a list nests to the left
public record Coordinated<T>(Conjunction Conjunction, T First, T Second);

/// <summary>'And' between nouns is a word of its own, as English and is.</summary>
public record AndWord(Lexeme Word);

/// <summary>
/// 'And' between nouns is the word for 'with', as in many languages that say 'the dog with the cat
/// slept': every noun phrase after the first takes the companion's marker.
/// </summary>
public record AndWith;

/// <summary>Nouns are joined by just setting them side by side.</summary>
public record AndJuxtaposed;

/// <summary>How a language joins nouns with 'and'.</summary>
public union NounAnd(AndWord, AndWith, AndJuxtaposed);

/// <summary>How a language joins clauses with 'and'.</summary>
//WALS 64A
public enum ClauseAnd
{
    //with the word that joins nouns
    SameAsNouns,
    //with a word of its own
    OwnWord,
    //by setting them side by side, as nouns are then too, unless 'and' between them is 'with'
    Juxtaposition,
}

/// <summary>A language's ways of saying 'and', 'or' and 'but'.</summary>
/*
  ClauseAnd is null where clauses are just set side by side. Each word goes between the two things
  it joins, the commonest place for one, though some languages put it after the second, as Latin
  -que does
 */
public record Conjunctions(NounAnd NounAnd, Lexeme? ClauseAnd, Lexeme Or, Lexeme But)
{
    /// <summary>The word that joins clauses, or adjectives said of one subject, with this conjunction.</summary>
    public Lexeme? BetweenClauses(Conjunction conjunction) => conjunction switch
    {
        Conjunction.And => ClauseAnd,
        Conjunction.Or => Or,
        Conjunction.But => But,
        _ => throw new ArgumentOutOfRangeException(nameof(conjunction), conjunction, null),
    };
}
