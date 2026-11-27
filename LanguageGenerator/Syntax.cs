namespace LanguageGenerator;

/*
  what a sentence means, independent of any language: words are meanings, like "dog", which each
  language's lexicon turns into its own roots. Realizing one in a language is where all of that
  language's choices (word order, role markers, paradigms, phonology) come together
 */
/// <summary>
/// A noun or an indefinite pronoun, and a relative clause saying more of it, if it has one, as in
/// 'the net she was mending' or 'nobody who goes into the forest'.
/// </summary>
public record NounPhrase(Nominal Head, Statement? Relative);

/// <summary>What a noun phrase is of: a noun with what modifies it, or an indefinite pronoun.</summary>
public union Nominal(Noun, IndefinitePronoun)
{
    /// <summary>The noun it's said with and agrees as: its own, or its kind's, as 'somebody' is a person.</summary>
    public string Meaning => this switch
    {
        Noun noun => noun.Meaning,
        IndefinitePronoun pronoun => pronoun.AsNoun.Meaning,
    };
}

/// <summary>
/// A noun with any adjectives modifying it, in English order, how many of it there are if a numeral
/// says, as in 'three wolves', which one it is, as in 'the third', a quantifier, as in 'every dog',
/// an article or a demonstrative, as 'the dog' and 'this dog' have, and whose it is, as in 'the
/// king's house'.
/// </summary>
//no determiner is a bare noun, as English has for 'dogs', 'water' or 'my dog'
public record Noun(string Meaning, Number Number, ValueList<Adjective> Adjectives, int? Count, int? Ordinal, Quantifier? Quantifier, Determiner? Determiner,
    Argument? Possessor);

/// <summary>
/// An indefinite pronoun: a series, as 'some', 'no' or 'every', of a kind, as a person, a thing, a
/// place or a time, so that 'nobody' is the no series of persons, said as the language says its
/// indefinite pronouns.
/// </summary>
public record IndefinitePronoun(Quantifier Series, Ontology Kind)
{
    /// <summary>It as a noun of its kind with its series' quantifier, as 'some person' is 'somebody'.</summary>
    public Noun AsNoun => new(Indefinites.NounFor(Kind), Number.Singular, [], null, null, Series, null, null);
}

/// <summary>
/// What determines a noun: whether it's definite, as 'the dog' is and 'a dog' isn't, or a
/// demonstrative pointing it out, as in 'this dog', which makes it definite, so a noun has one or
/// the other.
/// </summary>
public union Determiner(Definiteness, Distance);
/// <summary>
/// What fills a role: a noun phrase, a pronoun, a demonstrative standing alone, several of them
/// joined by 'and' or 'or', a question word asking what fills it, 'here' or 'there' saying a
/// place, or, as an object, a clause or a quotation. In a relative clause, a gap is the role its
/// noun fills, and a relative word what Syntax says in its place.
/// </summary>
//what agrees with one depends on the language, so that's Syntax.AgreementOf's to say
public union Argument(NounPhrase, Pronoun, DemonstrativePronoun, Coordination, QuestionWord, DemonstrativePlace, ComplementClause, Quotation, Gap, RelativeWord);
/// <summary>
/// A phrase saying where, to where, from where, which way, to whom, with whom or with what something
/// happened, what it was like, when it happened or for how long. A spatial one says where relative
/// to its place too, so 'into the river' is a goal In the river.
/// </summary>
public record Oblique(Role Role, Argument Argument, Configuration Configuration)
{
    public static IReadOnlyList<Role> Roles { get; } =
        [Role.Recipient, Role.Location, Role.Goal, Role.Source, Role.Route, Role.Companion, Role.Instrument, Role.Similative, Role.Time, Role.Duration];

    /// <summary>The roles that say where, which can take a configuration.</summary>
    public static IReadOnlyList<Role> Spatial { get; } = [Role.Location, Role.Goal, Role.Source, Role.Route];
}

/// <summary>
/// A clause: what it says of its subject, whether it states that or tells someone to do it, whether
/// it's negated, and the obliques and adverbs saying more of it.
/// </summary>
//obliques and adverbs are each in the order they're written, which ModifierOrder keeps for those
//that go equally near the verb
public record Clause(Predicate Predicate, Force Force, bool Negative, ValueList<Oblique> Obliques, ValueList<Adverbial> Adverbs)
{
    /// <summary>
    /// What fills each of the clause's roles, in order: its subject, where it states something, then
    /// its object, a passive's agent or where be says its subject is, then its obliques.
    /// </summary>
    public IEnumerable<Filler> Fillers =>
    [
        .. Force.Value is Declarative stated
            ? [new Filler(stated.Subject, Predicate.Value is Doing { Object: not null } ? Role.TransitiveSubject : Role.IntransitiveSubject, null)]
            : Array.Empty<Filler>(),
        .. Predicate.Fillers,
        .. Obliques.Select(o => new Filler(o.Argument, o.Role, o)),
    ];

    /// <summary>Every argument the clause says, in order: its fillers', with a noun be says its subject is after the subject.</summary>
    public IEnumerable<Argument> Arguments =>
    [
        .. Force.Value is Declarative stated ? [stated.Subject] : Array.Empty<Argument>(),
        .. Predicate.Arguments,
        .. Obliques.Select(o => o.Argument),
    ];

    /// <summary>The clause with each of its roles filled with what this makes of what fills it now.</summary>
    public Clause Refilled(Func<Argument, Argument> fill) => this with
    {
        Force = Force switch
        {
            Declarative stated => stated with { Subject = fill(stated.Subject) },
            Directive told => told,
        },
        Predicate = Predicate.Refilled(fill),
        Obliques = [.. Obliques.Select(o => o with { Argument = fill(o.Argument) })],
    };
}

/// <summary>What fills one of a clause's roles, and the oblique it's in, where it's in one.</summary>
public record Filler(Argument Argument, Role Role, Oblique? Oblique);

/// <summary>What a clause does: state something of its subject, or tell someone to do something.</summary>
public union Force(Declarative, Directive)
{
    /// <summary>Who or what the clause is about: its subject, or 'you' for a command.</summary>
    public Argument Subject => this switch
    {
        Declarative stated => stated.Subject,
        Directive told => new Pronoun(Person.Second, told.Number),
    };
}

/// <summary>A clause saying something of its subject, at a time and with an aspect.</summary>
public record Declarative(Tense Tense, Aspect Aspect, Argument Subject);

/// <summary>
/// Telling one person or more to do something, or with the clause's Negative, not to. Who's told is
/// 'you', so it isn't said, and a command has no tense or aspect of its own.
/// </summary>
public record Directive(Number Number);

/// <summary>What a clause says of its subject: what it does, what's done to it, or what or where it is.</summary>
public union Predicate(Doing, Done, Being)
{
    /// <summary>What fills the roles it has besides the subject: an object, an agent, or where be says the subject is.</summary>
    public IEnumerable<Filler> Fillers => this switch
    {
        Doing { Object: { } o } => [new Filler(o, Role.DirectObject, null)],
        Doing => [],
        Done { Agent: { } agent } => [new Filler(agent, Role.Agent, null)],
        Done => [],
        Being { Complement.Value: Oblique location } => [new Filler(location.Argument, location.Role, location)],
        Being => [],
    };

    /// <summary>The arguments it says: its fillers', or a noun be says the subject is.</summary>
    public IEnumerable<Argument> Arguments => this switch
    {
        Being { Complement.Value: NounPhrase noun } => [noun],
        Doing or Done or Being => Fillers.Select(f => f.Argument),
    };

    /// <summary>It with each of its fillers what this makes of it.</summary>
    public Predicate Refilled(Func<Argument, Argument> fill) => this switch
    {
        Doing doing => doing with { Object = doing.Object is { } o ? fill(o) : (Argument?)null },
        Done done => done with { Agent = done.Agent is { } agent ? fill(agent) : (Argument?)null },
        Being { Complement.Value: Oblique location } being => being with { Complement = location with { Argument = fill(location.Argument) } },
        Being being => being,
    };
}

/// <summary>A verb, with its object if it has one.</summary>
public record Doing(string Verb, Argument? Object);

/// <summary>A property, like 'big', said of something, or with Intensified, much of it, as in 'very big'.</summary>
public record Adjective(string Meaning, bool Intensified);
/// <summary>
/// What a copular clause says of its subject: a noun phrase, as in 'you are a fool', an adjective,
/// as in 'the gate is locked', adjectives together, as in 'the inn is warm and crowded', or a
/// Location, as in 'the dog is in the house'.
/// </summary>
public union Complement(NounPhrase, Adjective, AdjectiveCoordination, Oblique);
/// <summary>
/// What or where a clause's subject is, as English says with be, or told, what or where to be, as
/// in 'be careful', 'don't be a fool' or 'be in the house'.
/// </summary>
//a clause with one has obliques saying only when and for how long
public record Being(Complement Complement);

/// <summary>
/// A statement said to someone called by name or by what they are, as in 'Marta, come here' or 'old
/// man, the wolves are in the village'.
/// </summary>
//the addressee is a noun phrase, never a pronoun, and bare, since a vocative takes no article in
//nearly every language
public record Addressed(Statement Statement, NounPhrase Addressee);

/// <summary>Anything a sentence can say.</summary>
public union Statement(Clause, Comparative, Addressed, Coordinated, Subordinated, Modalized, Question);

public static class Syntax
{
    public static Sentence Realize(Language language, Statement statement, string translation) => Sentence(language, Words(language, statement).Words, translation);

    /// <summary>
    /// A clause's words, and the root of its finite verb where one is said, for what goes on that
    /// verb: a question's affix, the verb a question moves, or a suffix marking the whole clause.
    /// </summary>
    private record ClauseWords(IReadOnlyList<Word> Words, Root? Finite);

    //a statement's words, before they're sounded out, and its finite verb, so that a statement
    //inside another can be marked as one
    private static ClauseWords Words(Language language, Statement statement) => statement switch
    {
        Clause c => c.Force switch
        {
            Declarative => Words(language, Fold(language, c, null), []),
            Directive => Words(language, c, new Balanced(null, null, [])),
        },
        Comparative c => Words(language, c, []),
        Addressed a => Words(language, a),
        Coordinated c => Words(language, c),
        Subordinated s => Words(language, s),
        Modalized m => Words(language, m, []),
        Question q => Words(language, q),
    };

    /// <summary>Who or what a statement is about: its subject, or 'you' for a command.</summary>
    public static Argument SubjectOf(Statement statement) => statement switch
    {
        Clause c => c.Force.Subject,
        Comparative c => c.Clause.Force.Subject,
        Addressed a => SubjectOf(a.Statement),
        Coordinated c => SubjectOf(c.First),
        Subordinated s => SubjectOf(s.Main),
        Modalized m => SubjectOf(m.Clause),
        Question q => SubjectOf(q.Statement),
    };

    public static Sentence Realize(Language language, Subordinated subordinated, string translation) => Sentence(language, Words(language, subordinated).Words, translation);

