namespace LanguageGenerator;

//what a sentence means, independent of any language: words are meanings, like "dog", which each
//language's lexicon turns into its own roots. Realizing one in a language is where all of that
//language's choices (word order, role markers, paradigms, phonology) come together
/// <summary>
/// A noun with any adjectives modifying it, in English order, how many of it there are if a numeral
/// says, as in 'three wolves', whose it is, as in 'the king's house', which one it is, as in 'the
/// third', and whether it's definite, as 'the dog' is and 'a dog' isn't.
/// </summary>
//no definiteness is a bare noun, as English has for 'dogs', 'water' or 'my dog'
public record NounPhrase(string Noun, Number Number, ValueList<Adjective> Adjectives = default, int? Count = null, Argument? Possessor = null, int? Ordinal = null,
    Definiteness? Definiteness = null);
/// <summary>What fills a role: a noun phrase, a pronoun, or several of them joined by 'and' or 'or'.</summary>
//what agrees with one depends on the language, so that's Syntax.AgreementOf's to say
public union Argument(NounPhrase, Pronoun, Coordination);
/// <summary>
/// A phrase saying where, to where, from where, to whom, with whom or with what something happened.
/// </summary>
public record Oblique(Role Role, Argument Argument)
{
    public static IReadOnlyList<Role> Roles { get; } =
        [Role.Recipient, Role.Location, Role.Goal, Role.Source, Role.Companion, Role.Instrument];
}

//obliques are in the order they're said in English
public record Clause(string Verb, Tense Tense, Aspect Aspect, Argument Subject, Argument? Object = null, bool Negative = false, ValueList<Oblique> Obliques = default);

/// <summary>A property, like 'big', said of something.</summary>
public record Adjective(string Meaning);
/// <summary>
/// What a copular clause says of its subject: a noun phrase, as in 'you are a fool', an adjective,
/// as in 'the gate is locked', adjectives together, as in 'the inn is warm and crowded', or a
/// Location, as in 'the dog is in the house'.
/// </summary>
public union Complement(NounPhrase, Adjective, AdjectiveCoordination, Oblique);
/// <summary>A clause saying what or where its subject is, as English does with be.</summary>
public record CopularClause(Complement Complement, Tense Tense, Aspect Aspect, Argument Subject, bool Negative = false);

/// <summary>
/// Telling one person or more to do something, or with Negative, not to. Who's told is 'you', so it
/// isn't said, and a command has no tense or aspect of its own.
/// </summary>
public record Command(string Verb, Number Number, Argument? Object = null, bool Negative = false, ValueList<Oblique> Obliques = default);

/// <summary>
/// Telling one person or more what or where to be, as in 'be careful', 'don't be a fool' or 'be in
/// the house'.
/// </summary>
public record CopularCommand(Complement Complement, Number Number, bool Negative);

/// <summary>
/// A statement said to someone called by name or by what they are, as in 'Marta, come here' or 'old
/// man, the wolves are in the village'.
/// </summary>
//the addressee is a noun phrase, never a pronoun, and bare, since a vocative takes no article in
//nearly every language
public record Addressed(Statement Statement, NounPhrase Addressee);

/// <summary>Anything a sentence can say.</summary>
public union Statement(Clause, PassiveClause, CopularClause, Command, CopularCommand, Comparative, Addressed, Coordinated, Subordinated);

public static class Syntax
{
    public static Sentence Realize(Language language, Statement statement, string translation) => Sentence(language, Words(language, statement), translation);

    //a statement's words, before they're sounded out, so that a statement inside another can be
    //marked as one
    private static IReadOnlyList<Word> Words(Language language, Statement statement) => statement switch
    {
        Clause c => Words(language, c),
        PassiveClause c => Words(language, c),
        CopularClause c => Words(language, c),
        Command c => Words(language, c),
        CopularCommand c => Words(language, c),
        Comparative c => Words(language, c),
        Addressed a => Words(language, a),
        Coordinated c => Words(language, c),
        Subordinated s => Words(language, s),
    };

    /// <summary>Who or what a statement is about: its subject, or 'you' for a command.</summary>
    public static Argument SubjectOf(Statement statement) => statement switch
    {
        Clause c => c.Subject,
        PassiveClause c => c.Patient,
        CopularClause c => c.Subject,
        Comparative c => c.Clause.Subject,
        Command c => new Pronoun(Person.Second, c.Number),
        CopularCommand c => new Pronoun(Person.Second, c.Number),
        Addressed a => SubjectOf(a.Statement),
        Coordinated c => SubjectOf(c.First),
        Subordinated s => SubjectOf(s.Main),
    };

