namespace LanguageGenerator.Tests;

//builders for hand-made phonologies and words, using the full phoneme inventory. A phonology may say
//every sound it lacks as an allophone, as a generated language may
internal static class TestData
{
    public static List<Vowel> Vowels { get; } = [.. Vowel.All];
    public static List<Consonant> Consonants { get; } = [.. Consonant.All];
    public static StressSystem Stress { get; } = new(LanguageGenerator.Stress.Penultimate, false);

    public static Phonology Phonology(SyllableTemplate template, ClusterRepair repair, params SoundChange[] rules) =>
        Phonology(template, repair, Consonants, rules);

    public static Phonology Phonology(SyllableTemplate template, ClusterRepair repair, List<Consonant> consonants, params SoundChange[] rules) => new()
    {
        Vowels = Vowels,
        Consonants = consonants,
        SyllableRule = new SyllableRule(template, Vowels, consonants),
        SoundChanges = [.. rules],
        ClusterRepair = repair,
        EpentheticVowel = Vowel.ə,
        Stress = Stress,
        Dropoff = new Dropoff(0.15, 0.15),
        SyllableContact = SyllableContact.Any,
        Allophones = Generator.SelectAllophones(Vowels, consonants),
        Sandhi = NoSandhi,
    };

    //nothing happens where words meet, and clitics are stressed with their host by the rule
    public static Sandhi NoSandhi { get; } = new([], false, CliticStress.Group);

    //the first form is a root meaning "dog"; any after it are plural suffixes
    public static Word Word(params Phoneme[][] morphemes) =>
        new([.. morphemes.Select((form, i) => i == 0 ? (Morpheme)new Root(form, "dog", null) : new Affix(form, [Number.Plural], AffixAccent.None))]);
}

//a template with no onsets, which no generated language has, for testing what happens at the edges
internal static class Templates
{
    public static SyllableTemplate Onsetless { get; } = new(Slot.Vowel, Slot.Final);
}

//finding words in a realized sentence, by meaning or by root
internal static class Sentences
{
    public static int IndexOf(Rendering sentence, string meaning) => sentence.Words.ToList().FindIndex(w => w.Word.Root?.Meaning == meaning);
    public static int IndexOf(Rendering sentence, Root root) => sentence.Words.ToList().FindIndex(w => w.Word.Root == root);
}

/*
  clauses tests share, each present, imperfective and affirmative, to vary with `with` or At, and a
  statement's predicate or force, where it's a clause with one of that kind. They're immutable
  values, so sharing them shares nothing a test can change
 */
internal static class Clauses
{
    public static Clause DogWalks { get; } = Stating(Phrases.Dog, new Doing("walk", null));
    public static Clause DogSleeps { get; } = Stating(Phrases.Dog, new Doing("sleep", null));
    public static Clause DogIsBig { get; } = Stating(Phrases.Dog, new Being(new Adjective("big", false)));
    public static Clause DogEatsFish { get; } = Stating(Phrases.Dog, new Doing("eat", Phrases.Fish));
    public static Clause CatEatsFish { get; } = Stating(Phrases.Cat, new Doing("eat", Phrases.Fish));

    private static Clause Stating(Argument subject, Predicate predicate) => Declared(new Event(predicate, subject, [], []), Tense.Present, Aspect.Imperfective, false);

    //a stated clause at another time, with an aspect over what it states, which the imperfective isn't
    public static Clause At(this Clause clause, Tense tense, Aspect aspect) => clause switch
    {
        Declarative stated => stated with
        {
            Propositional = stated.Propositional.Rebuilt(p => p with
            {
                Tense = tense,
                StateOfAffairs = aspect == Aspect.Imperfective ? p.StateOfAffairs : new Aspected(aspect, p.StateOfAffairs),
            }),
        },
        _ => throw new ArgumentException("Only a clause that states something has a tense.", nameof(clause)),
    };

    //a clause with a negation over all it says, or tells
    public static Clause Not(this Clause clause) => clause switch
    {
        Declarative stated => stated with { Propositional = stated.Propositional.Rebuilt(p => p with { StateOfAffairs = new Negated(p.StateOfAffairs) }) },
        Interrogative asked => asked with { Propositional = asked.Propositional.Rebuilt(p => p with { StateOfAffairs = new Negated(p.StateOfAffairs) }) },
        Directive told => told with { StateOfAffairs = new Negated(told.StateOfAffairs) },
    };