    /*
      the subordinate clause, marked by its word or suffix, before or after the main one as the language
      puts that kind. Its verb is deranked, bare of tense and agreement, where the language derank
      this kind, or does either and the two clauses share a subject, which a deranked clause then
      leaves unsaid; only a verb's clause can be deranked here, and a copular one is always balanced.
      A balanced clause whose subject was taken from the main clause says it as a pronoun. A deranked
      clause the language marks like a role takes that role's case on its verb, or its adposition, in
      place of the subordinator, as a noun in that role would
     */
    private static ClauseWords Words(Language language, Subordinated subordinated)
    {
        var marking = language.Subordination.Markers[subordinated.Subordinator];
        var shared = subordinated.SharesSubject || SubjectOf(subordinated.Clause).Equals(SubjectOf(subordinated.Main));
        var derank = marking.Balance == Balance.Deranked || marking.Balance == Balance.Either && shared;
        var deranked = derank ? AsDeranked(language, subordinated.Clause, shared, marking.MarkedAs) : null;
        var clause = deranked ?? Words(language, subordinated.SharesSubject ? WithSubject(subordinated.Clause, Pronominal(language, SubjectOf(subordinated.Main))) : subordinated.Clause);
        var marked = deranked is not null && marking.MarkedAs is not null
            ? deranked.Words
            : Subordinate(clause, marking.Marker, marking.Position);
        var main = Words(language, subordinated.Main).Words;
        //TODO its finite verb is its main clause's, but a suffix marking it still goes on its last word
        return new(marking.BeforeMain ? [.. marked, .. main] : [.. main, .. marked], null);
    }

    //a clause with its marker: a word at its start or end, or a suffix outside everything on its verb,
    //or on its last word where it has no verb, as a copular clause that drops its copula doesn't
    private static IReadOnlyList<Word> Subordinate(ClauseWords clause, Morpheme marker, ExponentPosition position)
    {
        switch (position)
        {
            case ExponentPosition.PrecedingParticle:
                return [Word.Marker(marker, Lean.Forward), .. clause.Words];
            case ExponentPosition.FollowingParticle:
                return [.. clause.Words, Word.Marker(marker, Lean.Back)];
        }
        List<Word> words = [.. clause.Words];
        var at = words.FindLastIndex(w => w.Root is { } r && r == clause.Finite);
        at = at < 0 ? words.Count - 1 : at;
        words[at] = words[at] with { Morphemes = position == ExponentPosition.Prefix ? [marker, .. words[at].Morphemes] : [.. words[at].Morphemes, marker] };
        return words;
    }

    //a noun phrase with its relative clause, if it has one, before or after it all
    private static IReadOnlyList<Word> Relativized(Language language, NounPhrase phrase, IReadOnlyList<Word> words)
    {
        if (phrase.Relative is not { } clause)
        {
            return words;
        }
        var relative = RelativeClause(language, clause, phrase.Head);
        return language.Relativization.BeforeNoun ? [.. relative, .. words] : [.. words, .. relative];
    }

    /*
      a relative clause, with its noun said in the gap as the language says the gap's role: not at
      all, as an ordinary pronoun, or as a relative pronoun, which goes first in the clause with the
      rest of its phrase, as German den in der Mann, den ich sah. A relativizer that's an affix goes
      on the clause's verb, which it leaves bare of tense and agreement, as a participle is, and one
      that's a word goes at the clause's edge beside its noun
     */
    private static IReadOnlyList<Word> RelativeClause(Language language, Statement clause, Nominal head)
    {
        var relativization = language.Relativization;
        var (role, oblique) = GapSlot(language, clause);
        Argument filler = relativization.StrategyFor(role) switch
        {
            RelativeStrategy.Gap => new Gap(head),
            RelativeStrategy.Resumptive => Pronominal(language, head),
            RelativeStrategy.Pronoun => new RelativeWord(RelativePronounFor(language, head, role), head),
            var strategy => throw new ArgumentOutOfRangeException(nameof(clause), strategy, null),
        };
        var filled = Filled(clause, filler);
        var relativizer = relativization.Relativizer?.Value;
        var words = relativizer is Bound affix
            ? Subordinate(AsDeranked(language, filled, dropSubject: false, markedAs: null) ?? Words(language, filled), affix.Affix, ((Exponent)affix).Position)
            : Words(language, filled).Words;
        if (filler.Value is RelativeWord word)
        {
            var phrase = oblique is { } o ? Oblique(language, o with { Argument = filler })
                : role == Role.Agent ? Marked(language, filler, role)
                : Inflect(language, filler, role);
            words = TakePhrase(words, word.Word.Root, phrase) is var (taken, rest) ? [.. taken, .. rest] : words;
        }
        return relativizer is not Particle particle ? words
            : relativization.BeforeNoun ? [.. words, Word.Marker(particle, Lean.Back)]
            : [Word.Marker(particle, Lean.Forward), .. words];
    }

    //the role a relative clause's gap is in, as the language says the clause, and the oblique it's
    //in, if it's one. A passive in a language without one is said as the active
    private static (Role Role, Oblique? Oblique) GapSlot(Language language, Statement clause) =>
        Roles(language, clause).FirstOrDefault(f => f.Argument.Value is Gap) is { } gap
            ? (gap.Role, gap.Oblique)
            : throw new ArgumentException("A relative clause needs a gap in one of its roles.", nameof(clause));

    //a relative clause with this in its gap's place
    private static Statement Filled(Statement clause, Argument filler)
    {
        Argument Fill(Argument argument) => argument.Value is Gap ? filler : argument;
        return clause.Value switch
        {
            Clause c => c.Refilled(Fill),
            Comparative c => c with { Clause = c.Clause.Refilled(Fill), Standard = c.Standard.HasValue ? Fill(c.Standard.GetValueOrDefault()) : (Argument?)null },
            Modalized m => m with { Clause = Filled(m.Clause, filler) },
            _ => clause,
        };
    }

    //a relative pronoun: the question word for 'where' in a role saying where, and otherwise for
    //'who' or 'what', as its noun names a person or not, or the language's word of its own
    private static Lexeme RelativePronounFor(Language language, Nominal head, Role role)
    {
        if (language.Relativization.Pronoun.Value is OwnRelativePronoun own)
        {
            return own.Word;
        }
        var words = language.Questions.Words;
        return role is Role.Location or Role.Goal or Role.Source ? words[Interrogative.Where]
            : head.Value is IndefinitePronoun { Kind: Ontology.Person } || language.Lexicon.ClassOf(head.Meaning) is SemanticClass.Male or SemanticClass.Female or SemanticClass.Human
                ? words[Interrogative.Who]
            : words[Interrogative.What];
    }

    //a phrase taken out of a clause's words, found by its root, and the words left, or null where
    //the clause doesn't say it, for a question word or relative pronoun to go first
    private static (IReadOnlyList<Word> Taken, List<Word> Left)? TakePhrase(IReadOnlyList<Word> words, Root root, IReadOnlyList<Word> phrase)
    {
        var rest = words.ToList();
        var found = rest.FindIndex(w => w.Root == root);
        if (found < 0)
        {
            return null;
        }
        var at = found - phrase.ToList().FindIndex(w => w.Root == root);
        var taken = rest.GetRange(at, phrase.Count);
        rest.RemoveRange(at, phrase.Count);
        return (taken, rest);
    }

    //an object as its clause says it: in the object's place, or last in the clause, where it's a
    //quotation or a complement that keeps its verb and the language puts those last
    private static (IReadOnlyList<Word>? InPlace, IReadOnlyList<Word> Last) Object(Language language, Argument? @object, Argument subject)
    {
        if (!@object.HasValue)
        {
            return (null, []);
        }
        var argument = @object.GetValueOrDefault();
        var last = language.Complementation.Last;
        switch (argument.Value)
        {
            case ComplementClause complement:
            {
                var (words, balanced) = Complement(language, complement, subject);
                return balanced && last ? (null, words) : (words, []);
            }
            case Quotation quotation:
            {
                var words = Quoted(language, quotation);
                return last ? (null, words) : (words, []);
            }
            default:
                return (Inflect(language, argument, Role.DirectObject), []);
        }
    }

    /*
      a complement, and whether it kept its verb. It's marked as the language marks its kind: its verb
      deranked where the language deranks that kind, or does either and the two clauses share a
      subject, which a deranked complement then leaves unsaid, as subordinate clauses are. A deranked
      complement the language marks like a role takes that role's case or adposition in place of the
      complementizer, as Turkish puts a nominalized clause in the accusative. A balanced one whose
      subject was taken from the main clause says it as a pronoun, as Greek thélo na páo 'I want
      that I go' says it in the verb
     */
    //subject is the main clause's, or null where it has none the complement could share, as an impersonal verb's 'it' isn't
    private static (IReadOnlyList<Word> Words, bool Balanced) Complement(Language language, ComplementClause complement, Argument? subject)
    {
        var marking = language.Complementation.MarkingOf(complement.Kind);
        var shared = complement.SharesSubject || subject is { } main && SubjectOf(complement.Clause).Equals(main);
        var derank = marking.Balance == Balance.Deranked || marking.Balance == Balance.Either && shared;
        var deranked = derank ? AsDeranked(language, complement.Clause, shared, marking.MarkedAs) : null;
        if (deranked is not null && marking.MarkedAs is not null)
        {
            return (deranked.Words, false);
        }
        var clause = deranked ?? Words(language, complement.SharesSubject
            ? WithSubject(complement.Clause, Pronominal(language, subject ?? throw new ArgumentException("A complement that shares its subject needs the main clause's.", nameof(subject))))
            : complement.Clause);
        var marked = marking.Marker is { } complementizer ? Subordinate(clause, complementizer.Marker, complementizer.Position) : clause.Words;
        return (marked, deranked is null);
    }

    //what was said, as it was said, with the language's quotative, if it has one
    private static IReadOnlyList<Word> Quoted(Language language, Quotation quotation)
    {
        var said = Words(language, quotation.Statement);
        return language.Complementation.QuotativeMarker is var (marker, position) ? Subordinate(said, marker, position) : said.Words;
    }

    /*
      what's wanted, with 'want' as a marker on its verb, where the language has no verb for 'want': it
      takes the wanting clause's tense, aspect and subject, its modifiers too, and its negation as
      the marker's, as Japanese tabe-taku-nai 'eat-DESID-NEG' is 'doesn't want to eat'. Null where
      'want' is a verb, or what's wanted isn't an event its subject would do, or can't take a marker
      on its verb, as a comparison or a clause with a modal can't
     */
    private static (Clause Wanted, Modal Want)? Desired(Language language, Clause clause) =>
        Embedded(clause) is ("want", var wanted) && language.Complementation.DesiderativeMarking is { } marking
            ? (wanted, new Modal(marking, clause.Negative))
            : null;

    /*
      what's begun or stopped, with the language's inceptive or cessative on its verb in place of
      'begin' or 'stop', as Russian za-pel 'started singing' has no verb for 'start': it takes the
      phase verb's tense, aspect, subject and negation, and its modifiers too, so 'didn't start
      singing' is sing.NEG.INCEP. Null where the language has a verb for it
      instead, what's begun isn't an event its subject does, or is negated itself, as 'stopped not
      caring' is, or is said with be, or begins, stops or wants something itself, which keeps its
      verbs
     */
    //only an active verb begins or wants something itself, so a passive passes both
    private static (Clause Begun, AspectMarking Phase)? Phased(Language language, Clause clause) =>
        Embedded(clause) is (var verb, { Predicate.Value: Doing or Done, Negative: false } begun)
            && Aspects.PhaseOf(verb) is { } phase && language.Aspects.MarkingOf(phase) is { } marking
            && Phased(language, begun) is null && Desired(language, begun) is null
            ? (begun with { Negative = clause.Negative }, marking)
            : null;