    public static Sentence Realize(Language language, Subordinated subordinated, string translation) => Sentence(language, Words(language, subordinated), translation);

    //the subordinate clause, marked by its word or suffix, before or after the main one as the language
    //puts that kind. Its verb is deranked, bare of tense and agreement, where the language derank
    //this kind, or does either and the two clauses share a subject, which a deranked clause then
    //leaves unsaid; only a verb's clause can be deranked here, and a copular one is always balanced.
    //A balanced clause whose subject was taken from the main clause says it as a pronoun. A deranked
    //clause the language marks like a role takes that role's case on its verb, or its adposition, in
    //place of the subordinator, as a noun in that role would
    private static IReadOnlyList<Word> Words(Language language, Subordinated subordinated)
    {
        var marking = language.Subordination.Markers[subordinated.Subordinator];
        var shared = subordinated.SharesSubject || SubjectOf(subordinated.Clause).Equals(SubjectOf(subordinated.Main));
        var derank = marking.Balance == Balance.Deranked || marking.Balance == Balance.Either && shared;
        var deranked = derank ? Deranked(language, subordinated.Clause, shared, marking.MarkedAs) : null;
        var marked = deranked is not null && marking.MarkedAs is not null
            ? deranked
            : Subordinate(deranked ?? Words(language, subordinated.SharesSubject ? WithSubject(subordinated.Clause, Pronominal(language, SubjectOf(subordinated.Main))) : subordinated.Clause),
                marking, VerbRoot(language, subordinated.Clause));
        var main = Words(language, subordinated.Main);
        return marking.BeforeMain ? [.. marked, .. main] : [.. main, .. marked];
    }

    //a clause with its marker: a word at its start or end, or a suffix outside everything on its verb,
    //or on its last word where it has no verb, as a copular clause that drops its copula doesn't
    private static IReadOnlyList<Word> Subordinate(IReadOnlyList<Word> clause, SubordinatorMarking marking, Root? verb)
    {
        switch (marking.Position)
        {
            case ExponentPosition.PrecedingParticle:
                return [new Word([marking.Marker]), .. clause];
            case ExponentPosition.FollowingParticle:
                return [.. clause, new Word([marking.Marker])];
        }
        List<Word> words = [.. clause];
        var at = words.FindLastIndex(w => w.Root is { } r && r == verb);
        at = at < 0 ? words.Count - 1 : at;
        words[at] = words[at] with { Morphemes = [.. words[at].Morphemes, marking.Marker] };
        return words;
    }

    //the root of a statement's verb, where it has one: its own verb, a verbal adjective, or its copula
    private static Root? VerbRoot(Language language, Statement statement) => statement.Value switch
    {
        Clause c => language.Lexicon.Verb(c.Verb).Root,
        PassiveClause c => language.Lexicon.Verb(c.Verb).Root,
        Comparative c => VerbRoot(language, c.Clause),
        CopularClause { Complement.Value: LanguageGenerator.Oblique } => language.Predication.CopulaFor(locational: true).Root,
        CopularClause c when language.Predication.Adjectives == PredicativeAdjectives.Verbal && PredicateAdjectives(language, c.Complement) is var (adjectives, _) =>
            language.Lexicon.Adjective(adjectives[^1].Meaning).Root,
        CopularClause => language.Predication.Copula.Root,
        _ => null,
    };

