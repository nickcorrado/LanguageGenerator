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
public record NounPhrase(Nominal Head, Propositional? Relative);

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
/// noun fills, and a relative word what Syntax says in its place; in a dependent clause, a shared
/// subject is the subject of the clause it depends on.
/// </summary>
//what agrees with one depends on the language, so that's Syntax.AgreementOf's to say
public union Argument(NounPhrase, Pronoun, DemonstrativePronoun, Coordination, QuestionWord, DemonstrativePlace, Complement, Quotation, Gap, RelativeWord, Shared);
/// <summary>
/// A phrase saying where, to where, from where, which way, to whom, with whom or with what something
/// happened, what it was like, when it happened or for how long. A spatial one says where relative
/// to its place too, so 'into the river' is a goal In the river.
/// </summary>
public record Oblique(Role Role, Argument Argument, Configuration Configuration)
{
    public static IReadOnlyList<Role> Roles { get; } =
        [Role.Recipient, Role.Location, Role.Goal, Role.Source, Role.Route, Role.Companion, Role.Instrument, Role.Beneficiary, Role.Similative, Role.Time, Role.Duration];

    /// <summary>The roles that say where, which can take a configuration.</summary>
    public static IReadOnlyList<Role> Spatial { get; } = [Role.Location, Role.Goal, Role.Source, Role.Route];
}

public static class Syntax
{
    public static Rendering Realize(Language language, Sentence statement, string translation) => Rendering(language, Words(language, statement).Words, translation);

    /*
      a heading is its label, then its title, each as a noun is cited, said apart as phrases of their
      own, as a pause comes between them, which is written as a colon, and is said to no one.
      Anything else is said as one phrase
     */
    public static Rendering Realize(Language language, Utterance utterance, string translation)
    {
        if (utterance.Value is not Message { Content.Value: Fragment { Value: Heading heading } } message)
        {
            return Rendering(language, Words(language, utterance).Words, translation);
        }
        if (message.Addressee is not null)
        {
            throw new ArgumentException("A heading is said to no one.", nameof(utterance));
        }
        var phonology = language.Phonology;
        return new(phonology.RealizePhrase(Cited(language, heading.Label)), phonology.RealizePhrase(Cited(language, heading.Title)), translation, language.Orthography);
    }

    /*
      an utterance's words, as a sentence of its own or quoted: who it's said to first, in the
      addressee's case, the vocative where the language has one, and otherwise the case a noun is
      cited in, then what it says. Where a vocative goes is a matter of discourse more than grammar,
      which WALS doesn't count, and first is the commonest place. An interjection or an answer goes
      before who it's said to instead, as in 'hello, Marta' and 'no, Marta', which is a guess, since
      nobody counts it either
     */
    private static ClauseWords Words(Language language, Utterance utterance)
    {
        switch (utterance)
        {
            case Call call:
                return new(Marked(language, call.Addressee, Role.Addressee), null);
            case Message { Addressee: null } message:
                return Words(language, message.Content);
            case Message { Addressee: { } addressee } message:
                var called = Marked(language, addressee, Role.Addressee);
                var said = Words(language, message.Content);
                //TODO its finite verb is the message's, but a suffix marking it still goes on its last word
                return new(message.Content.Value is Fragment { Value: Interjection or Answer } ? [.. said.Words, .. called] : [.. called, .. said.Words], null);
            default:
                throw new ArgumentOutOfRangeException(nameof(utterance), utterance, null);
        }
    }

    private static ClauseWords Words(Language language, Content content) => content switch
    {
        Sentence statement => Words(language, statement),
        Fragment fragment => new(Words(language, fragment), null),
    };

    /*
      what a fragment says: a phrase as a noun is cited, an oblique marked as its role is in a
      clause, or an interjection or an answer, each a word of its own. It has no verb, so a marker of
      the whole goes on its last word. A heading is said only as a sentence of its own, since its
      label is said apart
     */
    private static IReadOnlyList<Word> Words(Language language, Fragment fragment) => fragment switch
    {
        Standalone standalone => Cited(language, standalone.Argument),
        Oblique oblique => Oblique(language, oblique),
        Interjection interjection => [new Word([language.Lexicon.Interjection(interjection.Meaning).Root])],
        Answer answer => [new Word([language.Lexicon.Interjection(answer.Meaning).Root])],
        Heading => throw new ArgumentException("A heading is said only as a sentence of its own.", nameof(fragment)),
    };

    /// <summary>
    /// A clause's words, and the root of its finite verb where one is said, for what goes on that
    /// verb: a question's affix, the verb a question moves, or a suffix marking the whole clause.
    /// </summary>
    private record ClauseWords(IReadOnlyList<Word> Words, Root? Finite);

    //a statement's words, before they're sounded out, and its finite verb, so that a statement
    //inside another can be marked as one
    private static ClauseWords Words(Language language, Sentence statement) => statement switch
    {
        Clause c => Words(language, c),
        Coordinated<Sentence> c => Words(language, c),
        Subordinated s => Words(language, s),
    };

    public static Rendering Realize(Language language, Clause clause, string translation) => Rendering(language, Words(language, clause).Words, translation);

    //a clause's words: a proposition stated or asked, or a state of affairs someone's told to bring about
    private static ClauseWords Words(Language language, Clause clause) => clause switch
    {
        Declarative stated => Words(language, stated.Propositional, []),
        Interrogative asked => Words(language, asked),
        Directive told => JoinedUnder(told.StateOfAffairs) is var (aspects, joined)
            ? Joined(language, joined, aspects, null)
            : Words(language, Collected(told.StateOfAffairs, null), new Balanced(null, null, [], false)),
    };

    /// <summary>Who or what a statement is about: its first clause's subject, which is 'you' for a command.</summary>
    public static Argument SubjectOf(Sentence statement) => statement switch
    {
        Clause c => c.Event.Subject,
        Coordinated<Sentence> c => SubjectOf(c.First),
        Subordinated s => SubjectOf(s.Main),
    };

    /*
      a clause as its verb says it: its event, and what the layers around it have the verb say,
      collected onto it: the tense, where it states something, its aspect, its negation, and a
      situational modal with the modal's own negation. A directive has no tense, and its subject is
      who's told. Syntax says only what can be collected here, so layers stacked beyond what's
      built, as two aspects, a modal over a modal, or the perfect or progressive with a modal, are
      refused as they're collected. An aspect is said the same on either side of a modal, and a
      negation the same on either side of an aspect, since nothing here says those scopes apart yet
     */
    private record Spine(Event Event, Tense? Tense, Aspect Aspect, bool Negative, SituationalModality? Modal, bool ModalNegative);