    //a clause with its event what this makes of it, as with another subject or object
    public static Clause WithEvent(this Clause clause, Func<Event, Event> rebuild) => clause switch
    {
        Declarative stated => stated with { Propositional = stated.Propositional.Rebuilt(p => p with { StateOfAffairs = p.StateOfAffairs.Rebuilt(rebuild) }) },
        Interrogative asked => asked with { Propositional = asked.Propositional.Rebuilt(p => p with { StateOfAffairs = p.StateOfAffairs.Rebuilt(rebuild) }) },
        Directive told => told with { StateOfAffairs = told.StateOfAffairs.Rebuilt(rebuild) },
    };

    //what a line or a quotation states, where it's a statement said to no one
    public static Sentence Stated(Utterance utterance) => utterance.Value is Message { Addressee: null } message
        ? SentenceOf(message)
        : throw new Xunit.Sdk.XunitException($"Expected a statement said to no one, but got {utterance.Value}.");

    //a line said to someone, with who it's said to
    public static Message Addressed(Utterance utterance) => utterance.Value is Message { Addressee: not null } message
        ? message
        : throw new Xunit.Sdk.XunitException($"Expected a message said to someone, but got {utterance.Value}.");

    //what a message states, where it's a statement and not a fragment
    public static Sentence SentenceOf(Message message) => message.Content.Value is Sentence statement
        ? statement
        : throw new Xunit.Sdk.XunitException($"Expected a statement, but got {message.Content.Value}.");