    //a verb's clause with its verb bare of tense and agreement, keeping negation and the passive,
    //and without its subject where it shares the main clause's. Marked like a role, its verb takes
    //that role's case, or the whole clause its adposition. Null for any other kind of clause
    private static IReadOnlyList<Word>? Deranked(Language language, Statement statement, bool dropSubject, Role? markedAs)
    {
        var marker = markedAs is { } role ? language.RoleMarkers[role].Value : null;
        IReadOnlyList<Word> Verb(Lexeme verb, bool negative)
        {
            var words = marker is Case @case ? AsNoun(language, verb, @case) : [new Word([verb.Root])];
            return negative ? language.Negation.Apply(words) : words;
        }
        IReadOnlyList<Word> Adpositioned(IReadOnlyList<Word> clause) => marker switch
        {
            Adposition { Type: AdpositionType.Preposition } a => [new Word([a.Lexeme.Root]), .. clause],
            Adposition a => [.. clause, new Word([a.Lexeme.Root])],
            _ => clause,
        };

        switch (statement.Value)
        {
            case Clause c:
            {
                var verb = Verb(language.Lexicon.Verb(c.Verb), c.Negative);
                var subject = dropSubject ? [] : Subject(language, c.Subject, c.Object is null ? Role.IntransitiveSubject : Role.TransitiveSubject);
                var @object = c.Object.HasValue ? Inflect(language, c.Object.GetValueOrDefault(), Role.DirectObject) : null;
                return Adpositioned(Flat(Order(language.WordOrder, subject, verb, @object, [.. c.Obliques.SelectMany(o => Oblique(language, o))])));
            }
            case PassiveClause c when language.Passive is { } passive:
            {
                var verb = passive.Apply(Verb(language.Lexicon.Verb(c.Verb), c.Negative));
                var subject = dropSubject ? [] : Subject(language, c.Patient, Role.IntransitiveSubject);
                IReadOnlyList<Word> agent = c.Agent.HasValue ? Marked(language, c.Agent.GetValueOrDefault(), Role.Agent) : [];
                return Adpositioned(Flat(Order(language.WordOrder, subject, verb, null, [.. agent, .. c.Obliques.SelectMany(o => Oblique(language, o))])));
            }
            case PassiveClause c:
                return Deranked(language, new Clause(c.Verb, c.Tense, c.Aspect, c.Agent.HasValue ? c.Agent.GetValueOrDefault() : new Pronoun(Person.Third, Number.Plural),
                    c.Patient, c.Negative, c.Obliques), dropSubject: false, markedAs);
            default:
                return null;
        }
    }

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

    private static Statement WithSubject(Statement statement, Argument subject) => statement.Value switch
    {
        Clause c => c with { Subject = subject },
        PassiveClause c => c with { Patient = subject },
        CopularClause c => c with { Subject = subject },
        Comparative c => c with { Clause = c.Clause with { Subject = subject } },
        _ => statement,
    };

    //each statement as it would be said alone, with the language's word for the conjunction between,
    //or nothing where it sets clauses side by side
    public static Sentence Realize(Language language, Coordinated coordinated, string translation) => Sentence(language, Words(language, coordinated), translation);

    private static IReadOnlyList<Word> Words(Language language, Coordinated coordinated)
    {
        IReadOnlyList<Word> between = language.Conjunctions.BetweenClauses(coordinated.Conjunction) is { } word ? [new Word([word.Root])] : [];
        return [.. Words(language, coordinated.First), .. between, .. Words(language, coordinated.Second)];
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

    //the addressee comes first, in the addressee's case: the vocative, where the language has one,
    //and otherwise the case a noun is cited in. Where a vocative goes is a matter of discourse more
    //than grammar, which WALS doesn't count, and first is the commonest place
    public static Sentence Realize(Language language, Addressed addressed, string translation) => Sentence(language, Words(language, addressed), translation);

    private static IReadOnlyList<Word> Words(Language language, Addressed addressed)
    {
        return [.. Marked(language, addressed.Addressee, Role.Addressee), .. Words(language, addressed.Statement)];
    }

    //the verb takes the language's imperative, or its ordinary second person where it has none for
    //this number or doesn't use it to forbid, and only its prohibitive negator says 'don't', if it
    //has one. The subject goes unsaid even in a language that says its pronouns, as in English
    public static Sentence Realize(Language language, Command command, string translation) => Sentence(language, Words(language, command), translation);

    private static IReadOnlyList<Word> Words(Language language, Command command)
    {
        var words = CommandVerb(language, language.Lexicon.Verb(command.Verb), command.Number, command.Negative);
        var @object = command.Object.HasValue ? Inflect(language, command.Object.GetValueOrDefault(), Role.DirectObject) : null;
        var obliques = command.Obliques.SelectMany(o => Oblique(language, o)).ToList();
        return Flat(Order(language.WordOrder, [], words, @object, obliques));
    }

    private static IReadOnlyList<Word> CommandVerb(Language language, Lexeme verb, Number number, bool negative)
    {
        var commands = language.Commands;
        var imperative = negative && !commands.ProhibitiveUsesImperative ? null : commands.MarkerFor(number);
        var words = imperative.HasValue
            ? commands.Imperative(verb, imperative.GetValueOrDefault())
            : Verb(language, verb, Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, number), negative: false);
        return negative ? (commands.Prohibitive ?? language.Negation).Apply(words) : words;
    }

    //the copula takes the imperative as any verb would, even in a language that drops it in the
    //present, since a command isn't a statement about the present: Russian has no copula in 'you are
    //careful' but has one in bud' ostorozhen 'be careful'. A verbal adjective takes the imperative
    //itself, a location takes the locational copula, and a predicate noun or adjective is what it
    //would be said of 'you'
    public static Sentence Realize(Language language, CopularCommand command, string translation) => Sentence(language, Words(language, command), translation);

