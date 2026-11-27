namespace LanguageGenerator;

/// <summary>
/// How a relative clause says the role its noun fills in it, as WALS 122A and 123A count them.
/// </summary>
public enum RelativeStrategy
{
    /// <summary>
    /// Not at all: the role is left empty, as Turkish kitab-ı al-an öğrenci 'book-ACC buy-REL
    /// student' is 'the student who bought the book'.
    /// </summary>
    Gap,
    /// <summary>
    /// With a relative pronoun, first in the clause and in its role's case, as German der Mann, den
    /// ich sah 'the man whom I saw'.
    /// </summary>
    Pronoun,
    /// <summary>
    /// With an ordinary pronoun, left in its place, as Persian resumes an oblique: 'the knife that I
    /// cut the bread with it'.
    /// </summary>
    Resumptive,
}

/// <summary>How a relative clause is marked, as a spec names it.</summary>
public enum RelativizerKind
{
    /// <summary>Not at all, as in English 'the net she was mending'.</summary>
    None,
    /// <summary>
    /// By a word at the clause's edge beside its noun, as English that and Mandarin de are.
    /// </summary>
    Particle,
    /// <summary>
    /// By an affix on its verb, which leaves the verb a participle, bare of tense and agreement, as
    /// Turkish -an and Korean -n do.
    /// </summary>
    Affix,
}

/// <summary>
/// How a language marks its relative clauses, glossed REL: an affix on the verb, on its side, or a
/// word at the clause's edge beside its noun, which is on whichever side the noun is.
/// </summary>
public union Relativizer(Bound, Particle);

/// <summary>
/// A language's relative pronoun: its question words for 'who', 'what' and 'where', as questions so
/// often become relative pronouns, or a word of its own, glossed REL, as German der came from a
/// demonstrative.
/// </summary>
public union RelativePronoun(RelativeFromQuestionWords, OwnRelativePronoun);

/// <summary>The question words as relative pronouns, as English who and where are.</summary>
public record RelativeFromQuestionWords;

/// <summary>A relative pronoun of its own, glossed REL.</summary>
public record OwnRelativePronoun(Lexeme Word);

/// <summary>
/// How a language makes relative clauses: before or after their noun, how each role the noun can
/// fill in one is said, what marks one, and its relative pronoun.
/// </summary>
//subjects, objects and obliques can each be relativized their own way, as the Accessibility
//Hierarchy has languages resume the roles lower on it before the higher ones
//the relativizer is null where the language doesn't mark them
public record Relativization(bool BeforeNoun, RelativeStrategy Subject, RelativeStrategy Object, RelativeStrategy Oblique, Relativizer? Relativizer, RelativePronoun Pronoun)
{
    /// <summary>The kind the relativizer is, as a spec names it.</summary>
    public RelativizerKind RelativizerKind => Relativizer is not { } relativizer ? RelativizerKind.None : relativizer switch
    {
        Bound => RelativizerKind.Affix,
        Particle => RelativizerKind.Particle,
    };

    /// <summary>How the noun is said when it fills this role in the relative clause.</summary>
    public RelativeStrategy StrategyFor(Role role) => role switch
    {
        Role.IntransitiveSubject or Role.TransitiveSubject => Subject,
        Role.DirectObject => Object,
        _ => Oblique,
    };
}

/// <summary>
/// The subject of the clause a dependent one depends on, which the dependent one shares, as 'to go
/// home' shares 'they' in 'they want to go home': control, or PRO, as grammars call it. Syntax says
/// it as a pronoun where the dependent clause keeps its verb, and leaves it out where it's deranked.
/// </summary>
public record Shared;

/// <summary>
/// The role a relative clause's noun fills in it, written _, as in 'the net [she was mending _]'.
/// Head is the noun, which the clause file leaves out and Syntax fills in, for what agrees with it.
/// </summary>
public record Gap(Nominal? Head);

/// <summary>
/// A relative pronoun standing for Head in a relative clause, which Syntax puts in a gap's place
/// where the language says the gap's role with one.
/// </summary>
public record RelativeWord(Lexeme Word, Nominal Head);