    //what the parser makes of one gloss line, as it is
    public static Utterance Line(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance;

    //a clause stating an event at a time, with an aspect, negated or not, the imperfective being no operator
    public static Clause Declared(Event @event, Tense tense, Aspect aspect, bool negative) => new Declarative(new Proposition(Layered(@event, aspect, negative), tense));

    //a clause telling 'you' to bring an event about, or not to
    public static Clause Told(Event @event, bool negative) => new Directive(negative ? new Negated(@event) : @event);

    //an event with its aspect, then its negation, over it
    public static StateOfAffairs Layered(Event @event, Aspect aspect, bool negative)
    {
        StateOfAffairs layers = aspect == Aspect.Imperfective ? @event : new Aspected(aspect, @event);
        return negative ? new Negated(layers) : layers;
    }

    //a stated clause with a situational modal over what it states, and the modal's own negation over that
    public static Clause WithModal(Clause clause, SituationalModality modality, bool negative) => clause switch
    {
        Declarative stated => stated with { Propositional = stated.Propositional.Rebuilt(p => p with { StateOfAffairs = Negating(new Modalized(modality, p.StateOfAffairs), negative) }) },
        _ => throw new ArgumentException("Only a stated clause takes a modal here.", nameof(clause)),
    };

    private static StateOfAffairs Negating(StateOfAffairs layers, bool negative) => negative ? new Negated(layers) : layers;

    //a stated clause the speaker judges possible or sure, and denies that or not
    public static Clause Judging(Clause clause, EpistemicModality modality, bool denied) => clause switch
    {
        Declarative stated => stated with { Propositional = denied ? new Denied(new Judged(modality, stated.Propositional)) : new Judged(modality, stated.Propositional) },
        _ => throw new ArgumentException("Only a stated clause is judged here.", nameof(clause)),
    };

    //a stated clause asked, with 'why' or 'how' among its adverbs if it asks either
    public static Clause Asked(Clause clause, QuestionAdverb? adverb) => clause switch
    {
        Declarative stated => new Interrogative(adverb is { } why
            ? stated.Propositional.Rebuilt(p => p with { StateOfAffairs = p.StateOfAffairs.Rebuilt(e => e with { Adverbs = [.. e.Adverbs, new QuestionAdverbial(why)] }) })
            : stated.Propositional),
        _ => throw new ArgumentException("Only a stated clause is asked here.", nameof(clause)),
    };

    //a stated clause with be and an adjective, its adjective compared with a standard, or with none
    public static Clause Comparing(Clause clause, Argument? standard) => clause switch
    {
        Declarative stated => stated with
        {
            Propositional = stated.Propositional.Rebuilt(p => p with
            {
                StateOfAffairs = p.StateOfAffairs.Rebuilt(e => e.Predicate.Value is Being { Attribute.Value: Adjective adjective } being
                    ? e with { Predicate = being with { Attribute = new Compared(adjective, standard) } }
                    : throw new ArgumentException("Only an adjective said with be is compared.", nameof(clause))),
            }),
        },
        _ => throw new ArgumentException("Only a stated clause compares here.", nameof(clause)),
    };

    //a stated clause under another, as an adverbial clause of that kind, a purpose clause sharing the main clause's subject or not
    public static Subordinated Under(Sentence main, Subordinator subordinator, Clause clause, bool sharesSubject) => (subordinator, clause) switch
    {
        (Subordinator.Purpose, Declarative stated) => new Subordinated(main, new Purpose(Sharing(stated.Propositional.Proposition.StateOfAffairs, sharesSubject))),
        (_, Declarative stated) when !sharesSubject => new Subordinated(main, new Circumstance(RelationOf(subordinator), stated.Propositional)),
        _ => throw new ArgumentException("Only a stated clause goes under another, and only a purpose clause shares its subject here.", nameof(clause)),
    };

    private static Relation RelationOf(Subordinator subordinator) => subordinator switch
    {
        Subordinator.If => Relation.If,
        Subordinator.When => Relation.When,
        Subordinator.Before => Relation.Before,
        Subordinator.After => Relation.After,
        Subordinator.Until => Relation.Until,
        Subordinator.Because => Relation.Because,
        _ => throw new ArgumentOutOfRangeException(nameof(subordinator), subordinator, "A purpose clause has no relation."),
    };

    //a stated clause as the object of a verb, what's said or what's wanted, the latter sharing the main clause's subject or not
    public static Complement Complementing(ComplementKind kind, Clause clause, bool sharesSubject) => (kind, clause) switch
    {
        (ComplementKind.Proposition, Declarative stated) when !sharesSubject => new Said(stated.Propositional),
        (ComplementKind.Event, Declarative { Propositional.Value: Proposition wanted }) => new Wanted(Sharing(wanted.StateOfAffairs, sharesSubject)),
        _ => throw new ArgumentException("Only a stated clause is an object, and only what's wanted shares its subject here.", nameof(clause)),
    };

    //a state of affairs whose events have the subject of the clause they depend on, or their own
    public static StateOfAffairs Sharing(StateOfAffairs layers, bool shares) => shares ? layers.Rebuilt(e => e with { Subject = new Shared() }) : layers;

    //a stated clause as a relative clause, with its gap where it has one
    public static Propositional Relating(Clause clause) => clause switch
    {
        Declarative stated => stated.Propositional,
        _ => throw new ArgumentException("Only a stated clause is a relative clause.", nameof(clause)),
    };

    //the first event of a clause inside another
    public static Event EventOf(Complement complement) => complement switch
    {
        Said said => said.Propositional.Proposition.StateOfAffairs.Event,
        Wanted wanted => wanted.StateOfAffairs.Event,
    };

    public static Event EventOf(AdverbialClause clause) => clause switch
    {
        Purpose purpose => purpose.StateOfAffairs.Event,
        Circumstance circumstance => circumstance.Propositional.Proposition.StateOfAffairs.Event,
    };

    //a statement's clause, its event, its predicate, its sentence type and the proposition it states or asks, where it has each of these
    public static Clause ClauseOf(Sentence statement) => Assert.IsType<Clause>(statement.Value);
    public static Event EventOf(Sentence statement) => ClauseOf(statement).Event;
    public static T PredicateOf<T>(Sentence statement) => Assert.IsType<T>(EventOf(statement).Predicate.Value);
    public static T ForceOf<T>(Sentence statement) => Assert.IsType<T>(ClauseOf(statement).Value);
    //the aspect over a statement's event, or the unmarked one where it has none
    public static Aspect AspectOf(Sentence statement) => AspectOf(ClauseOf(statement).StateOfAffairs);

    private static Aspect AspectOf(StateOfAffairs layers) => layers switch
    {
        Event => Aspect.Imperfective,
        Aspected aspected => aspected.Aspect,
        Negated negated => AspectOf(negated.StateOfAffairs),
        Modalized modalized => AspectOf(modalized.StateOfAffairs),
        Coordinated<StateOfAffairs> => throw new Xunit.Sdk.XunitException("Joined states of affairs each have an aspect of their own."),
    };

    public static Proposition PropositionOf(Sentence statement) => ClauseOf(statement).Value switch
    {
        Declarative stated => stated.Propositional.Proposition,
        Interrogative asked => asked.Propositional.Proposition,
        var other => throw new Xunit.Sdk.XunitException($"Expected a proposition stated or asked, but got {other}."),
    };

    //the judgment over what a statement states or asks, if it has one
    public static EpistemicModality? JudgmentOf(Sentence statement) => ClauseOf(statement).Value switch
    {
        Declarative stated => JudgmentOf(stated.Propositional),
        Interrogative asked => JudgmentOf(asked.Propositional),
        _ => null,
    };

    private static EpistemicModality? JudgmentOf(Propositional propositional) => propositional switch
    {
        Judged judged => judged.Modality,
        Denied denied => JudgmentOf(denied.Propositional),
        Proposition => null,
    };
}

//a noun on its own, with no modifiers, determiner, possessor or relative clause
internal static class Phrases
{
    public static NounPhrase BareNoun(string meaning, Number number) => new(new Noun(meaning, number, [], null, null, null, null, null), null);