    /*
      what a clause's verb takes as an event its own subject does, as 'want' and 'begin' take one,
      with that verb: the event as a clause that states something, timed as this one is, in its tense
      and aspect and with its subject, and with this one's modifiers after its own. Null for a clause
      that takes no such event
     */
    private static (string Verb, Clause Event)? Embedded(Clause clause)
    {
        if (clause is not { Predicate.Value: Doing { Object: { Value: ComplementClause { Kind: ComplementKind.Event } embedded } } doing, Force.Value: Declarative stated }
            || !(embedded.SharesSubject || SubjectOf(embedded.Clause).Equals(stated.Subject))
            || embedded.Clause.Value is not Clause { Force.Value: Declarative } inner)
        {
            return null;
        }
        return (doing.Verb, inner with { Force = stated, Obliques = [.. inner.Obliques, .. clause.Obliques], Adverbs = [.. inner.Adverbs, .. clause.Adverbs] });
    }

    //whether a clause's copula can be dropped: not with a modal or an aspect's marker, which go with
    //it, and only in the tense its verb is said in, as a perfect the language lacks is a past
    private static bool DropsCopula(Language language, Declarative stated, bool negative, Modal? modal, bool locational)
    {
        var (tense, _, marking) = language.Aspects.Said(stated.Tense, stated.Aspect);
        return modal is null && marking is null && language.Predication.Drops(tense, negative, locational);
    }

    /*
      a verb's clause with its verb bare of tense and agreement, keeping negation and the passive,
      and without its subject where it shares the main clause's. Marked like a role, its verb takes
      that role's case, or the whole clause its adposition. Null for any other kind of clause,
      including one with a modal, which stays balanced. One that begins or stops something, where
      the language marks that on the verb, is what's begun with its inceptive or cessative
     */
    private static ClauseWords? AsDeranked(Language language, Statement statement, bool dropSubject, Role? markedAs)
    {
        if (statement.Value is not Clause { Force.Value: Declarative } clause)
        {
            return null;
        }
        var folded = FoldPhase(language, clause, null);
        var voiced = Voiced(language, folded.Clause);
        if (voiced is not { Force.Value: Declarative, Predicate.Value: Doing or Done })
        {
            return null;
        }
        //a passive said as the active has the agent as its subject, which isn't the main clause's
        var dropped = dropSubject && voiced == folded.Clause;
        var marker = markedAs is { } role ? language.RoleMarkers[role] : null;
        return Words(language, voiced, new Deranked(folded.Phase, dropped, marker));
    }

    //a passive in a language without one is said as the active
    private static Clause Voiced(Language language, Clause clause) =>
        clause is { Predicate.Value: Done done, Force.Value: Declarative stated } && language.Passive is null ? Active(clause, done, stated) : clause;

    //a verb inflected as a singular noun in a case, as a nominalized verb is, with its zero markers
    //unglossed, as an agreeing adjective's are
    private static IReadOnlyList<Word> AsNoun(Language language, Lexeme verb, Case @case)
    {
        var nouns = language.Paradigms.Nouns;
        var words = nouns.Inflect(verb with { PartOfSpeech = PartOfSpeech.Noun }, Features(nouns, [Number.Singular, @case])).ToList();
        var main = words.FindIndex(w => w.Root is not null);
        words[main] = words[main] with { Covert = null };
        return words;
    }

    //the pronoun that refers back to something already said
    private static Pronoun Pronominal(Language language, Argument argument) => argument.Value is Pronoun pronoun
        ? pronoun
        : AgreementOf(language, argument) is var (person, number) ? new Pronoun(person, number, Gender: GenderOf(language, argument)) : throw new InvalidOperationException();

    //the pronoun that refers back to a noun or an indefinite pronoun, as a resumptive one does to its relative clause's
    private static Pronoun Pronominal(Language language, Nominal nominal) => AgreementOf(language, nominal) is var (person, number)
        ? new Pronoun(person, number, Gender: GenderOf(language, nominal))
        : throw new InvalidOperationException();

    private static Statement WithSubject(Statement statement, Argument subject) => statement.Value switch
    {
        Clause c => WithSubject(c, subject),
        Comparative c => c with { Clause = WithSubject(c.Clause, subject) },
        Modalized m => m with { Clause = WithSubject(m.Clause, subject) },
        _ => statement,
    };

    //a command's subject is always 'you'
    private static Clause WithSubject(Clause clause, Argument subject) => clause.Force.Value is Declarative stated
        ? clause with { Force = stated with { Subject = subject } }
        : clause;

    //each statement as it would be said alone, with the language's word for the conjunction between,
    //or nothing where it sets clauses side by side
    public static Sentence Realize(Language language, Coordinated coordinated, string translation) => Sentence(language, Words(language, coordinated).Words, translation);

    private static ClauseWords Words(Language language, Coordinated coordinated)
    {
        IReadOnlyList<Word> between = language.Conjunctions.BetweenClauses(coordinated.Conjunction) is { } word ? [new Word([word.Root])] : [];
        //TODO each clause has a finite verb of its own, but a suffix marking the two together still goes on the last word
        return new([.. Words(language, coordinated.First).Words, .. between, .. Words(language, coordinated.Second).Words], null);
    }

    //parts said one after another, with a word between each two, if there is one
    private static IReadOnlyList<Word> Join(IEnumerable<IReadOnlyList<Word>> parts, Lexeme? between) =>
        [.. parts.SelectMany((part, i) => i == 0 || between is null ? part : [new Word([between.Root]), .. part])];

    //a predicate's adjectives and how they're joined: one alone, or several said of the subject
    //together, joined as clauses are, since a predicate is what a clause says
    private static (IReadOnlyList<Adjective> Adjectives, Lexeme? Between)? PredicateAdjectives(Language language, Complement complement) => complement.Value switch
    {
        Adjective adjective => ([adjective], null),
        AdjectiveCoordination together => ([.. together.Adjectives], language.Conjunctions.BetweenClauses(together.Conjunction)),
        _ => null,
    };

    /*
      the addressee comes first, in the addressee's case: the vocative, where the language has one,
      and otherwise the case a noun is cited in. Where a vocative goes is a matter of discourse more
      than grammar, which WALS doesn't count, and first is the commonest place
     */
    public static Sentence Realize(Language language, Addressed addressed, string translation) => Sentence(language, Words(language, addressed).Words, translation);

    private static ClauseWords Words(Language language, Addressed addressed)
    {
        //TODO its finite verb is the statement's, but a suffix marking it still goes on its last word
        return new([.. Marked(language, addressed.Addressee, Role.Addressee), .. Words(language, addressed.Statement).Words], null);
    }