    private static IReadOnlyList<Word> Words(Language language, CopularCommand command)
    {
        var predication = language.Predication;
        if (PredicateAdjectives(language, command.Complement) is var (adjectives, between) && predication.Adjectives == PredicativeAdjectives.Verbal)
        {
            var predicate = Join(adjectives.Select(a => CommandVerb(language, language.Lexicon.Adjective(a.Meaning) with { PartOfSpeech = PartOfSpeech.Verb }, command.Number, command.Negative)), between);
            return Flat(Order(language.WordOrder, [], predicate, null, []));
        }
        if (command.Complement.Value is Oblique location)
        {
            var be = CommandVerb(language, predication.CopulaFor(locational: true), command.Number, command.Negative);
            return Flat(Order(language.WordOrder, [], be, null, Oblique(language, location)));
        }
        var complement = PredicateComplement(language, command.Complement, new Pronoun(Person.Second, command.Number));
        var copula = CommandVerb(language, predication.Copula, command.Number, command.Negative);
        return Flat(Order(language.WordOrder, [], copula, complement, []));
    }

    public static Sentence Realize(Language language, Clause clause, string translation) => Sentence(language, Words(language, clause), translation);

    private static IReadOnlyList<Word> Words(Language language, Clause clause)
    {
        var subjectRole = clause.Object is null ? Role.IntransitiveSubject : Role.TransitiveSubject;
        var subject = Subject(language, clause.Subject, subjectRole);
        var @object = clause.Object.HasValue ? Inflect(language, clause.Object.GetValueOrDefault(), Role.DirectObject) : null;
        var verb = Verb(language, language.Lexicon.Verb(clause.Verb), clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
        var obliques = clause.Obliques.SelectMany(o => Oblique(language, o)).ToList();
        return Flat(Order(language.WordOrder, subject, verb, @object, obliques));
    }

    //the patient takes the case of an intransitive subject, which in an ergative language is the one
    //it had already, and the verb agrees with it; the agent, if there is one, goes before the other
    //obliques, marked for its role. A language without a passive says the active instead, as real
    //ones do, with the agent as its subject, or 'they' where nobody says who
    public static Sentence Realize(Language language, PassiveClause clause, string translation) => Sentence(language, Words(language, clause), translation);

    private static IReadOnlyList<Word> Words(Language language, PassiveClause clause)
    {
        if (language.Passive is not { } passive)
        {
            var doer = clause.Agent.HasValue ? clause.Agent.GetValueOrDefault() : new Pronoun(Person.Third, Number.Plural);
            return Words(language, new Clause(clause.Verb, clause.Tense, clause.Aspect, doer, clause.Patient, clause.Negative, clause.Obliques));
        }
        var subject = Subject(language, clause.Patient, Role.IntransitiveSubject);
        var verb = passive.Apply(Verb(language, language.Lexicon.Verb(clause.Verb), clause.Tense, clause.Aspect, clause.Patient, clause.Negative));
        IReadOnlyList<Word> agent = clause.Agent.HasValue ? Marked(language, clause.Agent.GetValueOrDefault(), Role.Agent) : [];
        var obliques = agent.Concat(clause.Obliques.SelectMany(o => Oblique(language, o))).ToList();
        return Flat(Order(language.WordOrder, subject, verb, null, obliques));
    }

    //an oblique takes its role's case, and its adposition, if the role has one, before or after it
    private static IReadOnlyList<Word> Oblique(Language language, Oblique oblique)
    {
        if (!LanguageGenerator.Oblique.Roles.Contains(oblique.Role))
        {
            throw new ArgumentException($"{oblique.Role} isn't an oblique role.", nameof(oblique));
        }
        return Marked(language, oblique.Argument, oblique.Role);
    }

    //a phrase marked for a role: its case, and its adposition, if the role has one, before or after it
    private static IReadOnlyList<Word> Marked(Language language, Argument argument, Role role)
    {
        var phrase = Inflect(language, argument, role);
        return language.RoleMarkers[role] switch
        {
            Adposition { Type: AdpositionType.Preposition } a => [new Word([a.Lexeme.Root]), .. phrase],
            Adposition a => [.. phrase, new Word([a.Lexeme.Root])],
            _ => phrase,
        };
    }

    public static Sentence Realize(Language language, CopularClause clause, string translation) => Sentence(language, Words(language, clause), translation);

    private static IReadOnlyList<Word> Words(Language language, CopularClause clause) => Copular(language, clause, []);

    //a copular clause's complement goes where an object would, so it precedes the copula in an OV
    //language, as in Turkish and Japanese, and follows it in a VO one. Where adjectives are verbal,
    //the adjective takes the verb's place instead, with no copula. A standard of comparison, if the
    //clause has one, goes where obliques go
    private static IReadOnlyList<Word> Copular(Language language, CopularClause clause, IReadOnlyList<Word> standard)
    {
        var predication = language.Predication;
        var subject = Subject(language, clause.Subject, Role.IntransitiveSubject);
        if (PredicateAdjectives(language, clause.Complement) is var (adjectives, between) && predication.Adjectives == PredicativeAdjectives.Verbal)
        {
            //a property word inflects as a verb in a language whose adjectives are verbs
            var predicate = Join(adjectives.Select(a =>
                Verb(language, language.Lexicon.Adjective(a.Meaning) with { PartOfSpeech = PartOfSpeech.Verb }, clause.Tense, clause.Aspect, clause.Subject, clause.Negative)), between);
            return Flat(Order(language.WordOrder, subject, predicate, null, standard));
        }

        //a location is an oblique, and goes where obliques go, with the language's locational copula
        if (clause.Complement.Value is Oblique location)
        {
            if (location.Role != Role.Location)
            {
                throw new ArgumentException($"A copular clause can say where its subject is, but not its {location.Role}.", nameof(clause));
            }
            if (standard.Count > 0)
            {
                throw new ArgumentException("Only an adjective can be compared.", nameof(clause));
            }
            var be = predication.Drops(clause.Tense, clause.Negative, locational: true)
                ? []
                : Verb(language, predication.CopulaFor(locational: true), clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
            return Flat(Order(language.WordOrder, subject, be, null, Oblique(language, location)));
        }

        //a predicate noun takes the same case as an intransitive subject. A predicate adjective, in a
        //language whose adjectives agree, agrees with the subject in number, gender and that case,
        //as in Latin canis magnus est 'the dog is big'; otherwise it's just its root
        var complement = PredicateComplement(language, clause.Complement, clause.Subject);
        var copula = predication.Drops(clause.Tense, clause.Negative)
            ? []
            : Verb(language, predication.Copula, clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
        return Flat(Order(language.WordOrder, subject, copula, complement, standard));
    }

    //a locational standard takes its role's case or adposition, and a particle's standard the plain
    //case a subject takes, with the particle on its side. A conjoined comparison is two clauses,
    //the second denying the property of the standard, as in 'Marta is tall, the men are not tall'
    public static Sentence Realize(Language language, Comparative comparative, string translation) => Sentence(language, Words(language, comparative), translation);

    private static IReadOnlyList<Word> Words(Language language, Comparative comparative)
    {
        var clause = comparative.Clause;
        if (clause.Complement.Value is not Adjective)
        {
            throw new ArgumentException("Only an adjective can be compared.", nameof(comparative));
        }
        return language.Comparison switch
        {
            LocationalComparison locational => Copular(language, clause, Marked(language, comparative.Standard, locational.Role)),
            ParticleComparison particle => Copular(language, clause, Than(particle, Inflect(language, comparative.Standard, Role.IntransitiveSubject))),
            ConjoinedComparison => [.. Words(language, clause), .. Words(language, clause with { Subject = comparative.Standard, Negative = !clause.Negative })],
        };

        static IReadOnlyList<Word> Than(ParticleComparison particle, IReadOnlyList<Word> standard) =>
            particle.BeforeStandard ? [new Word([particle.Than.Root]), .. standard] : [.. standard, new Word([particle.Than.Root])];
    }

    //a predicate noun or adjective, said of this subject
    private static IReadOnlyList<Word> PredicateComplement(Language language, Complement complement, Argument subject) => complement.Value switch
    {
        NounPhrase noun => Inflect(language, noun, Role.IntransitiveSubject),
        AdjectiveCoordination together => Join(together.Adjectives.Select(a => PredicateComplement(language, a, subject)), language.Conjunctions.BetweenClauses(together.Conjunction)),
        Adjective adjective when language.AdjectiveAgreement == AdjectiveAgreement.Agreement =>
            [AgreeingAdjective(language, language.Lexicon.Adjective(adjective.Meaning),
                Features(language.Paradigms.Nouns, [AgreementOf(language, subject).Number, CaseFor(language, language.RoleMarkers[Role.IntransitiveSubject])]),
                GenderOf(language, subject))],
        Adjective adjective => [new Word([language.Lexicon.Adjective(adjective.Meaning).Root])],
        _ => throw new ArgumentException("A predicate is a noun or an adjective.", nameof(complement)),
    };

    //a language that drops subject pronouns leaves the verb's agreement, if any, to say who
    private static IReadOnlyList<Word> Subject(Language language, Argument subject, Role role) =>
        subject.Value is Pronoun && language.Pronouns.DropsSubjects ? [] : Inflect(language, subject, role);

    //the verb agrees with its subject. TODO many ergative languages agree with the absolutive instead
    private static IReadOnlyList<Word> Verb(Language language, Lexeme verb, Tense tense, Aspect aspect, Argument subject, bool negative)
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
        return negative ? language.Negation.Apply(words) : words;
    }

    //the number a noun takes: its own, unless a numeral leaves it singular, as in Turkish üç kurt
    //'three wolf'
    private static Number NumberOf(Language language, NounPhrase phrase) =>
        phrase.Count is not null && language.Numerals.NounStaysSingular ? Number.Singular : phrase.Number;

    //what a verb or adjective agrees with: a noun is third person, in the number of its word, so
    //that a verb is singular with 'three wolf' too, as in Hungarian. Things joined by 'and' are
    //plural, or dual if there are two and the language has a dual, as Slovene's are, in the first
    //person if any of them is, else the second if any is, so 'you and I' is 'we'; 'or' agrees with
    //its last, as English 'either the dogs or the cat is'
    private static (Person Person, Number Number) AgreementOf(Language language, Argument argument) => argument switch
    {
        NounPhrase n => (Person.Third, NumberOf(language, n)),
        Pronoun p => (p.Person, p.Number),
        Coordination { Conjunction: Conjunction.Or } c => AgreementOf(language, c.Items[^1]),
        Coordination c => (c.Items.Select(i => AgreementOf(language, i).Person).MinBy(p => p == Person.First ? 0 : p == Person.Second ? 1 : 2)!,
            c.Items.Count == 2 ? Number.Dual : Number.Plural),
    };

    //the gender something agrees with: a noun's own, or a pronoun's, if the language has that gender.
    //A pronoun with no gender given has none to agree with, since what it refers to isn't known
    //Things joined by 'or' take their last's gender; things joined by 'and' share it if they all have
    //one, and otherwise take the masculine, or the animate, as real gender systems mostly resolve them
    private static Gender? GenderOf(Language language, Argument argument) => argument.Value switch
    {
        NounPhrase n => language.GenderOf(language.Lexicon.Noun(n.Noun)),
        Pronoun { Gender: { } g } when language.Genders.Values.Contains(g) => g,
        Coordination { Conjunction: Conjunction.Or } c => GenderOf(language, c.Items[^1]),
        Coordination c => Resolved([.. c.Items.Select(i => GenderOf(language, i))]),
        _ => null,
    };

    private static Gender? Resolved(IReadOnlyList<Gender?> genders) =>
        genders.Distinct().Count() == 1 ? genders[0]
        : genders.Contains(Gender.Masculine) ? Gender.Masculine
        : genders.Contains(Gender.Animate) ? Gender.Animate
        : genders.FirstOrDefault(g => g is not null);

    //an adjective agreeing with a noun: it takes the noun's markers for these features, but only its
    //affixes, since a particle belongs to the phrase and is said once, and a marker for its gender.
    //Its zero markers go unglossed, since the noun's gloss already shows them
    private static Word AgreeingAdjective(Language language, Lexeme adjective, IReadOnlyList<Grammeme> features, Gender? gender)
    {
        var inflected = language.Paradigms.Nouns.Inflect(adjective with { PartOfSpeech = PartOfSpeech.Noun }, features);
        var word = inflected.First(w => w.Root is not null) with { Covert = null };
        return gender is null ? word : language.Genders.Agree(word, gender, nextToRoot: true);
    }

    //each constituent keeps its own particles with it
    private static IReadOnlyList<Word> Flat(IEnumerable<IReadOnlyList<Word>> constituents) => [.. constituents.SelectMany(c => c)];

    private static Sentence Sentence(Language language, IReadOnlyList<Word> words, string translation) =>
        new([.. words.Select(language.Phonology.Realize)], translation, language.Orthography);

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Role role) =>
        Inflect(language, argument, CaseFor(language, language.RoleMarkers[role]));

    //a noun phrase on its own, in the case a noun is cited in, as in a dictionary or a name
    public static IReadOnlyList<Word> Cited(Language language, NounPhrase phrase) => Inflect(language, phrase, Citation(language));

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Case @case)
    {
        var nouns = language.Paradigms.Nouns;
        var pronouns = language.Pronouns.Paradigm;
        return argument switch
        {
            NounPhrase phrase => Possessed(language, phrase, language.Articles.Apply(Modified(language, phrase, Features(nouns, [NumberOf(language, phrase), @case])),
                language.Lexicon.Noun(phrase.Noun).Root, phrase.Definiteness, phrase.Number, language.GenderOf(language.Lexicon.Noun(phrase.Noun)))),
            Pronoun pronoun => pronouns.Inflect(language.Pronouns.For(pronoun), Features(pronouns, [@case])),
            Coordination coordination => Joined(language, coordination, @case),
        };
    }