    //the nouns tests use most, bare and singular
    public static NounPhrase Dog { get; } = BareNoun("dog", Number.Singular);
    public static NounPhrase Cat { get; } = BareNoun("cat", Number.Singular);
    public static NounPhrase Fish { get; } = BareNoun("fish", Number.Singular);

    //the noun a phrase is of, where it's a noun phrase of a noun
    public static Noun NounOf(Argument argument) => NounOf(Assert.IsType<NounPhrase>(argument.Value));
    public static Noun NounOf(NounPhrase phrase) => Assert.IsType<Noun>(phrase.Head.Value);
}

//what the tests' people and animals are, as a noun file would say
internal static class TestNouns
{
    public static IReadOnlyDictionary<string, SemanticClass> All { get; } = NounFile.Parse("woman F\nking M\nperson HUM\nhunter HUM\ndog ANIM\ncat ANIM");

    public static SemanticClass Of(string meaning) => All.GetValueOrDefault(meaning, SemanticClass.Thing);
}

/*
  generated languages, shared between tests since generating them is the slow part. Seeds are
  fixed, so tests over them are deterministic. Their lexicons are frozen, since a test that coined
  a word into one would change what every other test sees; a test that needs new words generates
  a language of its own
 */
internal static class GeneratedLanguages
{
    private static readonly Lazy<IReadOnlyList<Language>> sample = new(() => [.. Enumerable.Range(0, 300).Select(seed =>
    {
        var language = Generator.Generate(seed, LanguageSpec.Free);
        language.Lexicon.Classify(TestNouns.All);
        language.Lexicon.Freeze();
        return language;
    })]);

    public static IReadOnlyList<Language> Sample => sample.Value;

    /*
      a language with some settings fixed, for a test that needs one with some feature, so it doesn't
      depend on which of the sample happen to have it. It's the test's own, so it's neither classified
      nor frozen
     */
    public static Language With(Func<LanguageSpec, LanguageSpec> fix) => Generator.Generate(1, fix(LanguageSpec.Free));

    //a spec that inflects, by any fusion but isolating
    public static LanguageSpec Inflecting(this LanguageSpec spec) => spec.Fix(Settings.Fusion, Fusion.Concatenative, Fusion.Mixed);

    //a spec whose object goes before the verb, or after it, by any constituent order that does
    public static LanguageSpec ObjectFirst(this LanguageSpec spec, bool objectFirst) =>
        spec.Fix(Settings.ConstituentOrder, [.. Enum.GetValues<ConstituentWordOrder>().Where(o => WordOrder.ObjectPrecedesVerb(o) == objectFirst)]);

    //every lexeme's and pronoun's citation form, and every cell of every paradigm for every lexeme or
    //pronoun it applies to
    public static IEnumerable<Word> AllWords(Language language)
    {
        var lexemes = language.Lexicon.Concat(language.Pronouns.Forms.Values.Distinct()).ToList();
        foreach (var lexeme in lexemes)
        {
            yield return new Word([lexeme.Root]);
        }
        foreach (var paradigm in language.Paradigms.All.Append(language.Pronouns.Paradigm))
        {
            foreach (var lexeme in lexemes.Where(l => l.PartOfSpeech == paradigm.PartOfSpeech))
            {
                foreach (var cell in paradigm.Cells)
                {
                    foreach (var word in paradigm.Inflect(lexeme, cell))
                    {
                        yield return word;
                    }
                }
            }
        }
    }
}