    //what a state of affairs has its verb say, at this tense, or none for a directive
    private static Spine Collected(StateOfAffairs layers, Tense? tense)
    {
        List<object> operators = [];
        var at = layers;
        while (at.Value is not Event)
        {
            switch (at)
            {
                case Negated negated:
                    operators.Add(negated);
                    at = negated.StateOfAffairs;
                    break;
                case Modalized modalized:
                    operators.Add(modalized);
                    at = modalized.StateOfAffairs;
                    break;
                case Aspected aspected:
                    operators.Add(aspected);
                    at = aspected.StateOfAffairs;
                    break;
                case Coordinated<StateOfAffairs>:
                    throw new ArgumentException("Joined states of affairs are said one at a time, as Joined says them.", nameof(layers));
                default:
                    throw new ArgumentOutOfRangeException(nameof(layers), at, null);
            }
        }
        var modals = operators.OfType<Modalized>().ToList();
        var aspects = operators.OfType<Aspected>().ToList();
        if (modals.Count > 1 || aspects.Count > 1)
        {
            throw new ArgumentException("A verb says one modal and one aspect yet.", nameof(layers));
        }
        var modalAt = modals.Count == 1 ? operators.IndexOf(modals[0]) : operators.Count;
        var outside = operators.Take(modalAt).OfType<Negated>().Count();
        var inside = operators.Skip(modalAt).OfType<Negated>().Count();
        if (outside > 1 || inside > 1 || modals.Count == 0 && outside + inside > 1)
        {
            throw new ArgumentException("A negation over a negation can't be said yet.", nameof(layers));
        }
        var aspect = aspects.Count == 1 ? aspects[0].Aspect : Aspect.Imperfective;
        if (modals.Count == 1 && (aspect == Aspect.Perfect || aspect == Aspect.Progressive))
        {
            throw new ArgumentException($"A situational modal can't be said with {aspect.Gloss} yet.", nameof(layers));
        }
        var modal = modals.Count == 1 ? modals[0].Modality : null;
        var @event = (Event)at.Value!;
        if (tense is null && (modal is not null || aspects.Count == 1 || @event.Subject.Value is not Pronoun told || told.Person != Person.Second))
        {
            throw new ArgumentException("A directive can't take a modal or an aspect yet, and is said to 'you'.", nameof(layers));
        }
        return modals.Count == 1
            ? new Spine(@event, tense, aspect, inside == 1, modal, outside == 1)
            : new Spine(@event, tense, aspect, outside == 1, null, false);
    }

    //a proposition with its judgments, and each event in it what this makes of it
    private static Propositional Rebuilt(Propositional propositional, Func<Event, Event> rebuild) =>
        propositional.Rebuilt(p => p with { StateOfAffairs = p.StateOfAffairs.Rebuilt(rebuild) });

    //whether a state of affairs joins events, anywhere in it
    private static bool JoinsEvents(StateOfAffairs layers) => layers.Events.Skip(1).Any();