    //each part in the case, with the language's 'and' or 'or' between each two, or for 'and' in a
    //language whose 'and' is 'with', every part after the first marked as a companion. An adposition
    //for the whole is said once, outside it all
    private static IReadOnlyList<Word> Joined(Language language, Coordination coordination, Case @case)
    {
        var conjunctions = language.Conjunctions;
        var parts = coordination.Items.Select(item => Inflect(language, item, @case)).ToList();
        return (coordination.Conjunction, conjunctions.NounAnd.Value) switch
        {
            (Conjunction.Or, _) => Join(parts, conjunctions.Or),
            (Conjunction.And, AndWith) => [.. parts[0], .. coordination.Items.Skip(1).SelectMany(item => Marked(language, item, Role.Companion))],
            (Conjunction.And, AndWord and) => Join(parts, and.Word),
            (Conjunction.And, _) => Join(parts, null),
            _ => throw new ArgumentException("Only 'and' and 'or' join noun phrases.", nameof(coordination)),
        };
    }

    //a phrase with its possessor, if it has one, before or after it all, as the language puts them.
    //The possessor takes the possessive role's case or adposition where the language marks it, and
    //is plain otherwise. Where the language marks the possessed noun, the noun takes an affix
    //agreeing with the possessor, and where it marks only that, the affix is all a pronoun possessor
    //needs, as in Hungarian ház-am 'my house'
    private static IReadOnlyList<Word> Possessed(Language language, NounPhrase phrase, IReadOnlyList<Word> words)
    {
        if (!phrase.Possessor.HasValue)
        {
            return words;
        }
        var possessor = phrase.Possessor.GetValueOrDefault();
        var possession = language.Possession;
        List<Word> possessed = [.. words];
        if (possession.MarksPossessed)
        {
            var root = language.Lexicon.Noun(phrase.Noun).Root;
            var at = possessed.FindIndex(w => w.Root == root);
            var (person, number) = AgreementOf(language, possessor);
            possessed[at] = possession.Agree(possessed[at], person, number);
        }
        if (possession.Marking == PossessiveMarking.Head && possessor.Value is Pronoun)
        {
            return possessed;
        }
        var owner = possession.MarksPossessor ? Marked(language, possessor, Role.Possessor) : Inflect(language, possessor, Citation(language));
        return possession.PossessorFirst ? [.. owner, .. possessed] : [.. possessed, .. owner];
    }