    /*
      the verb takes the language's imperative, or its ordinary second person where it has none for
      this number or doesn't use it to forbid, and only its prohibitive negator says 'don't', if it
      has one
     */
    private static VerbComplex Commanded(Language language, Lexeme verb, Number number, bool negative)
    {
        var commands = language.Commands;
        var imperative = negative && !commands.ProhibitiveUsesImperative ? null : commands.MarkerFor(number);
        Finiteness finiteness = imperative.HasValue
            ? new Imperative(imperative.GetValueOrDefault())
            : new Finite(Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, number));
        return new VerbComplex(verb, [.. NegationLayers(commands.Prohibitive ?? language.Negation, negative)], finiteness);
    }

    /*
      a clause as its verb says it: the clause, with its modal, if it has one, and its marker of
      beginning or ceasing, if a begin or stop clause was folded into it. 'Want' becomes the modal
      where the language marks it on the verb, unless the clause has a modal already, and then what
      it wants is what's folded, so 'wants to start singing' can be sing.INCEP.DESID
     */
    private record Folded(Clause Clause, Modal? Modal, AspectMarking? Phase);

    private static Folded Fold(Language language, Clause clause, Modal? modal) =>
        modal is null && Desired(language, clause) is var (wanted, want) ? FoldPhase(language, wanted, want) : FoldPhase(language, clause, modal);

    private static Folded FoldPhase(Language language, Clause clause, Modal? modal) =>
        Phased(language, clause) is var (begun, phase) ? new(begun, modal, phase) : new(clause, modal, null);

    //a clause that states something, as it's folded, with words said where its obliques go, before them
    private static ClauseWords Words(Language language, Folded folded, IReadOnlyList<Word> adverbs)
    {
        var clause = Voiced(language, folded.Clause);
        return clause.Force.Value is Declarative
            ? Words(language, clause, new Balanced(folded.Modal, folded.Phase, adverbs))
            : throw new ArgumentException("A command is said with no modal or phase.", nameof(folded));
    }

    /*
      a clause's rank: balanced, its verb as it would be in a clause of its own, which for a
      statement is finite, with its tense and aspect, any modal and marker of beginning or ceasing
      folded into it, and words said where its obliques go, before them, and for a command is its
      imperative; or deranked, its verb bare of tense and agreement, or a noun in the case of the
      role it's marked as, with the marker of beginning or ceasing folded into it, without its
      subject where it shares the main clause's, and the whole clause in the role's adposition
     */
    private union Rank(Balanced, Deranked);
    private record Balanced(Modal? Modal, AspectMarking? Phase, IReadOnlyList<Word> Adverbs);
    private record Deranked(AspectMarking? Phase, bool DropSubject, RoleMarker? Marker);

    /*
      a clause said at its rank, with its predicate's own words. Only a statement is deranked, or
      takes a modal or a phase, and a passive one is said only in a language with a passive
     */
    private static ClauseWords Words(Language language, Clause clause, Rank rank)
    {
        if (clause.Force.Value is Directive && rank.Value is not Balanced { Modal: null, Phase: null })
        {
            throw new ArgumentException("A command is said balanced, with no modal or phase.", nameof(rank));
        }
        return clause.Predicate switch
        {
            Doing doing => Verbal(language, clause, language.Lexicon.Verb(doing.Verb), doing.Object, null, null, rank),
            Done when clause.Force.Value is Directive => throw new ArgumentException("A command can't be passive yet.", nameof(clause)),
            Done done => Verbal(language, clause, language.Lexicon.Verb(done.Verb), null, done.Agent,
                language.Passive ?? throw new ArgumentException("A language without a passive says it as the active.", nameof(clause)), rank),
            Being being => Copular(language, clause, being, rank, [], null),
        };
    }

    //a clause's subject's words, unsaid in a command, even in a language that says its pronouns, as in English, and in a deranked clause that shares its subject
    private static IReadOnlyList<Word> SubjectWords(Language language, Clause clause, Rank rank, Role role) =>
        clause.Force.Value is Directive || rank.Value is Deranked { DropSubject: true } ? [] : Subject(language, clause.Force.Subject, role);

    //the words said where a statement's obliques go, before them, as a question's 'why' or 'how' is
    private static IReadOnlyList<Word> AdverbsOf(Rank rank) => rank.Value is Balanced balanced ? balanced.Adverbs : [];

    /*
      a verb as a clause says it at its rank, with its passive, negation and modal, and its marker of
      beginning or ceasing. A deranked verb has no auxiliary to take its place, so it's the clause's
      verb
     */
    private static VerbComplex Complex(Language language, Lexeme verb, Force force, Rank rank, bool negative, Modal? modal, Passive? passive) => rank switch
    {
        Balanced balanced => force switch
        {
            Declarative stated => Complex(language, verb, stated.Tense, stated.Aspect, stated.Subject, negative, modal, passive, balanced.Phase),
            Directive told => Commanded(language, verb, told.Number, negative),
        },
        Deranked deranked => new VerbComplex(verb, [.. PassiveLayers(passive), .. PhaseLayers(deranked.Phase), .. NegationLayers(language.Negation, negative)],
            deranked.Marker is { Case: { } @case, Adposition: null } ? new Nominalized(@case) : new Bare()),
    };

    /*
      a clause with a verb: its subject, its verb and its object, if it has one, in the language's
      order. In a passive one, its patient is the subject and takes the case of an intransitive one,
      which in an ergative language is the one it had already, and the verb agrees with it; the
      agent, if there is one, goes before the other obliques, marked for its role. A language
      without a passive says the active instead, as real ones do, with the agent as its subject, or
      'they' where nobody says who
     */
    private static ClauseWords Verbal(Language language, Clause clause, Lexeme verb, Argument? @object, Argument? agent, Passive? passive, Rank rank)
    {
        var subject = clause.Force.Subject;
        var (negative, modal) = Concord(language, clause.Negative, (rank.Value as Balanced)?.Modal, subject, @object, [.. Given(agent), .. ObliqueArguments(clause.Obliques)]);
        var complex = Complex(language, verb, clause.Force, rank, negative, modal, passive);
        var (inPlace, last) = Object(language, @object, subject);
        IReadOnlyList<Word> agentWords = agent.HasValue ? Marked(language, agent.GetValueOrDefault(), Role.Agent) : [];
        IReadOnlyList<Word> words = [.. Flat(Order(language.WordOrder, SubjectWords(language, clause, rank, @object is null ? Role.IntransitiveSubject : Role.TransitiveSubject),
            Linearized(language, complex), inPlace,
            WhereObliquesGo(language, [(Closeness.Reason, AdverbsOf(rank)), (Closeness.Complement, agentWords), .. Adverbials(language, clause.Adverbs, clause.Obliques)]))), .. last];
        return new(rank.Value is Deranked { Marker: { } marker } ? WithAdposition(words, marker.Adposition) : words, complex.Outermost.Root);
    }

    //a passive said as the active, with its agent as the subject, or 'they' where nobody says who
    private static Clause Active(Clause clause, Done done, Declarative stated) => clause with
    {
        Predicate = new Doing(done.Verb, stated.Subject),
        Force = stated with { Subject = done.Agent.HasValue ? done.Agent.GetValueOrDefault() : new Pronoun(Person.Third, Number.Plural) },
    };

    //a clause's adverbs and obliques, each with how near the verb it goes
    //the oblique is said before it's ranked, so that a role that's no oblique's says so
    private static IReadOnlyList<(Closeness Rank, IReadOnlyList<Word> Words)> Adverbials(Language language, ValueList<Adverbial> adverbs, ValueList<Oblique> obliques) =>
        [.. adverbs.Select(a => (ModifierOrder.Of(a), Adverbial(language, a))), .. obliques.Select(o =>
        {
            var words = Oblique(language, o);
            return (ModifierOrder.Of(o.Role), words);
        })];

    //what goes where obliques go, nearest the verb first after it, and mirrored before it, so that the
    //nearest is beside the verb, or the object where the object comes between
    private static IReadOnlyList<Word> WhereObliquesGo(Language language, IEnumerable<(Closeness Rank, IReadOnlyList<Word> Words)> modifiers) =>
        ModifierOrder.Arranged(modifiers, beforeHead: language.WordOrder.ObliquePosition != ObliquePosition.AfterVerbAndObject);

    /*
      an adverb is its own root, uninflected. One saying how is made from its adjective as the
      language makes them, with 'very' beside the adjective if it has it, outside the adverb's marker,
      as in 'very slowly'
     */
    private static IReadOnlyList<Word> Adverbial(Language language, Adverbial adverbial)
    {
        switch (adverbial)
        {
            case Adverb adverb:
                return [new Word([language.Lexicon.Adverb(adverb.Meaning).Root])];
            case Manner manner:
            {
                var root = language.Lexicon.Adjective(manner.Adjective.Meaning).Root;
                IReadOnlyList<Word> words = language.Manner switch
                {
                    MannerBare => [new Word([root])],
                    MannerAffix { Position: ExponentPosition.Prefix } affix => [new Word([affix.Marker, root])],
                    MannerAffix affix => [new Word([root, affix.Marker])],
                    MannerWord word => word.BeforeAdjective ? [Word.Marker(word.Marker, Lean.Forward), new Word([root])] : [new Word([root]), Word.Marker(word.Marker, Lean.Back)],
                };
                return Intensified(language, words, root, manner.Adjective);
            }
            default:
                throw new InvalidOperationException();
        }
    }

    /*
      an oblique takes its role's case, and its adposition, if the role has one, before or after it,
      and its configuration's marker, if it has one the language tells apart from At. A gap leaves
      nothing of its role behind, adposition and all, since stranding one, as English 'the house I
      live in' does, is rare outside Germanic
     */
    private static IReadOnlyList<Word> Oblique(Language language, Oblique oblique)
    {
        if (oblique.Argument.Value is Gap)
        {
            return [];
        }
        if (!LanguageGenerator.Oblique.Roles.Contains(oblique.Role))
        {
            throw new ArgumentException($"{oblique.Role} isn't an oblique role.", nameof(oblique));
        }
        if (oblique.Configuration != Configuration.At && !LanguageGenerator.Oblique.Spatial.Contains(oblique.Role))
        {
            throw new ArgumentException($"Only a role saying where takes a configuration, not {oblique.Role}.", nameof(oblique));
        }
        return language.Configurations.Markers.TryGetValue(oblique.Configuration, out var marker)
            ? Placed(language, oblique.Argument, oblique.Role, marker)
            : Marked(language, oblique.Argument, oblique.Role);
    }

    /*
      a place with its configuration and its role's marker together.
      A relational noun is possessed by the place and takes the role's marker itself, as Turkish
      masa-nın alt-ın-da is 'table-GEN bottom-3SG-LOC' for 'under the table'.
      An adposition goes beside the place with the role's case, if the role has one, as Latin sub
      mensā 'under the table' has the ablative, and the case it governs otherwise. A role marked by an
      adposition keeps it outside, as English says 'from under the table', except a location, whose
      plain 'at' the configuration's word replaces.
      A localization goes in the noun beside the role's case, and the role's adposition, if it has one,
      outside it
     */
    private static IReadOnlyList<Word> Placed(Language language, Argument place, Role role, ConfigurationMarker marker)
    {
        var path = language.RoleMarkers[role];
        switch (marker.Value)
        {
            case RelationalNoun noun:
                return Marked(language, new NounPhrase(new Noun(noun.Meaning, Number.Singular, [], null, null, null, null, place), null), role);
            case SpatialAdposition adposition:
            {
                var @case = (path.Adposition is null ? path.Case : adposition.Governs) ?? Citation(language);
                var phrase = WithAdposition(Inflect(language, place, @case, Localization.None), adposition.Adposition);
                return role == Role.Location ? phrase : WithAdposition(phrase, path.Adposition);
            }
            case SpatialCase spatial:
                return WithAdposition(Inflect(language, place, CaseFor(language, path), spatial.Localization), path.Adposition);
            default:
                throw new InvalidOperationException();
        }
    }

    //a phrase marked for a role: its case, and its adposition, if the role has one, before or after it
    private static IReadOnlyList<Word> Marked(Language language, Argument argument, Role role)
    {
        if (argument.Value is Gap)
        {
            return [];
        }
        if (role == Role.Similative)
        {
            return Likened(language, argument);
        }
        return WithAdposition(Inflect(language, argument, role), language.RoleMarkers[role].Adposition);
    }

    private static IReadOnlyList<Word> WithAdposition(IReadOnlyList<Word> phrase, Adposition? adposition) => adposition switch
    {
        null => phrase,
        { Type: AdpositionType.Preposition } a => [new Word([a.Lexeme.Root]), .. phrase],
        { } a => [.. phrase, new Word([a.Lexeme.Root])],
    };

    //what something is likened to, in the plain case a subject takes, with the language's word for
    //'like' on its side of the phrase, or its affix on the phrase's edge, outside everything else
    private static IReadOnlyList<Word> Likened(Language language, Argument argument)
    {
        var phrase = Inflect(language, argument, Role.IntransitiveSubject);
        switch (language.Similative.Value)
        {
            case SimilativeWord word:
                return word.BeforeStandard ? [new Word([word.Like.Root]), .. phrase] : [.. phrase, new Word([word.Like.Root])];
            case SimilativeAffix { Position: ExponentPosition.Prefix } affix:
                return [phrase[0] with { Morphemes = [affix.Marker, .. phrase[0].Morphemes] }, .. phrase.Skip(1)];
            case SimilativeAffix affix:
                return [.. phrase.SkipLast(1), phrase[^1] with { Morphemes = [.. phrase[^1].Morphemes, affix.Marker] }];
            default:
                throw new InvalidOperationException();
        }
    }


    /*
      a copular clause's complement goes where an object would, so it precedes the copula in an OV
      language, as in Turkish and Japanese, and follows it in a VO one. Where adjectives are verbal,
      the adjective takes the verb's place instead, with no copula. A standard of comparison, if the
      clause has one, goes as Standing says, and a comparative's marker, if it has one, goes on the
      adjective, whether it's verbal or not. A copula with a modal is never dropped, since the modal
      goes with it, as Russian has no copula in ty ostorožen 'you are careful' but has one in ty
      dolžen byt' ostorožen 'you must be careful', and neither is one with a perfect's or a
      progressive's marker
      A command keeps its copula even in a language that drops it in the present, since a command
      isn't a statement about the present: Russian has no copula in 'you are careful' but has one
      in bud' ostorozhen 'be careful'. Its verbal adjective takes the imperative itself, and its
      predicate noun or adjective is what it would be said of 'you'
     */
    private static ClauseWords Copular(Language language, Clause clause, Being being, Rank rank, IReadOnlyList<Word> standard, DegreeMarker? degree)
    {
        if (rank.Value is not Balanced { Phase: null } balanced)
        {
            throw new ArgumentException("Only a clause with a verb is deranked or said with a phase.", nameof(rank));
        }
        var predication = language.Predication;
        var subject = clause.Force.Subject;
        IEnumerable<Argument> placed = being.Complement.Value is Oblique at ? [at.Argument] : [];
        var (negative, modal) = Concord(language, clause.Negative, balanced.Modal, subject, null, [.. placed, .. ObliqueArguments(clause.Obliques)]);
        var subjectWords = SubjectWords(language, clause, rank, Role.IntransitiveSubject);
        var beforeAdjective = StandardBeforeAdjective(language);
        IReadOnlyList<(Closeness Rank, IReadOnlyList<Word> Words)> modifiers =
            [(Closeness.Reason, balanced.Adverbs), .. Adverbials(language, clause.Adverbs, clause.Obliques), (Closeness.Complement, beforeAdjective ? [] : standard)];
        IReadOnlyList<Word> Standing(IReadOnlyList<Word> adjective) => beforeAdjective ? [.. standard, .. adjective] : adjective;
        VerbComplex Be(Lexeme copula) => Complex(language, copula, clause.Force, rank, negative, modal, null);
        bool Drops(bool locational) => clause.Force.Value is Declarative stated && DropsCopula(language, stated, negative, modal, locational);
        if (PredicateAdjectives(language, being.Complement) is var (adjectives, between) && predication.Adjectives == PredicativeAdjectives.Verbal)
        {
            //a property word inflects as a verb in a language whose adjectives are verbs, and the last one's is the finite verb
            var lexemes = adjectives.Select(a => language.Lexicon.Adjective(a.Meaning)).ToList();
            var verbs = lexemes.Select(lexeme => Be(lexeme with { PartOfSpeech = PartOfSpeech.Verb })).ToList();
            var predicate = Join(adjectives.Select((a, i) => Graded(Intensified(language, Linearized(language, verbs[i]), lexemes[i].Root, a), lexemes[i].Root, degree)), between);
            return new(Flat(Order(language.WordOrder, subjectWords, Standing(predicate), null, WhereObliquesGo(language, modifiers))), verbs[^1].Outermost.Root);
        }

        //a location is an oblique, and goes where obliques go, with the language's locational copula
        if (being.Complement.Value is Oblique location)
        {
            if (location.Role != Role.Location)
            {
                throw new ArgumentException($"A copular clause can say where its subject is, but not its {location.Role}.", nameof(clause));
            }
            if (standard.Count > 0 || degree.HasValue)
            {
                throw new ArgumentException("Only an adjective can be compared.", nameof(clause));
            }
            var be = Drops(locational: true) ? null : Be(predication.CopulaFor(locational: true));
            return new(Flat(Order(language.WordOrder, subjectWords, be is null ? [] : Linearized(language, be), null,
                WhereObliquesGo(language, [(Closeness.Place, Oblique(language, location)), .. modifiers]))), be?.Outermost.Root);
        }

        /*
          a predicate noun takes the same case as an intransitive subject. A predicate adjective, in a
          language whose adjectives agree, agrees with the subject in number, gender and that case,
          as in Latin canis magnus est 'the dog is big'; otherwise it's just its root
         */
        var complement = being.Complement.Value is Adjective adjective
            ? Standing(Graded(PredicateComplement(language, adjective, subject), language.Lexicon.Adjective(adjective.Meaning).Root, degree))
            : PredicateComplement(language, being.Complement, subject);
        var copula = Drops(locational: false) ? null : Be(predication.Copula);
        return new(Flat(Order(language.WordOrder, subjectWords, copula is null ? [] : Linearized(language, copula), complement, WhereObliquesGo(language, modifiers))), copula?.Outermost.Root);
    }

    /*
      a standard of comparison goes right before the adjective in an OV language, as Japanese Taroo
      yori takai 'Taro-from tall' and Turkish kedi-den büyük 'cat-from big' have it, which Dryer finds
      follows the order of object and verb as degree words don't. In a VO language it stays nearest
      the verb where obliques go, which comes after the adjective in all but the few VO languages whose
      obliques go first. An exceeded standard goes the same way, though whether an OV language puts
      its 'exceed' phrase first is a guess
     */
    private static bool StandardBeforeAdjective(Language language) => language.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Final;

    /*
      a locational standard takes its role's case or adposition, and a particle's standard the plain
      case a subject takes, with the particle on its side. A standard that's exceeded is the object
      of a bare verb 'exceed', on the side of it the language puts objects. A conjoined comparison is
      two clauses, the second denying the property of the standard, as in 'Marta is tall, the men
      are not tall'. The adjective is comparative where the language marks it so alongside a
      standard, and always with no standard, which is 'more' than something already known
     */
    public static Sentence Realize(Language language, Comparative comparative, string translation) => Sentence(language, Words(language, comparative, []).Words, translation);

    private static ClauseWords Words(Language language, Comparative comparative, IReadOnlyList<Word> adverbs)
    {
        if (comparative.Clause is not { Predicate.Value: Being { Complement.Value: Adjective } being, Force.Value: Declarative stated } clause)
        {
            throw new ArgumentException("Only an adjective can be compared, in a clause that states it.", nameof(comparative));
        }
        var rank = new Balanced(null, null, adverbs);
        if (!comparative.Standard.HasValue)
        {
            return Copular(language, clause, being, rank, [], language.Degree.Marker);
        }
        var standard = comparative.Standard.GetValueOrDefault();
        var degree = language.Degree.WithStandard ? language.Degree.Marker : (DegreeMarker?)null;
        return language.Comparison switch
        {
            LocationalComparison locational => Copular(language, clause, being, rank, Marked(language, standard, locational.Role), degree),
            ParticleComparison particle => Copular(language, clause, being, rank, Than(particle, Inflect(language, standard, Role.IntransitiveSubject)), degree),
            ExceedComparison exceed => Copular(language, clause, being, rank, Exceeding(language, exceed, standard), degree),
            ConjoinedComparison => Conjoined(Copular(language, clause, being, rank, [], null),
                Copular(language, clause with { Force = stated with { Subject = standard }, Negative = !clause.Negative, Obliques = [], Adverbs = [] }, being, new Balanced(null, null, []), [], null)),
        };

        //both clauses say the same verb, so either's will do, where the first dropped its copula and the second didn't
        static ClauseWords Conjoined(ClauseWords first, ClauseWords second) => new([.. first.Words, .. second.Words], first.Finite ?? second.Finite);

        static IReadOnlyList<Word> Than(ParticleComparison particle, IReadOnlyList<Word> standard) =>
            particle.BeforeStandard ? [new Word([particle.Than.Root]), .. standard] : [.. standard, new Word([particle.Than.Root])];
    }

    private static IReadOnlyList<Word> Exceeding(Language language, ExceedComparison exceed, Argument standard)
    {
        IReadOnlyList<Word> verb = [new Word([exceed.Exceed.Root])];
        var @object = Inflect(language, standard, Role.DirectObject);
        return language.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial ? [.. verb, .. @object] : [.. @object, .. verb];
    }

    //an adjective's words with 'very' beside it, if it has it. It goes on before any comparative
    //marker, so that the comparative's is the nearer, as in 'much bigger'
    private static IReadOnlyList<Word> Intensified(Language language, IReadOnlyList<Word> words, Root root, Adjective adjective) =>
        adjective.Intensified ? Graded(words, root, language.Intensifier) : words;

    //an adjective's words with a marker of degree, if it has one: an affix next to its root, inside
    //any agreement or tense, or a word beside it
    private static IReadOnlyList<Word> Graded(IReadOnlyList<Word> words, Root adjective, DegreeMarker? degree)
    {
        if (degree is not { } marker)
        {
            return words;
        }
        List<Word> compared = [.. words];
        var at = compared.FindIndex(w => w.Root == adjective);
        switch (marker.Value)
        {
            case DegreeAffix affix:
            {
                var morphemes = compared[at].Morphemes.ToList();
                var root = morphemes.FindIndex(m => m.Value is Root);
                morphemes.Insert(affix.Position == ExponentPosition.Prefix ? root : root + 1, affix.Marker);
                compared[at] = compared[at] with { Morphemes = morphemes };
                break;
            }
            case DegreeWord word:
                compared.Insert(word.BeforeAdjective ? at : at + 1, Word.Marker(word.Marker, word.BeforeAdjective ? Lean.Forward : Lean.Back));
                break;
        }
        return compared;
    }

    //a predicate noun or adjective, said of this subject
    private static IReadOnlyList<Word> PredicateComplement(Language language, Complement complement, Argument subject) => complement.Value switch
    {
        NounPhrase noun => Inflect(language, noun, Role.IntransitiveSubject),
        AdjectiveCoordination together => Join(together.Adjectives.Select(a => PredicateComplement(language, a, subject)), language.Conjunctions.BetweenClauses(together.Conjunction)),
        Adjective adjective when language.AdjectiveAgreement == AdjectiveAgreement.Agreement =>
            Intensified(language, [AgreeingAdjective(language, language.Lexicon.Adjective(adjective.Meaning),
                Features(language.Paradigms.Nouns, [AgreementOf(language, subject).Number, CaseFor(language, language.RoleMarkers[Role.IntransitiveSubject])]),
                GenderOf(language, subject))], language.Lexicon.Adjective(adjective.Meaning).Root, adjective),
        Adjective adjective => Intensified(language, [new Word([language.Lexicon.Adjective(adjective.Meaning).Root])], language.Lexicon.Adjective(adjective.Meaning).Root, adjective),
        _ => throw new ArgumentException("A predicate is a noun or an adjective.", nameof(complement)),
    };

    //a language that drops subject pronouns leaves the verb's agreement, if any, to say who
    private static IReadOnlyList<Word> Subject(Language language, Argument subject, Role role) =>
        subject.Value is Pronoun && language.Pronouns.DropsSubjects ? [] : Inflect(language, subject, role);

    public static Sentence Realize(Language language, Modalized modalized, string translation) => Sentence(language, Words(language, modalized, []).Words, translation);

    //the clause, with its modal where its verb is, or as the complement of an epistemic modal verb
    private static ClauseWords Words(Language language, Modalized modalized, IReadOnlyList<Word> adverbs) =>
        language.Modals.Markers[modalized.Modality].Value is ModalVerb verb && modalized.Modality.Epistemic
            ? Impersonal(language, verb, modalized.Clause, modalized.Negative, adverbs)
            : Words(language, modalized.Clause, new Modal(language.Modals.Markers[modalized.Modality], modalized.Negative), adverbs);

    /*
      an epistemic modal verb is said impersonally, with what it judges as the 'that' clause of its
      verb, which keeps its own tense, as Greek borí na éfige 'it-may that he-left' and Hungarian
      lehet, hogy elment 'it may be that he left' do; the same verb said personally is the
      situational modal, as Greek boró na fíjo 'I can leave' is. The modal verb is present, since
      the judgment is the speaker's now, takes 'it' as its subject, which a language that drops its
      pronouns leaves unsaid, and the modal's negation. Its complement is marked as a proposition,
      and goes last or where the object goes, as one does; a language that deranks propositions
      loses the clause's tense, as it does in any complement
     */
    private static ClauseWords Impersonal(Language language, ModalVerb modal, Statement clause, bool negative, IReadOnlyList<Word> adverbs)
    {
        var it = new Pronoun(Person.Third, Number.Singular);
        var subject = Subject(language, it, Role.IntransitiveSubject);
        var verb = new VerbComplex(modal.Verb, [.. NegationLayers(language.Negation, negative)], new Finite(Tense.Present, Aspect.Imperfective, it));
        var (complement, balanced) = Complement(language, new ComplementClause(ComplementKind.Proposition, clause, SharesSubject: false), null);
        var last = balanced && language.Complementation.Last;
        return new([.. Flat(Order(language.WordOrder, subject, Linearized(language, verb), last ? null : complement, WhereObliquesGo(language, [(Closeness.Reason, adverbs)]))), .. last ? complement : []],
            verb.Outermost.Root);
    }

    private static ClauseWords Words(Language language, Statement clause, Modal modal, IReadOnlyList<Word> adverbs) => clause.Value is Clause { Force.Value: Declarative } c
        ? Words(language, Fold(language, c, modal), adverbs)
        : throw new ArgumentException("Only a clause that states something with a verb or be can take a modal.", nameof(clause));

    //how a modal or 'want' is marked on a verb, and whether it's negated
    private record Modal(ModalMarking Marking, bool Negative);

    public static Sentence Realize(Language language, Question question, string translation) => Sentence(language, Words(language, question).Words, translation);

    /*
      a content question puts its question word first, with the rest of its phrase, where the
      language fronts them, as English does, and otherwise leaves it in its role's own place, as
      Mandarin and Japanese do; 'why' and 'how' go first too, or else where obliques go, as a phrase
      saying why or how would. It needs no other marking. A yes-no question takes the language's
      particle, its affix on the finite verb, or the finite verb first, or nothing but intonation. A
      language that puts the verb first in a yes-no question puts it right after a fronted question
      word in a content one, as German does
      TODO many languages use their yes-no marker in content questions too, as Japanese does its ka,
      which WALS doesn't count
     */
    private static ClauseWords Words(Language language, Question question)
    {
        var questions = language.Questions;
        var said = Words(language, question.Statement);
        var finite = said.Finite;
        var asked = Asked(language, question.Statement);
        if (asked is null && question.Adverb is null)
        {
            return new(Polar(language, said.Words, finite), finite);
        }
        IReadOnlyList<Word> adverb = question.Adverb is { } which ? [new Word([questions.Adverbs[which].Root])] : [];
        if (!questions.FrontsQuestionWords)
        {
            return Words(language, question.Statement, adverb);
        }
        var words = said.Words.ToList();
        List<Word> fronted = [.. adverb];
        if (asked is var (root, phrase) && TakePhrase(words, root, phrase) is var (taken, rest))
        {
            fronted.AddRange(taken);
            words = rest;
        }
        return new([.. fronted, .. questions.Polar.Value is QuestionInversion ? Inverted(words, finite) : words], finite);
    }

    //a clause that can be asked, with words said where its obliques go, before them
    private static ClauseWords Words(Language language, Statement statement, IReadOnlyList<Word> adverbs) => statement.Value switch
    {
        Clause { Force.Value: Declarative } c => Words(language, Fold(language, c, null), adverbs),
        Comparative c => Words(language, c, adverbs),
        Modalized m => Words(language, m, adverbs),
        _ => throw new ArgumentException("Only a clause with a verb or be can be asked.", nameof(statement)),
    };

    /*
      a yes-no question's marker: a particle first, last or after the first word, or an affix on the
      finite verb outside everything else on it, or on the last word where there's no verb, as a
      copular clause that drops its copula has none, the way Turkish puts mi on the predicate
     */
    private static IReadOnlyList<Word> Polar(Language language, IReadOnlyList<Word> clause, Root? finite) => language.Questions.Polar switch
    {
        QuestionParticle { Position: QuestionParticlePosition.Initial } p => [Word.Marker(p.Marker, Lean.Forward), .. clause],
        QuestionParticle { Position: QuestionParticlePosition.Final } p => [.. clause, Word.Marker(p.Marker, Lean.Back)],
        //a particle in second place leans on the first word, as Latin -ne does
            QuestionParticle p => [.. clause.Take(1), Word.Marker(p.Marker, Lean.Back), .. clause.Skip(1)],
        QuestionAffix a => Affixed(clause, a, finite),
        QuestionInversion => Inverted(clause, finite),
        QuestionIntonation => clause,
    };

    private static IReadOnlyList<Word> Affixed(IReadOnlyList<Word> clause, QuestionAffix affix, Root? finite)
    {
        List<Word> words = [.. clause];
        var at = words.FindLastIndex(w => w.Root is { } r && r == finite);
        at = at < 0 ? words.Count - 1 : at;
        words[at] = words[at] with
        {
            Morphemes = affix.Position == ExponentPosition.Prefix ? [affix.Marker, .. words[at].Morphemes] : [.. words[at].Morphemes, affix.Marker],
        };
        return words;
    }

    //the finite verb first, leaving any negation or other verb behind, as German hast du ihn nicht
    //gesehen? 'didn't you see him?' does. A clause that drops its copula has nothing to move
    private static IReadOnlyList<Word> Inverted(IReadOnlyList<Word> clause, Root? finite)
    {
        List<Word> words = [.. clause];
        var at = words.FindIndex(w => w.Root is { } r && r == finite);
        if (at < 0)
        {
            return words;
        }
        var verb = words[at];
        words.RemoveAt(at);
        return [verb, .. words];
    }

    //the question word a statement asks with, if it has one, and its whole phrase as the statement
    //says it, with the role's case and adposition
    private static (Root Root, IReadOnlyList<Word> Phrase)? Asked(Language language, Statement statement)
    {
        foreach (var (argument, role, oblique) in Roles(language, statement))
        {
            if (argument.Value is QuestionWord question)
            {
                var marked = oblique is not null || role == Role.Agent;
                return (language.Questions.Words[question.Word].Root, marked ? Marked(language, argument, role) : Inflect(language, argument, role));
            }
        }
        return null;
    }

    //what fills each of a clause's roles, as the language says it: a passive in a language without
    //one is said as the active
    private static IEnumerable<Filler> Roles(Language language, Statement statement)
    {
        return statement.Value switch
        {
            Clause c => Voiced(language, c).Fillers,
            Comparative c => Roles(language, c.Clause),
            Modalized m => Roles(language, m.Clause),
            _ => [],
        };
    }

    /*
      a verb with its passive, its modal and its negation, each where the language puts it. A modal
      verb takes the tense, the agreement and the modal's negation, and the verb itself goes bare
      beside it with its own, as in French il ne peut pas nager 'he can't swim'. A modal affix goes
      next to the root, outside only the passive, as in Turkish gör-ül-ebil-ir 'can be seen', and a
      modal particle beside the verb. With either of those, a negation goes where it always does,
      whichever it negates
      A perfect or progressive auxiliary is a verb like a modal verb: it takes the tense, the
      agreement, the negation and any modal, as English 'hasn't gone' and 'must have gone' do, and
      the verb goes bare beside it with its passive. Its affix goes next to the root, outside only
      the passive and inside a modal, as aspect goes nearer the root than mood, and its particle
      beside the verb. An inceptive or cessative goes nearer the verb still, inside the aspect and
      outside the passive, as 'begin to be seen' begins what's passive, and stays on the verb where
      an auxiliary takes the tense, as 'was starting to sing' would say it
      TODO a language whose negation is an affix could show its scope by its order, as Turkish
      git-me-meli 'must not go' puts its negation inside the modal and gid-eme-z 'cannot go' outside
     */
    private static VerbComplex Complex(Language language, Lexeme verb, Tense tense, Aspect aspect, Argument subject, bool negative, Modal? modal, Passive? passive,
        AspectMarking? phase)
    {
        var (said, inflected, marking) = language.Aspects.Said(tense, aspect);
        List<VerbLayer> layers = [.. PassiveLayers(passive), .. PhaseLayers(phase)];
        if (marking is { } aspectual)
        {
            layers.Add(aspectual switch
            {
                Bound b => new VerbMarker(b, Reach.Root),
                Free f => new VerbMarker(f, Reach.Root),
                AspectAuxiliary a => new Auxiliary(language.AuxiliaryVerb(a, aspect), a.BeforeVerb ? Side.Before : Side.After),
            });
            //an auxiliary says a state, having done or being at it, so it's imperfective
            inflected = aspectual.Value is AspectAuxiliary ? Aspect.Imperfective : inflected;
        }
        if (modal is null)
        {
            layers.AddRange(NegationLayers(language.Negation, negative));
        }
        else
        {
            switch (modal.Marking)
            {
                case ModalVerb m:
                    layers.AddRange(NegationLayers(language.Negation, negative));
                    layers.Add(new Auxiliary(m.Verb, m.BeforeVerb ? Side.Before : Side.After));
                    layers.AddRange(NegationLayers(language.Negation, modal.Negative));
                    break;
                case Bound m:
                    layers.Add(new VerbMarker(m, Reach.Root));
                    layers.AddRange(NegationLayers(language.Negation, negative || modal.Negative));
                    break;
                case Free m:
                    layers.Add(new VerbMarker(m, Reach.Edge));
                    layers.AddRange(NegationLayers(language.Negation, negative || modal.Negative));
                    break;
            }
        }
        return new VerbComplex(verb, layers, new Finite(said, inflected, subject));
    }

    //voice sits closer to the root than tense or agreement, as it nearly always does
    private static IEnumerable<VerbLayer> PassiveLayers(Passive? passive) => passive is null ? [] : [new VerbMarker(passive.Exponent, Reach.Root)];

    //an inceptive's or cessative's marker, never an auxiliary, since a language without one says it with a verb
    private static IEnumerable<VerbLayer> PhaseLayers(AspectMarking? phase) => phase is { } p
        ? [p switch
        {
            Bound b => new VerbMarker(b, Reach.Root),
            Free f => new VerbMarker(f, Reach.Root),
            AspectAuxiliary => throw new ArgumentException("A phase isn't marked by an auxiliary.", nameof(phase)),
        }]
        : [];

    //a negation's markers go outside the verb's other affixes, and its words outside its other words
    private static IEnumerable<VerbLayer> NegationLayers(Negation negation, bool negative) =>
        negative ? negation.Markers.Select(m => (VerbLayer)new VerbMarker(m, Reach.Edge)) : [];

    /*
      a verb's words. Where it has an auxiliary, the outermost one takes its inflection and the
      markers outside it, beside everything inside it said bare, so a modal verb goes outside an
      aspect's auxiliary and the verb they share, as Thai tɔ̂ŋ kin sèt 'must eat finish' has it.
      Otherwise it's the verb inflected, with the markers that reach its root put there from the
      outermost in, so each goes nearer the root than the one before, and those that reach the edge
      from the innermost out, so each goes outside the one before
     */
    private static IReadOnlyList<Word> Linearized(Language language, VerbComplex complex)
    {
        var layers = complex.Layers;
        var at = layers.ToList().FindLastIndex(l => l.Value is Auxiliary);
        if (at >= 0 && layers[at].Value is Auxiliary auxiliary)
        {
            var inside = Linearized(language, complex with { Layers = [.. layers.Take(at)], Finiteness = new Bare() });
            var outside = Linearized(language, new VerbComplex(auxiliary.Verb, [.. layers.Skip(at + 1)], complex.Finiteness));
            return auxiliary.Side == Side.Before ? [.. outside, .. inside] : [.. inside, .. outside];
        }
        var markers = layers.Select(l => (VerbMarker)l.Value!).ToList();
        var words = Inflected(language, complex.Verb, complex.Finiteness);
        foreach (var marker in markers.Where(m => m.Reach == Reach.Root).Reverse())
        {
            words = marker.Exponent.Place(words, Exponent.Host(words), Reach.Root);
        }
        foreach (var marker in markers.Where(m => m.Reach == Reach.Edge))
        {
            words = marker.Exponent.Place(words, Exponent.Host(words), Reach.Edge);
        }
        return words;
    }

    //a verb's own words, inflected as its finiteness says, before any of its markers
    private static IReadOnlyList<Word> Inflected(Language language, Lexeme verb, Finiteness finiteness) => finiteness switch
    {
        Finite f => Verb(language, verb, f.Tense, f.Aspect, f.Subject),
        Imperative i => language.Commands.Imperative(verb, i.Marker),
        Nominalized n => AsNoun(language, verb, n.Case),
        Bare => [new Word([verb.Root])],
    };

    //the verb agrees with its subject. TODO many ergative languages agree with the absolutive instead
    private static IReadOnlyList<Word> Verb(Language language, Lexeme verb, Tense tense, Aspect aspect, Argument subject)
    {
        var (person, number) = AgreementOf(language, subject);
        var paradigm = language.Paradigms.Verbs;
        var words = paradigm.Inflect(verb, Features(paradigm, [tense, aspect, person, number])).ToList();
        var genders = language.Genders;
        var gender = genders.VerbsAgree ? GenderOf(language, subject) : null;
        if (gender is not null)
        {
            var main = words.FindIndex(w => w.Root is not null);
            words[main] = genders.Agree(words[main], gender, nextToRoot: false);
        }
        return words;
    }

    //the number a noun takes: its own, unless a numeral leaves it singular, as in Turkish üç kurt
    //'three wolf'
    private static Number NumberOf(Language language, Noun noun) =>
        noun.Count is not null && language.Numerals.NounStaysSingular ? Number.Singular : noun.Number;

    /*
      what a verb or adjective agrees with: a noun is third person, in the number of its word, so
      that a verb is singular with 'three wolf' too, as in Hungarian. Things joined by 'and' are
      plural, or dual if there are two and the language has a dual, as Slovene's are, in the first
      person if any of them is, else the second if any is, so 'you and I' is 'we'; 'or' agrees with
      its last, as English 'either the dogs or the cat is'
     */
    private static (Person Person, Number Number) AgreementOf(Language language, Argument argument) => argument switch
    {
        NounPhrase n => AgreementOf(language, n.Head),
        Pronoun p => (p.Person, p.Number),
        DemonstrativePronoun d => (Person.Third, d.Number),
        QuestionWord or DemonstrativePlace => (Person.Third, Number.Singular),
        //a gap or relative pronoun agrees as its noun does, as in 'the dogs that sleep'
        Gap { Head: { } head } => AgreementOf(language, head),
        Gap => (Person.Third, Number.Singular),
        RelativeWord word => AgreementOf(language, word.Head),
        //a clause is agreed with as a third person singular, as English 'that he came is strange' is
        ComplementClause or Quotation => (Person.Third, Number.Singular),
        Coordination { Conjunction: Conjunction.Or } c => AgreementOf(language, c.Items[^1]),
        Coordination c => (c.Items.Select(i => AgreementOf(language, i).Person).MinBy(p => p == Person.First ? 0 : p == Person.Second ? 1 : 2)!,
            c.Items.Count == 2 ? Number.Dual : Number.Plural),
    };

    //an indefinite pronoun is singular, as 'everybody is here' is
    private static (Person Person, Number Number) AgreementOf(Language language, Nominal nominal) => nominal switch
    {
        Noun noun => (Person.Third, NumberOf(language, noun)),
        IndefinitePronoun => (Person.Third, Number.Singular),
    };

    /*
      the gender something agrees with: a noun's own, or a pronoun's, if the language has that gender.
      A pronoun with no gender given has none to agree with, since what it refers to isn't known
      Things joined by 'or' take their last's gender; things joined by 'and' share it if they all have
      one, and otherwise take the masculine, or the animate, as real gender systems mostly resolve them
     */
    private static Gender? GenderOf(Language language, Argument argument) => argument.Value switch
    {
        NounPhrase n => GenderOf(language, n.Head),
        Pronoun { Gender: { } g } when language.Genders.Values.Contains(g) => g,
        Gap { Head: { } head } => GenderOf(language, head),
        RelativeWord word => GenderOf(language, word.Head),
        Coordination { Conjunction: Conjunction.Or } c => GenderOf(language, c.Items[^1]),
        Coordination c => Resolved([.. c.Items.Select(i => GenderOf(language, i))]),
        _ => null,
    };

    private static Gender? GenderOf(Language language, Nominal nominal) => language.GenderOf(language.Lexicon.Noun(nominal.Meaning));

    private static Gender? Resolved(IReadOnlyList<Gender?> genders) =>
        genders.Distinct().Count() == 1 ? genders[0]
        : genders.Contains(Gender.Masculine) ? Gender.Masculine
        : genders.Contains(Gender.Animate) ? Gender.Animate
        : genders.FirstOrDefault(g => g is not null);

    /*
      an adjective agreeing with a noun: it takes the noun's markers for these features, but only its
      affixes, since a particle belongs to the phrase and is said once, and a marker for its gender.
      Its zero markers go unglossed, since the noun's gloss already shows them
     */
    private static Word AgreeingAdjective(Language language, Lexeme adjective, IReadOnlyList<Grammeme> features, Gender? gender)
    {
        var inflected = language.Paradigms.Nouns.Inflect(adjective with { PartOfSpeech = PartOfSpeech.Noun }, features);
        var word = inflected.First(w => w.Root is not null) with { Covert = null };
        return gender is null ? word : language.Genders.Agree(word, gender, nextToRoot: true);
    }

    //each constituent keeps its own particles with it
    private static IReadOnlyList<Word> Flat(IEnumerable<IReadOnlyList<Word>> constituents) => [.. constituents.SelectMany(c => c)];

    private static Sentence Sentence(Language language, IReadOnlyList<Word> words, string translation) =>
        new(language.Phonology.RealizePhrase(words), translation, language.Orthography);

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Role role) =>
        Inflect(language, argument, CaseFor(language, language.RoleMarkers[role]), Localization.None);

    //a noun phrase on its own, in the case a noun is cited in, as in a dictionary or a name
    public static IReadOnlyList<Word> Cited(Language language, NounPhrase phrase) => Inflect(language, phrase, Citation(language), Localization.None);

    //a localization is None for anything that isn't a place with a configuration in the noun
    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Case @case, Localization localization)
    {
        var nouns = language.Paradigms.Nouns;
        var pronouns = language.Pronouns.Paradigm;
        return argument switch
        {
            NounPhrase phrase => Relativized(language, phrase, phrase.Head switch
            {
                IndefinitePronoun pronoun => Indefinite(language, pronoun, @case, localization),
                Noun noun => Possessed(language, noun, Determined(language, noun, Modified(language, noun, Features(nouns, [NumberOf(language, noun), @case, localization])))),
            }),
            Pronoun pronoun => pronouns.Inflect(language.Pronouns.For(pronoun), Features(pronouns, [@case, localization])),
            //a demonstrative standing alone, or 'here' or 'there', inflects as a noun does
            DemonstrativePronoun demonstrative => nouns.Inflect(language.Demonstratives.Pronoun(demonstrative.Distance), Features(nouns, [demonstrative.Number, @case, localization])),
            DemonstrativePlace place => nouns.Inflect(language.Demonstratives.Place(place.Distance), Features(nouns, [Number.Singular, @case, localization])),
            Coordination coordination => Joined(language, coordination, @case, localization),
            QuestionWord question => nouns.Inflect(language.Questions.Words[question.Word], Features(nouns, [Number.Singular, @case, localization])),
            ComplementClause or Quotation => throw new ArgumentException("A clause or a quotation can only be an object, which Object says.", nameof(argument)),
            //a gap says nothing; a relative pronoun inflects as a noun does, in its noun's number
            Gap => [],
            RelativeWord word => nouns.Inflect(word.Word, Features(nouns, [AgreementOf(language, word.Head).Number, @case, localization])),
        };
    }

    /*
      an indefinite pronoun, as the language makes one of its kind: a word of its own; the question
      word for its kind with its series' marker, which goes on it as a demonstrative goes on a noun,
      inside its case, as in Japanese dare-ka-ni 'who-INDF-DAT'; or the noun for its kind with its
      series' quantifier, as 'some person'. Each inflects as a singular noun
     */
    private static IReadOnlyList<Word> Indefinite(Language language, IndefinitePronoun pronoun, Case @case, Localization localization)
    {
        var indefinites = language.Indefinites;
        var nouns = language.Paradigms.Nouns;
        var features = Features(nouns, [Number.Singular, @case, localization]);
        IReadOnlyList<Word> Marked(Exponent marker)
        {
            var word = language.Questions.Words[Indefinites.QuestionWordFor(pronoun.Kind)];
            return Articles.Mark(nouns.Inflect(word, features), word.Root, marker.Marker, marker.Position);
        }
        return indefinites.SourceOf(pronoun.Kind) switch
        {
            SpecialIndefinites special => nouns.Inflect(special.Words[(pronoun.Series, pronoun.Kind)], features),
            InterrogativeIndefinites interrogative => Marked(interrogative.Markers[pronoun.Series]),
            GenericNounIndefinites => Modified(language, pronoun.AsNoun, features),
        };
    }

    /*
      a noun phrase's words with its article and demonstrative, agreeing with the noun's gender and
      with how many things there are. A demonstrative makes the phrase definite, so it takes the
      definite article only where the language keeps one beside a demonstrative, outside the article
     */
    private static IReadOnlyList<Word> Determined(Language language, Noun noun, IReadOnlyList<Word> words)
    {
        var lexeme = language.Lexicon.Noun(noun.Meaning);
        var gender = language.GenderOf(lexeme);
        IReadOnlyList<Word> Article(Definiteness? definiteness) => language.Articles.Apply(words, lexeme.Root, definiteness, noun.Number, gender);
        return noun.Determiner switch
        {
            null => Article(null),
            Definiteness definiteness => Article(definiteness),
            Distance distance => language.Demonstratives.Apply(Article(language.Articles.WithDemonstrative ? Definiteness.Definite : null), lexeme.Root, distance, noun.Number, gender),
        };
    }

    /*
      each part in the case, with the language's 'and' or 'or' between each two, or for 'and' in a
      language whose 'and' is 'with', every part after the first marked as a companion. An adposition
      for the whole is said once, outside it all
     */
    private static IReadOnlyList<Word> Joined(Language language, Coordination coordination, Case @case, Localization localization)
    {
        var conjunctions = language.Conjunctions;
        var parts = coordination.Items.Select(item => Inflect(language, item, @case, localization)).ToList();
        return (coordination.Conjunction, conjunctions.NounAnd.Value) switch
        {
            (Conjunction.Or, _) => Join(parts, conjunctions.Or),
            (Conjunction.And, AndWith) => [.. parts[0], .. coordination.Items.Skip(1).SelectMany(item => Marked(language, item, Role.Companion))],
            (Conjunction.And, AndWord and) => Join(parts, and.Word),
            (Conjunction.And, _) => Join(parts, null),
            _ => throw new ArgumentException("Only 'and' and 'or' join noun phrases.", nameof(coordination)),
        };
    }

    /*
      a phrase with its possessor, if it has one, before or after it all, as the language puts them.
      The possessor takes the possessive role's case or adposition where the language marks it, and
      is plain otherwise. Where the language marks the possessed noun, the noun takes an affix
      agreeing with the possessor, and where it marks only that, the affix is all a pronoun possessor
      needs, as in Hungarian ház-am 'my house'
     */
    private static IReadOnlyList<Word> Possessed(Language language, Noun noun, IReadOnlyList<Word> words)
    {
        if (!noun.Possessor.HasValue)
        {
            return words;
        }
        var possessor = noun.Possessor.GetValueOrDefault();
        var possession = language.Possession;
        List<Word> possessed = [.. words];
        if (possession.Agreement is { } agreement)
        {
            var root = language.Lexicon.Noun(noun.Meaning).Root;
            var at = possessed.FindIndex(w => w.Root == root);
            var (person, number) = AgreementOf(language, possessor);
            possessed[at] = agreement.Agree(possessed[at], person, number);
        }
        if (!possession.MarksPossessor && possession.MarksPossessed && possessor.Value is Pronoun)
        {
            return possessed;
        }
        var owner = possession.MarksPossessor ? Marked(language, possessor, Role.Possessor) : Inflect(language, possessor, Citation(language), Localization.None);
        return possession.PossessorFirst ? [.. owner, .. possessed] : [.. possessed, .. owner];
    }

    /*
      the noun's words with its adjectives placed around it: after the noun in a language whose noun
      phrases are head-initial, before it in one whose are head-final, nearest the noun as
      ModifierOrder says.
      A numeral goes outside them, before or after, as the language puts numerals, so that 'three big
      wolves' is either three big wolf or wolf big three. An ordinal goes like the outermost
      adjective, inside the numeral, as in 'the first three big wolves', but never agrees, even where
      adjectives do. A quantifier goes outside the numeral, before or after as the language puts
      quantifiers, as in 'all three wolves', and doesn't agree either. The noun's particles stay
      outside it all
     */
    private static IReadOnlyList<Word> Modified(Language language, Noun noun, IReadOnlyList<Grammeme> features)
    {
        var nounLexeme = language.Lexicon.Noun(noun.Meaning);
        var inflected = language.Paradigms.Nouns.Inflect(nounLexeme, features);
        if (noun.Adjectives.Count == 0 && noun.Count is null && noun.Ordinal is null && noun.Quantifier is null)
        {
            return inflected;
        }
        var main = inflected.ToList().FindIndex(w => w.Root is not null);
        var head = inflected[main];
        var agreement = language.AdjectiveAgreement;

        var headFirst = language.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial;
        //written in English order, which is outermost first before the noun, and so mirrored after
        //it where the language mirrors them
        var written = headFirst && language.WordOrder.MirrorsAdjectives ? noun.Adjectives.Reverse() : noun.Adjectives;
        var adjectives = written.SelectMany(a =>
        {
            var lexeme = language.Lexicon.Adjective(a.Meaning);
            return Intensified(language, [agreement == AdjectiveAgreement.Agreement
                ? AgreeingAdjective(language, lexeme, features, language.GenderOf(nounLexeme))
                : new Word([lexeme.Root])], lexeme.Root, a);
        }).ToList();

        IReadOnlyList<Word> ordinal = noun.Ordinal is { } nth ? language.Numerals.SayOrdinal(nth) : [];
        List<Word> words = headFirst ? [head, .. adjectives, .. ordinal] : [.. ordinal, .. adjectives, head];
        var at = headFirst ? 0 : words.Count - 1;
        if (noun.Count is { } count)
        {
            var numerals = language.Numerals;
            List<Word> numeral = [.. numerals.Say(count).Select(n => new Word([n.Root]))];
            words = numerals.BeforeNoun ? [.. numeral, .. words] : [.. words, .. numeral];
            at += numerals.BeforeNoun ? numeral.Count : 0;
        }
        if (noun.Quantifier is { } quantifier)
        {
            var indefinites = language.Indefinites;
            var word = new Word([indefinites.Quantifiers[quantifier].Root]);
            words = indefinites.QuantifiersBeforeNoun ? [word, .. words] : [.. words, word];
            at += indefinites.QuantifiersBeforeNoun ? 1 : 0;
        }
        if (agreement == AdjectiveAgreement.Phrasal)
        {
            //the noun's affixes move to the edges of the phrase
            var root = head.Morphemes.ToList().FindIndex(m => m.Value is Root);
            var prefixes = head.Morphemes.Take(root).ToList();
            var suffixes = head.Morphemes.Skip(root + 1).ToList();
            words[at] = head with { Morphemes = [head.Morphemes[root]] };
            words[0] = words[0] with { Morphemes = [.. prefixes, .. words[0].Morphemes] };
            words[^1] = words[^1] with { Morphemes = [.. words[^1].Morphemes, .. suffixes] };
        }
        return [.. inflected.Take(main), .. words, .. inflected.Skip(main + 1)];
    }

    //the case a noun takes for a role, which is the plain citation case where the role has none
    private static Case CaseFor(Language language, RoleMarker marker) => marker.Case ?? Citation(language);

    //the plain case a noun is cited in
    private static Case Citation(Language language) => language.MorphosyntacticAlignment switch
    {
        MorphosyntacticAlignment.NominativeAccusative => Case.Nominative,
        MorphosyntacticAlignment.ErgativeAbsolutive => Case.Absolutive,
        _ => Case.Direct,
    };

    /*
      each category the paradigm inflects for takes whichever wanted value it distinguishes. A value
      the language doesn't have falls back on the nearest one it does, and failing that on the
      category's first value
     */
    private static List<Grammeme> Features(Paradigm paradigm, IReadOnlyList<Grammeme> wanted) =>
        [.. paradigm.Categories.Select(category =>
            wanted.Where(w => w.GetType() == category.Values[0].GetType())
                .Select(w => Nearest(w, category.Values))
                .FirstOrDefault(v => v is not null) ?? category.Values[0])];

    //the value itself if it's available, or else the nearest available one it falls back on
    public static Grammeme? Nearest(Grammeme wanted, IReadOnlyCollection<Grammeme> available) =>
        ((Grammeme[])[wanted, .. Fallbacks.GetValueOrDefault(wanted, [])]).FirstOrDefault(available.Contains);

    /*
      what a value turns into in a language that doesn't distinguish it, nearest first: a dual or a
      paucal is still more than one, a language without a future speaks of it in the present, as
      English often does, and an aorist is a perfective. A middle distance is far in a language with
      only near and far, as English 'that' points near the hearer too, a demonstrative that says no
      distance is the far one, as English uses 'that', and any distance is the one demonstrative of
      a language that tells none apart
     */
    private static readonly Dictionary<Grammeme, Grammeme[]> Fallbacks = new()
    {
        [Number.Dual] = [Number.Paucal, Number.Plural],
        [Number.Paucal] = [Number.Plural],
        [Number.Collective] = [Number.Plural],
        [Tense.Future] = [Tense.Present],
        [Aspect.Aorist] = [Aspect.Perfective],
        [Mood.Subjunctive] = [Mood.Indicative],
        [Mood.Imperative] = [Mood.Subjunctive, Mood.Indicative],
        [Distance.Proximal] = [Distance.Neutral],
        [Distance.Medial] = [Distance.Distal, Distance.Neutral],
        [Distance.Distal] = [Distance.Neutral],
        [Distance.Neutral] = [Distance.Distal],
    };

    //the order's own letters say where each constituent goes; an intransitive clause just has no O.
    //Obliques go together, as X, before or after the verb and object, or just before the verb
    private static IEnumerable<IReadOnlyList<Word>> Order(WordOrder order, IReadOnlyList<Word> subject, IReadOnlyList<Word> verb, IReadOnlyList<Word>? @object, IReadOnlyList<Word> obliques) =>
        Letters(order, @object is not null, obliques.Count > 0).Select(letter => letter switch
        {
            'S' => subject,
            'V' => verb,
            'X' => obliques,
            _ => @object!,
        });

    private static List<char> Letters(WordOrder order, bool hasObject, bool hasObliques)
    {
        var letters = order.ConstituentWordOrder.ToString().Where(letter => letter != 'O' || hasObject).ToList();
        if (hasObliques)
        {
            var verbAndObject = letters.Select((letter, i) => (letter, i)).Where(p => p.letter != 'S').Select(p => p.i).ToList();
            letters.Insert(order.ObliquePosition switch
            {
                ObliquePosition.BeforeVerbAndObject => verbAndObject.Min(),
                ObliquePosition.BetweenObjectAndVerb => letters.IndexOf('V'),
                _ => verbAndObject.Max() + 1,
            }, 'X');
        }
        return letters;
    }

    /*
      a clause's negation and modal as its verb says them. A negative indefinite in it, as in 'nobody
      came', negates the verb too where the language has it do so: always, never, or only where the
      indefinite comes after the verb, as Spanish has no vino nadie but nadie vino. Where it comes is
      where its role goes in the clause's order; a question word or relative pronoun put first doesn't
      move it. With a modal, it's the modal that's negated, as in Russian nikto ne možet 'nobody not
      can'
     */
    private static (bool Negative, Modal? Modal) Concord(Language language, bool negative, Modal? modal, Argument? subject, Argument? @object, IReadOnlyList<Argument> obliques)
    {
        if (negative || modal is { Negative: true })
        {
            return (negative, modal);
        }
        var letters = Letters(language.WordOrder, @object is not null, obliques.Count > 0);
        bool After(char letter) => letters.IndexOf(letter) > letters.IndexOf('V');
        IEnumerable<(Argument Argument, char Letter)> roles =
            [.. Given(subject).Select(a => (a, 'S')), .. Given(@object).Select(a => (a, 'O')), .. obliques.Select(a => (a, 'X'))];
        var negated = language.Indefinites.Negation switch
        {
            NegativeIndefiniteNegation.Always => roles.Any(r => Indefinites.IsNegative(r.Argument)),
            NegativeIndefiniteNegation.AfterVerb => roles.Any(r => Indefinites.IsNegative(r.Argument) && After(r.Letter)),
            _ => false,
        };
        return !negated ? (false, modal) : modal is null ? (true, null) : (false, modal with { Negative = true });
    }

    private static IEnumerable<Argument> Given(Argument? argument) => argument.HasValue ? [argument.GetValueOrDefault()] : [];

    private static IReadOnlyList<Argument> ObliqueArguments(ValueList<Oblique> obliques) => [.. obliques.Select(o => o.Argument)];
}