    /*
      what's joined at the top of a state of affairs, under the aspects over it all, outermost
      first, or null where nothing's joined there. A negation or a modal over joined states of
      affairs can't be said yet, since saying it on each changes what it says: 'can't eat and
      drink', 'not both', isn't 'can't eat and can't drink'
     */
    private static (IReadOnlyList<Aspect> Aspects, Coordinated<StateOfAffairs> Joined)? JoinedUnder(StateOfAffairs layers)
    {
        List<Aspect> aspects = [];
        var at = layers;
        while (true)
        {
            switch (at)
            {
                case Coordinated<StateOfAffairs> joined:
                    return (aspects, joined);
                case Aspected aspected:
                    aspects.Add(aspected.Aspect);
                    at = aspected.StateOfAffairs;
                    break;
                case Negated or Modalized:
                    return JoinsEvents(at)
                        ? throw new ArgumentException("A negation or a modal over joined states of affairs can't be said yet.", nameof(layers))
                        : null;
                case Event:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layers), at, null);
            }
        }
    }

    //the states of affairs joined, each with the conjunction before it, none for the first
    private static IEnumerable<(StateOfAffairs Conjunct, Conjunction? Before)> Conjuncts(StateOfAffairs layers, Conjunction? before) =>
        layers.Value is Coordinated<StateOfAffairs> joined
            ? [.. Conjuncts(joined.First, before), .. Conjuncts(joined.Second, joined.Conjunction)]
            : [(layers, before)];

    /*
      states of affairs joined under one tense and sentence type, each said as a clause of its own
      at that tense, or told, with the aspects over them all on each, as 'had eaten and drunk' has
      its perfect on both, and the language's word for the conjunction between, as clauses are
      joined. A subject the one before it has isn't said again, as in 'she turns and walks away'.
      TODO languages that chain clauses, as Turkic and Papuan ones do, say the tense only on the
      last verb, and many mark whether the next subject is the same, as switch reference does;
      others serialize the verbs, as Yoruba and Mandarin do
     */
    private static ClauseWords Joined(Language language, Coordinated<StateOfAffairs> joined, IReadOnlyList<Aspect> aspects, Tense? tense)
    {
        List<Word> words = [];
        Argument? previous = null;
        foreach (var (conjunct, before) in Conjuncts(joined, null))
        {
            var layered = Enumerable.Reverse(aspects).Aggregate(conjunct, (inner, aspect) => new Aspected(aspect, inner));
            var spine = Collected(layered, tense);
            var drop = previous is { } said && said.Equals(spine.Event.Subject);
            if (before is { } conjunction && language.Conjunctions.BetweenClauses(conjunction) is { } between)
            {
                words.Add(new Word([between.Root]));
            }
            words.AddRange(tense is null
                ? Words(language, spine, new Balanced(null, null, [], drop)).Words
                : Words(language, Fold(language, spine, ModalOf(language, spine)), [], drop).Words);
            previous = spine.Event.Subject;
        }
        //TODO each has a finite verb of its own, but a suffix marking them together still goes on the last word
        return new(words, null);
    }

    //a spine's situational modal, as the language marks it
    private static Modal? ModalOf(Language language, Spine spine) =>
        spine.Modal is { } modality ? new Modal(language.Modals.Markers[modality], spine.ModalNegative) : null;

    //a clause with each event in it what this makes of it
    private static Clause Rebuilt(Clause clause, Func<Event, Event> rebuild) => clause switch
    {
        Declarative stated => stated with { Propositional = Rebuilt(stated.Propositional, rebuild) },
        Interrogative asked => asked with { Propositional = Rebuilt(asked.Propositional, rebuild) },
        Directive told => told with { StateOfAffairs = told.StateOfAffairs.Rebuilt(rebuild) },
    };

    public static Rendering Realize(Language language, Subordinated subordinated, string translation) => Rendering(language, Words(language, subordinated).Words, translation);

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
        var marking = language.Subordination.Markers[subordinated.Clause.Subordinator];
        var main = SubjectOf(subordinated.Main);
        var dependent = Stated(subordinated.Clause);
        var shares = SubjectOf(dependent).Value is Shared;
        var shared = shares || SubjectOf(dependent).Equals(main);
        var derank = marking.Balance == Balance.Deranked || marking.Balance == Balance.Either && shared;
        var deranked = derank ? AsDeranked(language, shares ? WithSubject(dependent, main) : dependent, shared, marking.MarkedAs) : null;
        var clause = deranked ?? Words(language, shares ? WithSubject(dependent, Pronominal(language, main)) : dependent);
        var marked = deranked is not null && marking.MarkedAs is not null
            ? deranked.Words
            : Subordinate(clause, marking.Marker, marking.Position);
        var said = Words(language, subordinated.Main).Words;
        //TODO its finite verb is its main clause's, but a suffix marking it still goes on its last word
        return new(marking.BeforeMain ? [.. marked, .. said] : [.. said, .. marked], null);
    }

    /*
      a dependent clause said as a clause that states it, which is how Syntax says one, at its own
      time where it has one, and at the present where its time is set by the clause it's in
      TODO the main clause's time, or a language's own way of saying a clause with none, would be truer
      than the present
     */
    private static Sentence Stated(AdverbialClause clause) => clause switch
    {
        Purpose purpose => (Clause)new Declarative(new Proposition(purpose.StateOfAffairs, Tense.Present)),
        Circumstance circumstance => (Clause)new Declarative(circumstance.Propositional),
    };

    private static Sentence Stated(Complement complement) => complement switch
    {
        Said said => (Clause)new Declarative(said.Propositional),
        Wanted wanted => (Clause)new Declarative(new Proposition(wanted.StateOfAffairs, Tense.Present)),
    };

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

    /*
      a noun phrase whose adjective is compared with a standard, in a language that compares by
      exceeding or with two clauses, which are clause constructions: the phrase without the adjective,
      with a relative clause in its place. Exceeding, the relative clause compares, as in 'arms that
      are strong, exceed yours'; with two clauses, it says the adjective plainly, and the clause
      denying it of the standard follows the phrase, as in 'arms that are strong, yours are not
      strong'. Both are at the present, since a noun phrase has no time of its own, which the clause
      it's in would say more truly. Null for any other phrase
     */
    private static (NounPhrase Phrase, Sentence? Denial)? ComparedByClause(Language language, NounPhrase phrase)
    {
        bool ByClause(Adjective adjective) => Standards(adjective).Any(s => language.Comparison.Value is ExceedComparison || InTwoClauses(language, s));
        if (phrase.Head.Value is not Noun noun || noun.Adjectives.ToList().FindIndex(ByClause) is var at && at < 0)
        {
            return null;
        }
        if (phrase.Relative is not null || noun.Adjectives.Skip(at + 1).Any(a => a.Grade is { Value: Comparative { Standard: not null } }))
        {
            throw new ArgumentException("A noun phrase that compares by a relative clause can't have another relative clause or comparison yet.", nameof(phrase));
        }
        var compared = noun.Adjectives[at];
        var standard = Standards(compared).Single();
        var conjoined = InTwoClauses(language, standard);
        var relative = new Proposition(new Event(new Being(conjoined ? Plain(compared) : compared), new Gap(null), [], []), Tense.Present);
        Sentence? denial = conjoined ? Denial(Plain(compared), standard) : (Sentence?)null;
        return (new NounPhrase(noun with { Adjectives = [.. noun.Adjectives.Where((_, i) => i != at)] }, relative), denial);
    }

    //a noun phrase with its relative clause, if it has one, before or after it all
    private static IReadOnlyList<Word> Relativized(Language language, NounPhrase phrase, IReadOnlyList<Word> words)
    {
        if (phrase.Relative is not { } clause)
        {
            return words;
        }
        var relative = RelativeClause(language, (Clause)new Declarative(clause), phrase.Head);
        return language.Relativization.BeforeNoun ? [.. relative, .. words] : [.. words, .. relative];
    }

    /*
      a relative clause, with its noun said in the gap as the language says the gap's role: not at
      all, as an ordinary pronoun, or as a relative pronoun, which goes first in the clause with the
      rest of its phrase, as German den in der Mann, den ich sah. A relativizer that's an affix goes
      on the clause's verb, which it leaves bare of tense and agreement, as a participle is, and one
      that's a word goes at the clause's edge beside its noun
     */
    private static IReadOnlyList<Word> RelativeClause(Language language, Sentence clause, Nominal head)
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
    private static (Role Role, Oblique? Oblique) GapSlot(Language language, Sentence clause) =>
        Roles(language, clause).FirstOrDefault(f => f.Argument.Value is Gap) is { } gap
            ? (gap.Role, gap.Oblique)
            : throw new ArgumentException("A relative clause needs a gap in one of its roles.", nameof(clause));

    //a relative clause with this in its gap's place
    private static Sentence Filled(Sentence clause, Argument filler)
    {
        Argument Fill(Argument argument) => argument.Value is Gap ? filler : argument;
        return clause.Value is Clause c ? Rebuilt(c, e => e.Refilled(Fill)) : clause;
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
        return role is Role.Location or Role.Goal or Role.Source ? words[InterrogativeWord.Where]
            : head.Value is IndefinitePronoun { Kind: Ontology.Person } || language.Lexicon.ClassOf(head.Meaning) is SemanticClass.Male or SemanticClass.Female or SemanticClass.Human
                ? words[InterrogativeWord.Who]
            : words[InterrogativeWord.What];
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
            case Complement complement:
            {
                var (words, balanced) = ComplementWords(language, complement, subject);
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
    private static (IReadOnlyList<Word> Words, bool Balanced) ComplementWords(Language language, Complement complement, Argument? subject)
    {
        var marking = language.Complementation.MarkingOf(complement.Kind);
        var dependent = Stated(complement);
        var shares = SubjectOf(dependent).Value is Shared;
        Argument Main() => subject ?? throw new ArgumentException("A complement that shares its subject needs the main clause's.", nameof(subject));
        var shared = shares || subject is { } main && SubjectOf(dependent).Equals(main);
        var derank = marking.Balance == Balance.Deranked || marking.Balance == Balance.Either && shared;
        var deranked = derank ? AsDeranked(language, shares ? WithSubject(dependent, Main()) : dependent, shared, marking.MarkedAs) : null;
        if (deranked is not null && marking.MarkedAs is not null)
        {
            return (deranked.Words, false);
        }
        var clause = deranked ?? Words(language, shares ? WithSubject(dependent, Pronominal(language, Main())) : dependent);
        var marked = marking.Marker is { } complementizer ? Subordinate(clause, complementizer.Marker, complementizer.Position) : clause.Words;
        return (marked, deranked is null);
    }

    //what was said, as it was said, with the language's quotative, if it has one
    private static IReadOnlyList<Word> Quoted(Language language, Quotation quotation)
    {
        var said = Words(language, quotation.Utterance);
        return language.Complementation.QuotativeMarker is var (marker, position) ? Subordinate(said, marker, position) : said.Words;
    }

    /*
      what's wanted, with 'want' as a marker on its verb, where the language has no verb for 'want': it
      takes the wanting clause's tense, aspect and subject, its modifiers too, and its negation as
      the marker's, as Japanese tabe-taku-nai 'eat-DESID-NEG' is 'doesn't want to eat'. Null where
      'want' is a verb, or what's wanted isn't an event its subject would do, or can't take a marker
      on its verb, as a comparison or a clause with a modal can't
     */
    private static (Spine Wanted, Modal Want)? Desired(Language language, Spine spine) =>
        Embedded(spine) is ("want", var wanted) && language.Complementation.DesiderativeMarking is { } marking
            ? (wanted, new Modal(marking, spine.Negative))
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
    private static (Spine Begun, AspectMarking Phase)? Phased(Language language, Spine spine) =>
        Embedded(spine) is (var verb, { Event.Predicate.Value: Doing or Done, Negative: false } begun)
            && Aspects.PhaseOf(verb) is { } phase && language.Aspects.MarkingOf(phase) is { } marking
            && Phased(language, begun) is null && Desired(language, begun) is null
            ? (begun with { Negative = spine.Negative }, marking)
            : null;

    /*
      what a clause's verb takes as an event its own subject does, as 'want' and 'begin' take one,
      with that verb: the event as a clause that states something, timed as this one is, in its tense
      and aspect and with its subject, and with this one's modifiers after its own. Null for a clause
      that takes no such event, or one whose event has a modal of its own
     */
    private static (string Verb, Spine Event)? Embedded(Spine spine)
    {
        if (spine is not { Event.Predicate.Value: Doing { Object: { Value: Complement { Value: Wanted embedded } } } doing, Tense: { } tense }
            || embedded.StateOfAffairs.Event.Subject is var own && !(own.Value is Shared || own.Equals(spine.Event.Subject))
            || JoinsEvents(embedded.StateOfAffairs))
        {
            return null;
        }
        var wanted = Collected(embedded.StateOfAffairs, tense);
        if (wanted.Modal is not null)
        {
            return null;
        }
        return (doing.Verb, wanted with
        {
            Aspect = spine.Aspect,
            Event = wanted.Event with
            {
                Subject = spine.Event.Subject,
                Obliques = [.. wanted.Event.Obliques, .. spine.Event.Obliques],
                Adverbs = [.. wanted.Event.Adverbs, .. spine.Event.Adverbs],
            },
        });
    }

    //whether a clause's copula can be dropped: not with a modal or an aspect's marker, which go with
    //it, and only in the tense its verb is said in, as a perfect the language lacks is a past
    private static bool DropsCopula(Language language, Tense tense, Aspect aspect, bool negative, Modal? modal, bool locational)
    {
        var (said, _, marking) = language.Aspects.Said(tense, aspect);
        return modal is null && marking is null && language.Predication.Drops(said, negative, locational);
    }

    /*
      a verb's clause with its verb bare of tense and agreement, keeping negation and the passive,
      and without its subject where it shares the main clause's. Marked like a role, its verb takes
      that role's case, or the whole clause its adposition. Null for any other kind of clause,
      including one with a modal, which stays balanced. One that begins or stops something, where
      the language marks that on the verb, is what's begun with its inceptive or cessative
     */

    private static ClauseWords? AsDeranked(Language language, Sentence statement, bool dropSubject, Role? markedAs)
    {
        if (statement.Value is not Clause { Value: Declarative { Propositional.Value: Proposition proposition } } || JoinsEvents(proposition.StateOfAffairs))
        {
            return null;
        }
        var spine = Collected(proposition.StateOfAffairs, proposition.Tense);
        if (spine.Modal is not null)
        {
            return null;
        }
        var folded = FoldPhase(language, spine, null);
        var voiced = Voiced(language, folded.Clause);
        //a clause that compares its manner with two clauses says both, and keeps its verb
        if (voiced is not { Event.Predicate.Value: Doing or Done }
            || voiced.Event.Adverbs.Any(a => InTwoClauses(language, a)))
        {
            return null;
        }
        //a passive said as the active has the agent as its subject, which isn't the main clause's
        var dropped = dropSubject && voiced == folded.Clause;
        var marker = markedAs is { } role ? language.RoleMarkers[role] : null;
        return Words(language, voiced, new Deranked(folded.Phase, dropped, marker));
    }

    //a passive in a language without one is said as the active
    private static Spine Voiced(Language language, Spine spine) =>
        spine is { Event.Predicate.Value: Done done, Tense: not null } && language.Passive is null ? Active(spine, done) : spine;

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

    //a command's subject is always 'you'
    private static Sentence WithSubject(Sentence statement, Argument subject) => statement.Value switch
    {
        Clause { Value: Directive } => statement,
        Clause c => Rebuilt(c, e => e with { Subject = subject }),
        _ => statement,
    };

    //each statement as it would be said alone, with the language's word for the conjunction between,
    //or nothing where it sets clauses side by side
    public static Rendering Realize(Language language, Coordinated<Sentence> coordinated, string translation) => Rendering(language, Words(language, coordinated).Words, translation);

    private static ClauseWords Words(Language language, Coordinated<Sentence> coordinated)
    {
        IReadOnlyList<Word> between = language.Conjunctions.BetweenClauses(coordinated.Conjunction) is { } word ? [new Word([word.Root])] : [];
        //TODO each clause has a finite verb of its own, but a suffix marking the two together still goes on the last word
        return new([.. Words(language, coordinated.First).Words, .. between, .. Words(language, coordinated.Second).Words], null);
    }

    //parts said one after another, with a word between each two, if there is one
    private static IReadOnlyList<Word> Join(IEnumerable<IReadOnlyList<Word>> parts, Lexeme? between) =>
        [.. parts.SelectMany((part, i) => i == 0 || between is null ? part : [new Word([between.Root]), .. part])];

    //an adjective joined to others, which can be compared but can't take a standard of its own yet
    private static Adjective Unstandarded(Adjective adjective) => adjective.Grade is { Value: Comparative { Standard: not null } }
        ? throw new ArgumentException("Adjectives joined can't be compared with standards of their own yet.", nameof(adjective))
        : adjective;

    //a predicate's adjectives and how they're joined: one alone, or several said of the subject
    //together, joined as clauses are, since a predicate is what a clause says
    private static (IReadOnlyList<Adjective> Adjectives, Lexeme? Between)? PredicateAdjectives(Language language, Attribute attribute) => attribute.Value switch
    {
        Adjective adjective => ([adjective], null),
        AdjectiveCoordination together => ([.. together.Adjectives.Select(Unstandarded)], language.Conjunctions.BetweenClauses(together.Conjunction)),
        _ => null,
    };

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
    private record Folded(Spine Clause, Modal? Modal, AspectMarking? Phase);

    private static Folded Fold(Language language, Spine spine, Modal? modal) =>
        modal is null && Desired(language, spine) is var (wanted, want) ? FoldPhase(language, wanted, want) : FoldPhase(language, spine, modal);

    private static Folded FoldPhase(Language language, Spine spine, Modal? modal) =>
        Phased(language, spine) is var (begun, phase) ? new(begun, modal, phase) : new(spine, modal, null);

    //a clause that states something, as it's folded, with words said where its obliques go, before them
    private static ClauseWords Words(Language language, Folded folded, IReadOnlyList<Word> adverbs, bool dropSubject)
    {
        var spine = Voiced(language, folded.Clause);
        return spine.Tense is not null
            ? Words(language, spine, new Balanced(folded.Modal, folded.Phase, adverbs, dropSubject))
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
    //a balanced clause leaves out its subject where the one joined before it has said it
    private record Balanced(Modal? Modal, AspectMarking? Phase, IReadOnlyList<Word> Adverbs, bool DropSubject);
    private record Deranked(AspectMarking? Phase, bool DropSubject, RoleMarker? Marker);

    /*
      a clause said at its rank, with its predicate's own words. Only a statement is deranked, or
      takes a modal or a phase, and a passive one is said only in a language with a passive
     */
    private static ClauseWords Words(Language language, Spine spine, Rank rank)
    {
        if (spine.Tense is null && rank.Value is not Balanced { Modal: null, Phase: null })
        {
            throw new ArgumentException("A command is said balanced, with no modal or phase.", nameof(rank));
        }
        if (ConjoinedManner(language, spine, rank) is { } conjoined)
        {
            return conjoined;
        }
        return spine.Event.Predicate switch
        {
            Doing doing => Verbal(language, spine, language.Lexicon.Verb(doing.Verb), doing.Object, null, null, rank),
            Done when spine.Tense is null => throw new ArgumentException("A command can't be passive yet.", nameof(spine)),
            Done done => Verbal(language, spine, language.Lexicon.Verb(done.Verb), null, done.Agent,
                language.Passive ?? throw new ArgumentException("A language without a passive says it as the active.", nameof(spine)), rank),
            Being { Attribute.Value: Adjective { Grade.Value: Comparative compared } } being => ComparativeWords(language, spine, being, compared, rank),
            Being being => Copular(language, spine, being, rank, []),
        };
    }

    //a clause's subject's words, unsaid in a command, even in a language that says its pronouns, as in English, and in a deranked clause that shares its subject
    private static IReadOnlyList<Word> SubjectWords(Language language, Spine spine, Rank rank, Role role) =>
        spine.Tense is null || rank.Value is Deranked { DropSubject: true } or Balanced { DropSubject: true } ? [] : Subject(language, spine.Event.Subject, role);

    //the words said where a statement's obliques go, before them, as a question's 'why' or 'how' is
    private static IReadOnlyList<Word> AdverbsOf(Rank rank) => rank.Value is Balanced balanced ? balanced.Adverbs : [];

    /*
      a verb as a clause says it at its rank, with its passive, negation and modal, and its marker of
      beginning or ceasing. A deranked verb has no auxiliary to take its place, so it's the clause's
      verb. A command's is said to the number its subject, 'you', has
     */
    private static VerbComplex Complex(Language language, Lexeme verb, Spine spine, Rank rank, bool negative, Modal? modal, Passive? passive) => rank switch
    {
        Balanced balanced => spine.Tense is { } tense
            ? Complex(language, verb, tense, spine.Aspect, spine.Event.Subject, negative, modal, passive, balanced.Phase)
            : Commanded(language, verb, ((Pronoun)spine.Event.Subject.Value!).Number, negative),
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
    private static ClauseWords Verbal(Language language, Spine spine, Lexeme verb, Argument? @object, Argument? agent, Passive? passive, Rank rank)
    {
        var @event = spine.Event;
        var subject = @event.Subject;
        var (negative, modal) = Concord(language, spine.Negative, (rank.Value as Balanced)?.Modal, subject, @object, [.. Given(agent), .. ObliqueArguments(@event.Obliques)]);
        var complex = Complex(language, verb, spine, rank, negative, modal, passive);
        var (inPlace, last) = Object(language, @object, subject);
        IReadOnlyList<Word> agentWords = agent.HasValue ? Marked(language, agent.GetValueOrDefault(), Role.Agent) : [];
        IReadOnlyList<Word> words = [.. Flat(Order(language.WordOrder, SubjectWords(language, spine, rank, @object is null ? Role.IntransitiveSubject : Role.TransitiveSubject),
            Linearized(language, complex), inPlace,
            WhereObliquesGo(language, [(Closeness.Reason, AdverbsOf(rank)), (Closeness.Complement, agentWords), .. Adverbials(language, @event.Adverbs, @event.Obliques)]))), .. last];
        return new(rank.Value is Deranked { Marker: { } marker } ? WithAdposition(words, marker.Adposition) : words, complex.Outermost.Root);
    }

    //a passive said as the active, with its agent as the subject, or 'they' where nobody says who
    private static Spine Active(Spine spine, Done done) => spine with
    {
        Event = spine.Event with
        {
            Predicate = new Doing(done.Verb, spine.Event.Subject),
            Subject = done.Agent.HasValue ? done.Agent.GetValueOrDefault() : new Pronoun(Person.Third, Number.Plural),
        },
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
            //a standard's manner is left out where it's the comparison's, as in 'than they started before'
            case Manner { Adjective.Grade.Value: DegreeGap }:
                return [];
            case Manner manner:
            {
                var root = language.Lexicon.Adjective(manner.Adjective.Meaning).Root;
                IReadOnlyList<Word> adjective = [new Word([root])];
                var words = language.Manner is { } marker ? marker.Place(adjective, 0, Reach.Root) : adjective;
                return WithStandards(language, Graded(language, words, root, manner.Adjective), Standards(manner.Adjective));
            }
            //a question asks with it, and moves it where questions move their words
            case QuestionAdverbial asked:
                return [new Word([language.Questions.Adverbs[asked.Word].Root])];
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
        return language.Similative switch
        {
            SimilativeWord word => word.BeforeStandard ? [new Word([word.Like.Root]), .. phrase] : [.. phrase, new Word([word.Like.Root])],
            SimilativeAffix affix => ((Exponent)affix.Marker).Place(phrase, affix.Marker.Side == Side.Before ? 0 : phrase.Count - 1, Reach.Edge),
        };
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
    private static ClauseWords Copular(Language language, Spine spine, Being being, Rank rank, IReadOnlyList<Word> standard)
    {
        if (rank.Value is not Balanced { Phase: null } balanced)
        {
            throw new ArgumentException("Only a clause with a verb is deranked or said with a phase.", nameof(rank));
        }
        var predication = language.Predication;
        var @event = spine.Event;
        var subject = @event.Subject;
        IEnumerable<Argument> placed = being.Attribute.Value is Oblique at ? [at.Argument] : [];
        var (negative, modal) = Concord(language, spine.Negative, balanced.Modal, subject, null, [.. placed, .. ObliqueArguments(@event.Obliques)]);
        var subjectWords = SubjectWords(language, spine, rank, Role.IntransitiveSubject);
        var beforeAdjective = StandardBeforeAdjective(language);
        IReadOnlyList<(Closeness Rank, IReadOnlyList<Word> Words)> modifiers =
            [(Closeness.Reason, balanced.Adverbs), .. Adverbials(language, @event.Adverbs, @event.Obliques), (Closeness.Complement, beforeAdjective ? [] : standard)];
        IReadOnlyList<Word> Standing(IReadOnlyList<Word> adjective) => beforeAdjective ? [.. standard, .. adjective] : adjective;
        VerbComplex Be(Lexeme copula) => Complex(language, copula, spine, rank, negative, modal, null);
        bool Drops(bool locational) => spine.Tense is { } tense && DropsCopula(language, tense, spine.Aspect, negative, modal, locational);
        if (PredicateAdjectives(language, being.Attribute) is var (adjectives, between) && predication.Adjectives == PredicativeAdjectives.Verbal)
        {
            //a property word inflects as a verb in a language whose adjectives are verbs, and the last one's is the finite verb
            var lexemes = adjectives.Select(a => language.Lexicon.Adjective(a.Meaning)).ToList();
            var verbs = lexemes.Select(lexeme => Be(lexeme with { PartOfSpeech = PartOfSpeech.Verb })).ToList();
            var predicate = Join(adjectives.Select((a, i) => Graded(language, Linearized(language, verbs[i]), lexemes[i].Root, a)), between);
            return new(Flat(Order(language.WordOrder, subjectWords, Standing(predicate), null, WhereObliquesGo(language, modifiers))), verbs[^1].Outermost.Root);
        }

        //a location is an oblique, and goes where obliques go, with the language's locational copula
        if (being.Attribute.Value is Oblique location)
        {
            if (location.Role != Role.Location)
            {
                throw new ArgumentException($"A copular clause can say where its subject is, but not its {location.Role}.", nameof(being));
            }
            if (standard.Count > 0)
            {
                throw new ArgumentException("Only an adjective can be compared.", nameof(being));
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
        var complement = being.Attribute.Value is Adjective adjective
            ? Standing(PredicateComplement(language, adjective, subject))
            : PredicateComplement(language, being.Attribute, subject);
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
    private static ClauseWords ComparativeWords(Language language, Spine spine, Being being, Comparative compared, Rank rank)
    {
        if (spine.Tense is null || rank.Value is not Balanced { Modal: null })
        {
            throw new ArgumentException("Only a clause that states something compares, with no modal yet.", nameof(spine));
        }
        if (!compared.Standard.HasValue)
        {
            return Copular(language, spine, being, rank, []);
        }
        var standard = compared.Standard.GetValueOrDefault();
        if (!InTwoClauses(language, standard))
        {
            return Copular(language, spine, being, rank, StandardWords(language, standard));
        }
        var plain = Plain((Adjective)being.Attribute.Value!);
        var first = Copular(language, spine, being with { Attribute = plain }, rank, []);
        return Conjoined(first, standard switch
        {
            Argument phrase => Copular(language, spine with { Event = spine.Event with { Subject = phrase, Obliques = [], Adverbs = [] }, Negative = !spine.Negative },
                being with { Attribute = plain }, new Balanced(null, null, [], false), []),
            Propositional => Words(language, Denial(plain, standard)),
        });
    }

    //the two clauses of a conjoined comparison. Both say the same verb, so either's will do, where the first dropped its copula and the second didn't
    private static ClauseWords Conjoined(ClauseWords first, ClauseWords second) => new([.. first.Words, .. second.Words], first.Finite ?? second.Finite);

    //an adjective as each clause of a conjoined comparison says it, plainly, with the 'very' that says 'much' if it has one
    private static Adjective Plain(Adjective adjective) =>
        new(adjective.Meaning, adjective.Grade is { Value: Comparative { Differential.Value: Much } } ? new Intensified() : (Grade?)null);

    /*
      a clause whose manner is compared with a standard, in a language that compares with two
      clauses: the clause with its manner said plainly, then the clause again of the standard,
      denying it, with only that manner, as in 'the dog walks fast, the cat doesn't walk fast'. It's
      said balanced and states something, with no modal or phase, as be's comparison is. Null for any
      other clause
     */
    private static ClauseWords? ConjoinedManner(Language language, Spine spine, Rank rank)
    {
        var adverbs = spine.Event.Adverbs;
        var at = adverbs.ToList().FindIndex(a => InTwoClauses(language, a));
        if (at < 0)
        {
            return null;
        }
        if (spine.Tense is null || spine.Negative || rank.Value is not Balanced { Modal: null, Phase: null })
        {
            throw new ArgumentException("A conjoined comparison of a manner is said only of a clause that states something, not negated and with no modal or phase yet.", nameof(spine));
        }
        var manner = ((Manner)adverbs[at].Value!).Adjective;
        var standard = Standards(manner).Single();
        Adverbial plain = new Manner(Plain(manner));
        var first = Words(language, spine with { Event = spine.Event with { Adverbs = [.. adverbs.Select((a, i) => i == at ? plain : a)] } }, rank);
        var second = standard switch
        {
            Argument phrase => Words(language, spine with { Event = spine.Event with { Subject = phrase, Obliques = [], Adverbs = [plain] }, Negative = true }, new Balanced(null, null, [], false)),
            Propositional => Words(language, Denial(Plain(manner), standard)),
        };
        return Conjoined(first, second);
    }

    //whether an adverb is a manner compared in two clauses, as InTwoClauses says
    private static bool InTwoClauses(Language language, Adverbial adverbial) =>
        adverbial.Value is Manner { Adjective.Grade.Value: Comparative { Standard: { } standard } } && InTwoClauses(language, standard);

    //'exceed' and its object, on the side of it the language puts objects
    private static IReadOnlyList<Word> Exceeding(Language language, ExceedComparison exceed, IReadOnlyList<Word> @object)
    {
        IReadOnlyList<Word> verb = [new Word([exceed.Exceed.Root])];
        return language.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial ? [.. verb, .. @object] : [.. @object, .. verb];
    }

    /*
      an adjective's words with its grade said, wherever the adjective is: 'very' beside it if it's
      intensified, and a comparative's marker where the language marks one, which is always with no
      standard, and with one only where the language marks it alongside a standard. A comparative by
      much takes the language's 'very' until a language has a word of its own for 'much', as Turkish
      çok is both. 'Very' goes on first, so that the comparative's marker is the nearer, as in 'much
      bigger'
     */
    private static IReadOnlyList<Word> Graded(Language language, IReadOnlyList<Word> words, Root root, Adjective adjective) => adjective.Grade switch
    {
        null => words,
        { Value: Intensified } => WithMarker(words, root, language.Intensifier),
        //a standard's own adjective, said where it's the verb, as other degrees of it are
        { Value: DegreeGap } => words,
        { Value: Comparative compared } => WithMarker(WithMarker(words, root, compared.Differential is { Value: Much } ? language.Intensifier : (Exponent?)null), root,
            compared.Standard.HasValue && !language.Degree.WithStandard ? null : (Exponent?)language.Degree.Marker),
    };

    //an adjective's words with a marker of degree, if it has one: an affix next to its root, inside
    //any agreement or tense, or a word beside it
    private static IReadOnlyList<Word> WithMarker(IReadOnlyList<Word> words, Root adjective, Exponent? degree) =>
        degree is { } marker ? marker.Place(words, words.ToList().FindIndex(w => w.Root == adjective), Reach.Root) : words;

    /*
      a standard of comparison beside what's compared, marked as the language marks one: in its
      role's case or adposition, in the plain case a subject takes with the particle on its side, or
      as the object of 'exceed'. A clause as the standard is balanced after a particle, as English
      'than it should be' and Russian čem have it, and deranked in the standard's role or as the
      object of 'exceed', as Turkish bekle-diğ-im-den 'expect-NMLZ-1SG-ABL' is 'than I expected'. A
      comparison said in two clauses has its standard in a clause of its own, which InTwoClauses
      says
     */
    private static IReadOnlyList<Word> StandardWords(Language language, Standard standard) => standard switch
    {
        Argument phrase => language.Comparison switch
        {
            LocationalComparison locational => Marked(language, phrase, locational.Role),
            ParticleComparison particle => Than(particle, Inflect(language, phrase, Role.IntransitiveSubject)),
            ExceedComparison exceed => Exceeding(language, exceed, Inflect(language, phrase, Role.DirectObject)),
            ConjoinedComparison => throw new ArgumentException("A conjoined comparison's standard is said in a clause of its own.", nameof(standard)),
        },
        Propositional clause => language.Comparison switch
        {
            ParticleComparison particle => Than(particle, Words(language, (Clause)new Declarative(clause)).Words),
            LocationalComparison => DerankedWords(language, clause),
            ExceedComparison exceed => Exceeding(language, exceed, DerankedWords(language, clause)),
            ConjoinedComparison => throw new ArgumentException("A conjoined comparison's standard is said in a clause of its own.", nameof(standard)),
        },
    };

    //a clause as a standard is deranked where the language deranks it, which InTwoClauses checks first
    private static IReadOnlyList<Word> DerankedWords(Language language, Propositional clause) =>
        (DerankedStandard(language, clause) ?? throw new ArgumentException("A standard that can't be deranked is said in a clause of its own.", nameof(clause))).Words;

    //a clause as a standard, deranked in the standard's role or as the object of 'exceed', or null
    //where it can't be, as a copular clause or one with a modal can't be yet
    private static ClauseWords? DerankedStandard(Language language, Propositional clause) => language.Comparison switch
    {
        LocationalComparison locational => AsDeranked(language, (Clause)new Declarative(clause), dropSubject: false, markedAs: locational.Role),
        ExceedComparison => AsDeranked(language, (Clause)new Declarative(clause), dropSubject: false, markedAs: Role.DirectObject),
        ParticleComparison or ConjoinedComparison => null,
    };

    /*
      whether a comparison with this standard is said in two clauses: in a language that compares
      that way, or with a clause as the standard that the language would derank and can't, which
      every language can say in two clauses instead, as in 'it is warm, it should not be warm'
     */
    private static bool InTwoClauses(Language language, Standard standard) => language.Comparison switch
    {
        ConjoinedComparison => true,
        ParticleComparison => false,
        LocationalComparison or ExceedComparison => standard.Value is Propositional clause && DerankedStandard(language, clause) is null,
    };

    //the clause of a comparison said in two clauses that denies the property of its standard: a noun
    //phrase's, said of it, or a clause's own, with its degree said plainly
    private static Sentence Denial(Adjective plain, Standard standard) => standard switch
    {
        Argument phrase => (Clause)new Declarative(new Proposition(new Negated(new Event(new Being(plain), phrase, [], [])), Tense.Present)),
        Propositional clause => (Clause)new Declarative(clause.Rebuilt(p => p with { StateOfAffairs = new Negated(p.StateOfAffairs.Rebuilt(Ungapped)) })),
    };

    //an event with its degree gap said plainly, as a clause denying it says it
    private static Event Ungapped(Event @event) => @event with
    {
        Predicate = @event.Predicate.Value is Being { Attribute.Value: Adjective { Grade.Value: DegreeGap } gapped } being
            ? being with { Attribute = gapped with { Grade = null } }
            : @event.Predicate,
        Adverbs = [.. @event.Adverbs.Select(a => a.Value is Manner { Adjective: { Grade.Value: DegreeGap } gapped } ? new Manner(gapped with { Grade = null }) : a)],
    };

    //the standard an adjective's compared with, if it has one
    private static IEnumerable<Standard> Standards(Adjective adjective) => adjective.Grade is { Value: Comparative { Standard: { } standard } } ? [standard] : [];

    private static IReadOnlyList<Word> Than(ParticleComparison particle, IReadOnlyList<Word> standard) =>
        particle.BeforeStandard ? [new Word([particle.Than.Root]), .. standard] : [.. standard, new Word([particle.Than.Root])];

    //words with a standard's words on the side of them the language puts standards
    private static IReadOnlyList<Word> WithStandards(Language language, IReadOnlyList<Word> words, IEnumerable<Standard> standards)
    {
        var said = standards.SelectMany(s => StandardWords(language, s)).ToList();
        return said.Count == 0 ? words : StandardBeforeAdjective(language) ? [.. said, .. words] : [.. words, .. said];
    }

    //a predicate noun or adjective, said of this subject
    private static IReadOnlyList<Word> PredicateComplement(Language language, Attribute attribute, Argument subject) => attribute.Value switch
    {
        //a standard's adjective is left out where it's the comparison's, as in 'than it should be', unless it's the verb
        Adjective { Grade.Value: DegreeGap } => [],
        NounPhrase noun => Inflect(language, noun, Role.IntransitiveSubject),
        AdjectiveCoordination together => Join(together.Adjectives.Select(a => PredicateComplement(language, Unstandarded(a), subject)), language.Conjunctions.BetweenClauses(together.Conjunction)),
        Adjective adjective when language.AdjectiveAgreement == AdjectiveAgreement.Agreement =>
            Graded(language, [AgreeingAdjective(language, language.Lexicon.Adjective(adjective.Meaning),
                Features(language.Paradigms.Nouns, [AgreementOf(language, subject).Number, CaseFor(language, language.RoleMarkers[Role.IntransitiveSubject])]),
                GenderOf(language, subject))], language.Lexicon.Adjective(adjective.Meaning).Root, adjective),
        Adjective adjective => Graded(language, [new Word([language.Lexicon.Adjective(adjective.Meaning).Root])], language.Lexicon.Adjective(adjective.Meaning).Root, adjective),
        _ => throw new ArgumentException("A predicate is a noun or an adjective.", nameof(attribute)),
    };

    //a language that drops subject pronouns leaves the verb's agreement, if any, to say who
    private static IReadOnlyList<Word> Subject(Language language, Argument subject, Role role) =>
        subject.Value is Pronoun && language.Pronouns.DropsSubjects ? [] : Inflect(language, subject, role);

    /*
      a proposition's words, with words said where its obliques go, before them: its verb with its
      modal, or, where an epistemic modal is a verb, as the complement of that verb. An epistemic
      modal that isn't a verb is marked on the verb as a situational one is, and a verb takes only
      one or the other yet
     */
    private static ClauseWords Words(Language language, Propositional propositional, IReadOnlyList<Word> adverbs)
    {
        var (proposition, epistemic, denied) = Judgment(propositional);
        if (JoinedUnder(proposition.StateOfAffairs) is var (aspects, joined))
        {
            return epistemic is null && adverbs.Count == 0
                ? Joined(language, joined, aspects, proposition.Tense)
                : throw new ArgumentException("A judgment or a question over joined states of affairs can't be said yet.", nameof(propositional));
        }
        var spine = Collected(proposition.StateOfAffairs, proposition.Tense);
        if (epistemic is null)
        {
            return Words(language, Fold(language, spine, ModalOf(language, spine)), adverbs, false);
        }
        if (spine.Modal is not null)
        {
            throw new ArgumentException("A verb takes a situational modal or an epistemic one, not both, yet.", nameof(propositional));
        }
        var marking = language.Modals.Markers[epistemic];
        return marking.Value is ModalVerb verb
            ? Impersonal(language, verb, proposition, denied, adverbs)
            : Words(language, Fold(language, spine, new Modal(marking, denied)), adverbs, false);
    }

    //a proposition, its judgment, if it has one, and whether that's denied, which is as much as is said yet
    private static (Proposition Proposition, EpistemicModality? Epistemic, bool Denied) Judgment(Propositional propositional) => propositional switch
    {
        Proposition proposition => (proposition, null, false),
        Judged { Propositional.Value: Proposition proposition } judged => (proposition, judged.Modality, false),
        Denied { Propositional.Value: Judged { Propositional.Value: Proposition proposition } judged } => (proposition, judged.Modality, true),
        _ => throw new ArgumentException("A proposition is said with one judgment, denied or not, yet.", nameof(propositional)),
    };

    /*
      an epistemic modal verb is said impersonally, with what it judges as the 'that' clause of its
      verb, which keeps its own tense, as Greek borí na éfige 'it-may that he-left' and Hungarian
      lehet, hogy elment 'it may be that he left' do; the same verb said personally is the
      situational modal, as Greek boró na fíjo 'I can leave' is. The modal verb is present, since
      the judgment is the speaker's now, takes 'it' as its subject, which a language that drops its
      pronouns leaves unsaid, and the judgment's denial. Its complement is marked as a proposition,
      and goes last or where the object goes, as one does; a language that deranks propositions
      loses the clause's tense, as it does in any complement
     */
    private static ClauseWords Impersonal(Language language, ModalVerb modal, Proposition judged, bool denied, IReadOnlyList<Word> adverbs)
    {
        var it = new Pronoun(Person.Third, Number.Singular);
        var subject = Subject(language, it, Role.IntransitiveSubject);
        var verb = new VerbComplex(modal.Verb, [.. NegationLayers(language.Negation, denied)], new Finite(Tense.Present, Aspect.Imperfective, it));
        var (complement, balanced) = ComplementWords(language, new Said(judged), null);
        var last = balanced && language.Complementation.Last;
        return new([.. Flat(Order(language.WordOrder, subject, Linearized(language, verb), last ? null : complement, WhereObliquesGo(language, [(Closeness.Reason, adverbs)]))), .. last ? complement : []],
            verb.Outermost.Root);
    }

    //how a modal or 'want' is marked on a verb, and whether it's negated
    private record Modal(ModalMarking Marking, bool Negative);

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
    private static ClauseWords Words(Language language, Interrogative question)
    {
        var questions = language.Questions;
        var adverbial = question.Propositional.Proposition.StateOfAffairs.Event.Adverbs.Select(a => a.Value).OfType<QuestionAdverbial>().FirstOrDefault();
        var stated = Rebuilt(question.Propositional, e => e with { Adverbs = [.. e.Adverbs.Where(a => a.Value is not QuestionAdverbial)] });
        var said = Words(language, stated, []);
        var finite = said.Finite;
        var asked = Asked(language, stated.Proposition);
        if (asked is null && adverbial is null)
        {
            return new(Polar(language, said.Words, finite), finite);
        }
        IReadOnlyList<Word> adverb = adverbial is { } which ? [new Word([questions.Adverbs[which.Word].Root])] : [];
        if (!questions.FrontsQuestionWords)
        {
            return Words(language, stated, adverb);
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
        words[at] = affix.Marker.On(words[at], Reach.Edge);
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
    private static (Root Root, IReadOnlyList<Word> Phrase)? Asked(Language language, Proposition proposition)
    {
        foreach (var (argument, role, oblique) in Voiced(language, Collected(proposition.StateOfAffairs, proposition.Tense)).Event.Fillers)
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
    private static IEnumerable<Filler> Roles(Language language, Sentence statement) => statement.Value switch
    {
        Clause { Value: Declarative stated } => Voiced(language, Collected(stated.Propositional.Proposition.StateOfAffairs, stated.Propositional.Proposition.Tense)).Event.Fillers,
        _ => [],
    };

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
        Complement or Quotation => (Person.Third, Number.Singular),
        Shared _ => throw new ArgumentException("A shared subject is the main clause's, which is said in its place.", nameof(argument)),
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

    private static Rendering Rendering(Language language, IReadOnlyList<Word> words, string translation) =>
        new([], language.Phonology.RealizePhrase(words), translation, language.Orthography);

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Role role) =>
        Inflect(language, argument, CaseFor(language, language.RoleMarkers[role]), Localization.None);

    //a phrase on its own, in the case a noun is cited in, as in a dictionary or a name
    public static IReadOnlyList<Word> Cited(Language language, Argument phrase) => Inflect(language, phrase, Citation(language), Localization.None);

    //a localization is None for anything that isn't a place with a configuration in the noun
    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Case @case, Localization localization)
    {
        var nouns = language.Paradigms.Nouns;
        var pronouns = language.Pronouns.Paradigm;
        return argument switch
        {
            NounPhrase compares when ComparedByClause(language, compares) is var (relative, denial) =>
                [.. Inflect(language, relative, @case, localization), .. denial is { } denied ? Words(language, denied).Words : []],
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
            Complement or Quotation => throw new ArgumentException("A clause or a quotation can only be an object, which Object says.", nameof(argument)),
            Shared _ => throw new ArgumentException("A shared subject is the main clause's, which is said in its place.", nameof(argument)),
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
            return Graded(language, [agreement == AdjectiveAgreement.Agreement
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
        return [.. inflected.Take(main), .. WithStandards(language, words, noun.Adjectives.SelectMany(Standards)), .. inflected.Skip(main + 1)];
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
/// A line rendered in a language: its words in groups, each a word with a root and the clitics leaning
/// on it, said as one word, or a clitic with nothing to lean on. A heading's label comes first,
/// said apart from the rest, as a phrase of its own, and written with a colon after it; any other
/// sentence has none.
/// </summary>
public record Rendering(IReadOnlyList<SurfaceForm> Label, IReadOnlyList<SurfaceForm> Said, string Translation, Orthography Orthography)
{
    /// <summary>Every group of the sentence, its label's first.</summary>
    public IReadOnlyList<SurfaceForm> Groups => [.. Label, .. Said];

    /// <summary>Every word of the sentence, in order, with the group it's said in.</summary>
    public IReadOnlyList<RenderedWord> Words => [.. Groups.SelectMany(g => g.Words.Select(w => new RenderedWord(g, w)))];

    //clitics are written as words of their own, as French and Greek write theirs, and a word that
    //lost every sound to its host, as a clitic's vowel can, isn't written at all
    public string Text => Label.Count == 0 ? Spelled(Said) : $"{Spelled(Label)}: {Spelled(Said)}";

    private string Spelled(IReadOnlyList<SurfaceForm> groups) =>
        string.Join(" ", groups.SelectMany(g => g.Words.Select(w => g.Spell(Orthography, w))).Where(s => s.Length > 0));

    /// <summary>
    /// The IPA of the sentence as it's said, a group at a time, with ‿ where a silent consonant is
    /// said before the next word's vowel, as in [pəˈtit‿aˈmi], and | for the pause after a label.
    /// </summary>
    public string Transcribed() => Label.Count == 0 ? $"[{Transcribed(Said)}]" : $"[{Transcribed(Label)} | {Transcribed(Said)}]";

    private static string Transcribed(IReadOnlyList<SurfaceForm> groups) =>
        string.Concat(groups.Select((g, i) => g.Transcribed() + (i == groups.Count - 1 ? "" : g.LinksToNext ? "‿" : " ")));

    /*
       https://www.eva.mpg.de/lingua/resources/glossing-rules.php
      the sentence, then, if asked for, how it's said, then its morphemes and their glosses aligned
      group by group, with = between a host and its clitics, then a free translation. How it's said
      is left out unless asked for, since most uses only want the language as it's written. A
      label's colon is written after its last group, as it is in the sentence
     */
    public string Interlinear(bool phonetic)
    {
        var groups = Groups;
        string Segmented(int i) => groups[i].Segmented(Orthography) + (i == Label.Count - 1 ? ":" : "");
        var widths = groups.Select((g, i) => Math.Max(Segmented(i).Length, g.Gloss.Length)).ToList();
        string Aligned(Func<int, string> line) =>
            string.Join("  ", groups.Select((_, i) => line(i).PadRight(widths[i]))).TrimEnd();
        return string.Join(Environment.NewLine,
            [Text, .. phonetic ? [Transcribed()] : Array.Empty<string>(), Aligned(Segmented), Aligned(i => groups[i].Gloss), $"'{Translation}'"]);
    }
}

/// <summary>One word of a sentence, and the group it's said in.</summary>
public record RenderedWord(SurfaceForm Group, Word Word)
{
    public string Gloss => Word.Gloss;

    public string Spell(Orthography orthography) => Group.Spell(orthography, Word);
}