    //the noun's words with its adjectives placed around it: after the noun in a language whose noun
    //phrases are head-initial, before it in one whose are head-final, in the order given either way.
    //A numeral goes outside them, before or after, as the language puts numerals, so that 'three big
    //wolves' is either three big wolf or wolf big three. An ordinal goes like the outermost
    //adjective, inside the numeral, as in 'the first three big wolves', but never agrees, even where
    //adjectives do. The noun's particles stay outside it all
    private static IReadOnlyList<Word> Modified(Language language, NounPhrase phrase, IReadOnlyList<Grammeme> features)
    {
        var nounLexeme = language.Lexicon.Noun(phrase.Noun);
        var noun = language.Paradigms.Nouns.Inflect(nounLexeme, features);
        if (phrase.Adjectives.Count == 0 && phrase.Count is null && phrase.Ordinal is null)
        {
            return noun;
        }
        var main = noun.ToList().FindIndex(w => w.Root is not null);
        var head = noun[main];
        var agreement = language.AdjectiveAgreement;

        var adjectives = phrase.Adjectives.Select(a =>
        {
            var lexeme = language.Lexicon.Adjective(a.Meaning);
            return agreement == AdjectiveAgreement.Agreement
                ? AgreeingAdjective(language, lexeme, features, language.GenderOf(nounLexeme))
                : new Word([lexeme.Root]);
        }).ToList();

        var headFirst = language.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial;
        IReadOnlyList<Word> ordinal = phrase.Ordinal is { } nth ? language.Numerals.SayOrdinal(nth) : [];
        List<Word> words = headFirst ? [head, .. adjectives, .. ordinal] : [.. ordinal, .. adjectives, head];
        var at = headFirst ? 0 : words.Count - 1;
        if (phrase.Count is { } count)
        {
            var numerals = language.Numerals;
            List<Word> numeral = [.. numerals.Say(count).Select(n => new Word([n.Root]))];
            words = numerals.BeforeNoun ? [.. numeral, .. words] : [.. words, .. numeral];
            at += numerals.BeforeNoun ? numeral.Count : 0;
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
        return [.. noun.Take(main), .. words, .. noun.Skip(main + 1)];
    }

    //the case a noun takes for a role. Roles marked some other way still need a value for the
    //Case category: an adposition's governed case, or the plain citation case
    private static Case CaseFor(Language language, RoleMarker marker) => marker switch
    {
        Case c => c,
        Adposition a => a.Governs ?? Citation(language),
        Unmarked => Citation(language),
    };

    //the plain case a noun is cited in
    private static Case Citation(Language language) => language.MorphosyntacticAlignment switch
    {
        MorphosyntacticAlignment.NominativeAccusative => Case.Nominative,
        MorphosyntacticAlignment.ErgativeAbsolutive => Case.Absolutive,
        _ => Case.Direct,
    };

    //each category the paradigm inflects for takes whichever wanted value it distinguishes. A value
    //the language doesn't have falls back on the nearest one it does, and failing that on the
    //category's first value
    private static List<Grammeme> Features(Paradigm paradigm, IReadOnlyList<Grammeme> wanted) =>
        [.. paradigm.Categories.Select(category =>
            wanted.Where(w => w.GetType() == category.Values[0].GetType())
                .Select(w => Nearest(w, category.Values))
                .FirstOrDefault(v => v is not null) ?? category.Values[0])];

    //the value itself if it's available, or else the nearest available one it falls back on
    public static Grammeme? Nearest(Grammeme wanted, IReadOnlyCollection<Grammeme> available) =>
        ((Grammeme[])[wanted, .. Fallbacks.GetValueOrDefault(wanted, [])]).FirstOrDefault(available.Contains);

    //what a value turns into in a language that doesn't distinguish it, nearest first: a dual or a
    //paucal is still more than one, a language without a future speaks of it in the present, as
    //English often does, and an aorist is a perfective
    private static readonly Dictionary<Grammeme, Grammeme[]> Fallbacks = new()
    {
        [Number.Dual] = [Number.Paucal, Number.Plural],
        [Number.Paucal] = [Number.Plural],
        [Number.Collective] = [Number.Plural],
        [Tense.Future] = [Tense.Present],
        [Aspect.Aorist] = [Aspect.Perfective],
        [Mood.Subjunctive] = [Mood.Indicative],
        [Mood.Imperative] = [Mood.Subjunctive, Mood.Indicative],
    };

    //the order's own letters say where each constituent goes; an intransitive clause just has no O.
    //Obliques go together, as X, before or after the verb and object, or just before the verb
    private static IEnumerable<IReadOnlyList<Word>> Order(WordOrder order, IReadOnlyList<Word> subject, IReadOnlyList<Word> verb, IReadOnlyList<Word>? @object, IReadOnlyList<Word> obliques)
    {
        var letters = order.ConstituentWordOrder.ToString().Where(letter => letter != 'O' || @object is not null).ToList();
        if (obliques.Count > 0)
        {
            var verbAndObject = letters.Select((letter, i) => (letter, i)).Where(p => p.letter != 'S').Select(p => p.i).ToList();
            letters.Insert(order.ObliquePosition switch
            {
                ObliquePosition.BeforeVerbAndObject => verbAndObject.Min(),
                ObliquePosition.BetweenObjectAndVerb => letters.IndexOf('V'),
                _ => verbAndObject.Max() + 1,
            }, 'X');
        }
        return letters.Select(letter => letter switch
        {
            'S' => subject,
            'V' => verb,
            'X' => obliques,
            _ => @object!,
        });
    }
}

public record Sentence(IReadOnlyList<SurfaceForm> Words, string Translation, Orthography Orthography)
{
    public string Text => string.Join(" ", Words.Select(w => w.Spell(Orthography)));

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //the sentence, then its morphemes and their glosses aligned word by word, then a free translation
    public string Interlinear()
    {
        var widths = Words.Select(w => Math.Max(w.Segmented(Orthography).Length, w.Gloss.Length)).ToList();
        string Aligned(Func<SurfaceForm, string> line) =>
            string.Join("  ", Words.Select((w, i) => line(w).PadRight(widths[i]))).TrimEnd();
        return string.Join(Environment.NewLine, Text, Aligned(w => w.Segmented(Orthography)), Aligned(w => w.Gloss), $"'{Translation}'");
    }
}