/// <summary>
/// A sentence as it's said: its words in groups, each a word with a root and the clitics leaning
/// on it, said as one word, or a clitic with nothing to lean on.
/// </summary>
public record Sentence(IReadOnlyList<SurfaceForm> Groups, string Translation, Orthography Orthography)
{
    /// <summary>Every word of the sentence, in order, with the group it's said in.</summary>
    public IReadOnlyList<SentenceWord> Words => [.. Groups.SelectMany(g => g.Words.Select(w => new SentenceWord(g, w)))];

    //clitics are written as words of their own, as French and Greek write theirs, and a word that
    //lost every sound to its host, as a clitic's vowel can, isn't written at all
    public string Text => string.Join(" ", Words.Select(w => w.Spell(Orthography)).Where(s => s.Length > 0));

    /// <summary>
    /// The IPA of the sentence as it's said, a group at a time, with ‿ where a silent consonant is
    /// said before the next word's vowel, as in [pəˈtit‿aˈmi].
    /// </summary>
    public string Transcribed() =>
        $"[{string.Concat(Groups.Select((g, i) => g.Transcribed() + (i == Groups.Count - 1 ? "" : g.LinksToNext ? "‿" : " ")))}]";

    /*
       https://www.eva.mpg.de/lingua/resources/glossing-rules.php
      the sentence, then, if asked for, how it's said, then its morphemes and their glosses aligned
      group by group, with = between a host and its clitics, then a free translation. How it's said
      is left out unless asked for, since most uses only want the language as it's written
     */
    public string Interlinear(bool phonetic)
    {
        var widths = Groups.Select(g => Math.Max(g.Segmented(Orthography).Length, g.Gloss.Length)).ToList();
        string Aligned(Func<SurfaceForm, string> line) =>
            string.Join("  ", Groups.Select((g, i) => line(g).PadRight(widths[i]))).TrimEnd();
        return string.Join(Environment.NewLine,
            [Text, .. phonetic ? [Transcribed()] : Array.Empty<string>(), Aligned(g => g.Segmented(Orthography)), Aligned(g => g.Gloss), $"'{Translation}'"]);
    }
}

/// <summary>One word of a sentence, and the group it's said in.</summary>
public record SentenceWord(SurfaceForm Group, Word Word)
{
    public string Gloss => Word.Gloss;

    public string Spell(Orthography orthography) => Group.Spell(orthography, Word);
}
