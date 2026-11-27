# The clause model

What a sentence means before any language says it: the types `ClauseFile` builds from the [clause format](clause-format.md) and `Syntax` realizes. It replaced `Statement`, `Force`, `Directive`, `Question`, `Modalized`, `Comparative` and `Addressed` as they stood, for the reasons [Why](#why) gives. What's in the model but not built yet, as an absolute clause or a jussive, is marked so.

## Why

`Statement` was the union of `Clause`, `Comparative`, `Addressed`, `Coordinated`, `Subordinated`, `Modalized` and `Question`, which sat at four different levels:

- **Categories of the clause, modeled as wrappers around it.** `Modalized` wraps a statement, though a modal is a category of the clause as negation is, and negation is already a field. `Comparative` wraps a clause whose predicate, a comment promises, is always `be` with an adjective.
- **Sentence type, split in two.** A command is `Clause.Force` being `Directive`, a question a `Question` around the clause, though the clause file glosses both as moods of the verb, `IMP` and `Q`. `Force`'s other case, `Declarative`, holds the tense, aspect and subject, so it's really finiteness, and `Directive(Number)` bakes the second person into the command.
- **The utterance's, modeled as a statement's.** `Addressed` is a vocative, and the fragment has an addressee of its own, so one thing has two homes.
- **Independent and dependent clauses as one type.** A subordinate clause, a complement and a relative clause are each a `Statement`, and each refuses what a dependent clause can't be.

The parser's refusals showed it. Each of these existed only because a type allowed what the grammar doesn't:

| Refusal | What it patches |
| --- | --- |
| A clause under another can't be a command, a question, or said to someone | Dependent clauses are `Statement`s |
| A relative clause can't be a command, a question, or said to someone | The same |
| A clause in object: can't be a command, a question, or said to someone | The same |
| A question can't be joined to another clause | Sentence type sits outside coordination as a wrapper |
| A command can't be a question | Sentence type is in two places |
| Only be takes a standard: | Comparison is a wrapper, not part of be's complement |
| A command can't take a modal | The modal is a wrapper, with no layer saying what it can go on |

Some refusals are real limits of what's built so far, like "a comparison can't be negated yet", and those stay. The ones above are things the types can't say now; the parser still says what's wrong with a line that tries.

## Principles

- **Meaning, not form.** The model says what a sentence means. What a language chooses when it says it stays out: finiteness and rank, word order, whether 'want' is a verb or an affix, whether an epistemic modal is personal or impersonal. `Syntax` decides these today, which is right, and `VerbComplex.Finiteness` is where finiteness belongs.
- **Each distinction once, at the level the literature puts it.** Sentence type is one category, Sadock and Zwicky's and König and Siemund's declarative, interrogative and directive. Tense and the modals sit at the layers Dik and Hengeveld give them, simplified. Clause combining follows Noonan for complements, Thompson, Longacre and Hwang for adverbial clauses, Keenan and Comrie and Andrews for relative clauses, and Haspelmath for coordination.
- **A dependency between fields is said by where the field is.** Where one thing can only go with another, as a tense can't go with a directive, the field goes in the case that has it, not beside a check.

## The layers

From the inside out:

### The event

What happens, and who and what take part: Hengeveld and Mackenzie's configurational property, and Noonan's event, which `ComplementKind.Event` already names.

```
record Event(Predicate Predicate, Argument Subject, ValueList<Oblique> Obliques, ValueList<Adverbial> Adverbs)
```

- **The subject is always there,** whatever the clause's type or rank. A directive's is 'you' unless it says otherwise, a hortative's 'we' and a jussive's anyone (see [Sentence types](#sentence-types)). A dependent clause whose subject is the one it depends on says so with an `Argument` case, `Shared`, which took the place of the `SharesSubject` flags on `ComplementClause` and `Subordinated`. Syntax says a shared subject as a pronoun where the clause is balanced and leaves it out where it's deranked, as it did before.
- **Comparison is part of what be says of its subject:** be's complement is an `Attribute`, one of which is `Compared(Adjective Adjective, Argument? Standard)`, in place of the `Comparative` wrapper there was. A question or a modal on a comparison then needs no nesting rule.
- **Phase stays as it is:** 'begin' and 'stop' are verbs with an event complement, folded into an inceptive or cessative where the language has one, which is the language's choice.

### The state of affairs

An event with what scopes over it, or several joined: Dik's and Hengeveld and Mackenzie's state of affairs. Negation, the situational modals and aspect are operators, each a case wrapping the state of affairs it scopes over, so the order they nest in is their scope.

```
union StateOfAffairs(Event, Coordinated<StateOfAffairs>, Negated, Modalized, Aspected)
record Negated(StateOfAffairs StateOfAffairs)
record Modalized(SituationalModality Modality, StateOfAffairs StateOfAffairs)   // ability, permission, obligation, advice
record Aspected(Aspect Aspect, StateOfAffairs StateOfAffairs)                   // perfective, imperfective, perfect, progressive, habitual
```

- **Scope is nesting.** 'You must not sing' is `Modalized(OBLIG, Negated(sing))` and 'you don't have to sing' `Negated(Modalized(OBLIG, sing))`, in place of today's `Modalized.Negative` flag. 'You can't eat and drink here', read as 'not both', is one `Negated(Modalized(ABIL, ...))` over the joined pair, and 'you can't eat and you can't drink here' joins two that each have one. These are operators at the layer they scope over, as Hengeveld's are, not wrappers at the wrong level, which is what the old `Modalized` was.
- **Operators stack.** 'Must be able to swim' is a modal over a modal, as many languages say, and 'I didn't not go' a negation over a negation, as colloquial English does. 'You should have gone' is a modal over the perfect, and 'she has had to leave' the perfect over a modal, as Russian *smog*, the perfective of *moč'* 'be able', is 'managed to'. 'Has been waiting' is the perfect over the progressive. Which stacks of aspect languages allow, as the perfective over the imperfective may be nonsense, is for the work on stacked aspects to settle; until it does, the parser refuses what Syntax can't say.
- **A state of affairs with no aspect has the unmarked one,** imperfective, as a clause without `PFV` or `IPFV` does today.
- **Only a situational modal goes here.** `Modality` has two kinds, `SituationalModality` and `EpistemicModality`, so an epistemic modal, which judges a proposition, can't be put on a state of affairs at all.
- **A directive holds a state of affairs,** so it can have an aspect, as Russian perfective and imperfective imperatives do, and a negation, which is a prohibitive. It can have a situational modal too, as 'be able to swim by summer!' does; the roadmap has a modal on a command as something to build, so Syntax refuses one until it is, as a limit of what's built rather than of the model.

### The proposition

A state of affairs placed in time, which can be true or false: Hengeveld and Mackenzie's episode and propositional content together, and Noonan's proposition.

```
record Proposition(StateOfAffairs StateOfAffairs, Tense Tense)
union Propositional(Proposition, Judged, Denied)
record Judged(EpistemicModality Modality, Propositional Propositional)   // dubitative or presumptive
record Denied(Propositional Propositional)                              // 'he can't have left'
```

- **Tense is here, not on the event,** since only a proposition has a time of its own. That's Noonan's split between independent and dependent time reference: an event complement's time is set by the verb it's the object of, a purpose clause's by its main clause, and a directive's by the act of telling.
- **An epistemic modal is a layer over the proposition,** since it judges it whole, tense and all, as 'he must have left' does.
- **The proposition's operators nest as the state of affairs' do,** so a negation over a judgment, as in 'he can't have left', which says it isn't possible that he left, is `Denied(Judged(DUB, ...))`, and a negation under one, as in 'he might not have left', is the state of affairs' own. Only one judgment, denied or not, is said yet.

### Sentence types

What an independent clause does: König and Siemund's sentence types, which each language marks in its own way.

```
union Clause(Declarative, Interrogative, Directive)
record Declarative(Propositional Propositional)
record Interrogative(Propositional Propositional)      // yes-no with no question word, a content question with one
record Directive(StateOfAffairs StateOfAffairs)        // no tense, since its time is the telling's
```

- **Each type holds the layer it needs,** so a directive can't have a tense and a declarative can't lack one, with no check.
- **A content question is one with a question word in a role,** as now. 'Why' and 'how' become question words among the adverbs, a `QuestionAdverbial`, in place of `Question.Adverb`, so a question is only its type. The question words that fill roles are `InterrogativeWord`, which was `Interrogative` before the sentence type took the name.
- **A directive is any person's.** 'Go!' has a second person subject, 'let's go' a first person inclusive one, and 'let him go' a third person one, which WALS 72A counts as imperative-hortative systems: some languages have a form for each person, some only the imperative and say the rest with a word like 'let' or with the subjunctive, as Latin *eat* 'let him go' does. The model has them all today; saying them is a generator stage of its own (see [Roadmap items](#roadmap-items)).
- **An optative,** a wish, as in 'may you live long', and an **exclamative**, as in 'how tall you are!', would be further types. Grammars differ on whether a wish is a directive in the third person, so the union leaves room rather than deciding.

### Clause combining

Independent clauses joined, and dependent clauses under them.

```
union Sentence(Clause, Coordinated<Sentence>, Subordinated)
record Coordinated<T>(Conjunction Conjunction, T First, T Second)
record Subordinated(Sentence Main, AdverbialClause Clause)

union AdverbialClause(Purpose, Circumstance)            // Thompson, Longacre and Hwang's adverbial clauses
record Purpose(StateOfAffairs StateOfAffairs)          // its time is set by the main clause's
record Circumstance(Relation Relation, Propositional Propositional)   // if, when, before, after, until, because

//not built yet
record Absolute(StateOfAffairs StateOfAffairs, Taxis Taxis)   // 'the work done, we left', a third AdverbialClause
enum Taxis { Anterior, Simultaneous, Posterior }
```

- **Sentences joined each have their own type,** so 'I'm leaving, but will you come?' is a declarative and an interrogative joined.
- **States of affairs join too, under one sentence type and one tense,** which Haspelmath separates from joining clauses, and which Hengeveld and Mackenzie's episode is: states of affairs sharing their time. A joined state of affairs is a case of `StateOfAffairs`, so operators can scope over the whole or over each (see [The state of affairs](#the-state-of-affairs)).

  'She turns and walks away' is one declarative over two events sharing a subject; 'sit down and eat' is one directive, as Turkish *otur-up ye* 'sit-CVB eat.IMP' has it; 'you can't eat and drink here', read as 'not both', is one modal over two events, and 'he didn't eat or drink' one negation over them, which many languages say as 'neither … nor'. Languages that chain clauses, putting tense only on the last verb as Turkic and Papuan ones do, or that serialize verbs, as Yoruba and Mandarin do, say a joined state of affairs that way; the rest say each event, and may leave out what they share. Each event keeps its own subject, so events with different subjects can share a tense, which is what switch reference marks in Papuan languages: whether the next event's subject is the same. Sentences are joined where the conjuncts differ in type or tense, and events where they share both.
- **An absolute clause's time is a relative tense,** its taxis, as Jakobson and Comrie have it: before, at the same time as or after the main clause's, and nothing else of its relation to it, which can be temporal, causal or concessive. It isn't an aspect, which has no posterior, as Latin *moriturus* 'about to die' needs, and is a state of affairs' own: an absolute can be simultaneous and still perfective. Russian says taxis with aspect, as *pročitav* 'having read' and *čitaja* 'while reading' do, Latin and Greek with the tense of a participle, and Turkish with converbs like *-ken* 'while', which are each a language's form for it.
- **A dependent clause holds a state of affairs or a proposition, never a sentence type,** since it does nothing of its own: an embedded 'that' clause asserts nothing. So none of the refusals above is needed.
- **The main clause can be anything,** as 'if you hear the song, cover your ears' has a directive.
- **A purpose clause is a kind apart,** since it alone has no time of its own, so `Circumstance` takes a `Relation`, the subordinators but purpose. A language's markers stay keyed by `Subordinator`, purpose among them, since its semantic map joins purpose to the others, as Turkish *için* is both 'in order to' and 'because'; Syntax maps each relation and a purpose clause to their subordinator when it says them.

Complements and relative clauses are dependent clauses too, in the roles and noun phrases they fill:

```
union Complement(Said, Wanted)                         // Noonan; in place of ComplementClause
record Said(Proposition Proposition)                   // what's said, known or seen
record Wanted(StateOfAffairs StateOfAffairs)           // what's wanted, tried, begun or told to be done
record NounPhrase(Nominal Head, Proposition? Relative) // with a Gap in the relative's role, as now
```


### The utterance

What a line says, and who it's said to: the discourse level, above the sentence.

```
union Utterance(Message, Call)
record Message(Content Content, NounPhrase? Addressee)
record Call(NounPhrase Addressee)                       // 'stranger!', someone called with nothing else said
union Content(Sentence, Fragment)
union Fragment(Standalone, Oblique, Heading, Interjection, Answer)
record Quotation(Utterance Utterance)
```

- **The addressee is here once,** for a sentence or a fragment, in place of the `Addressed` statement and the fragment's own addressee there were. A fragment is the union of its parts.
- **A call has an addressee and nothing else,** and a message has content and maybe an addressee, so the types can't say an utterance with neither.
- **A heading still can't have one,** which stays a check, since it's a fact about headings rather than a type that allows too much.
- **A quotation holds an utterance,** as it does now, so it can be any of these.

## The whole

```
Utterance = Message(Content, Addressee?) | Call(Addressee)
  Content = Sentence | Fragment
    Sentence = Clause | Coordinated<Sentence> | Subordinated(Sentence, AdverbialClause)
      Clause = Declarative(Propositional) | Interrogative(Propositional) | Directive(StateOfAffairs)
        Propositional = Proposition | Judged(EpistemicModality, Propositional) | Denied(Propositional)
        Proposition(StateOfAffairs, Tense)
          StateOfAffairs = Event | Coordinated<StateOfAffairs>
                         | Negated(StateOfAffairs) | Modalized(SituationalModality, StateOfAffairs) | Aspected(Aspect, StateOfAffairs)
            Event(Predicate, Subject, Obliques, Adverbs)
      AdverbialClause = Purpose(StateOfAffairs) | Circumstance(Relation, Propositional) | Absolute(StateOfAffairs, Taxis)
  Fragment = Standalone | Oblique | Heading | Interjection | Answer
in an event's roles:  Said(Proposition) | Wanted(StateOfAffairs) | Quotation(Utterance)
in a noun phrase:     a relative Proposition with a Gap
```

Every layer that can be a dependent clause is a layer below the sentence type, which is what keeps dependent clauses from being commands or questions without saying so.

## Roadmap items

The model has to take these without another redesign. For each, where it goes and what's still to build:

| Item | Where it goes | Still to build |
| --- | --- | --- |
| Questions inside clauses, as in 'did anyone see what happened?' | A third complement, `Asked(Proposition)`, an interrogative clause type with no question's force, as König and Siemund separate the two | Its marking, 'whether' as 'if' or as the yes-no particle |
| Jussives and hortatives, as in 'let's go' | A directive with any subject, already | A generator stage after WALS 72A; the glosser stops refusing 'let's go' |
| Absolute clauses, as in 'the work done, we left' | `Absolute`, a state of affairs and its taxis, a third `AdverbialClause` | The case, and its marking, as a participle, a converb or an aspect |
| Existentials, as in 'there is a fallen tree upstream' | A predicate of its own, existing, beside be's | The predicate, and how languages say it |
| Insubordination, as in 'If you come back.' | A `Sentence` case holding an `AdverbialClause` with no main clause | The case; the glosser stops refusing it |
| Experiencers, as in 'I like it' or Spanish *me gusta* | An event's participant, in a role of its own | The role, and verbs that take one |
| Predicative possession, as in 'I have a book' | A predicate of its own, having, which a language says as 'have', 'at me is' or the others WALS 117A counts | The predicate and its marking |
| A situational modal with a perfect, as in 'you should have gone' | A modal over the perfect, already, and the perfect over a modal, as in 'she has had to leave' | Saying an auxiliary chain where both are verbs |
| Comparison with a clause as its standard, as in 'warmer than it should be' | `Compared`'s standard as a proposition, beside a noun phrase | The standard's case, and ellipsis in it |
| Comparison of adverbs and inside a noun phrase | `Compared` for a manner and for an adjective in a noun phrase | The same, in those places |
| Dropping a repeated subject, as in 'she turns and walks away' | A joined `StateOfAffairs`, built: each event said at the shared tense, a shared subject once | Chaining, serializing and switch reference; a negation, a modal or a question over joined events |
| A negated comparison, or one with a modal | `Negated` and `Modalized` over it, already | Saying them |


## How it's read and said

- **The clause file** writes the layers the way its glosses already did: mood on the verb (`IMP`, `Q`), modals as features, and comparison on the adjective. The order of a verb's glosses is scope, the innermost first, as the Mirror Principle has it: `sing.NEG.OBLIG` is 'must not sing' and `go.OBLIG.NEG` 'doesn't have to go'. Tense, mood and an epistemic modal belong to the layers above, so where they're written doesn't matter, but for a negation after an epistemic modal, which denies the judgment, as `leave.PRF.DUB.NEG` is 'can't have left'. Stacked modals and aspects, as `swim.ABIL.OBLIG` for 'must be able to swim' or `leave.OBLIG.PRF` for 'has had to leave', fit the model and are refused until Syntax can say them, as is a directive to anyone but 'you'. Joined events are written as [the clause format](clause-format.md#joined-events) describes.
- **The parser** builds the layers, nesting operators in gloss order.
- **Syntax** says a dependent clause as a clause stating it, at its own time where it has one and at the present where the clause it's in sets its time, which the main clause's time would say more truly. It keeps everything it decides about form, and reads it from layers: finiteness from the sentence type and the clause's place, rank from the language, folding of 'want' and 'begin' into the verb. It collects what a clause's verb says from the layers around its event into a `Spine`: the tense, the aspect, the negation, and a situational modal with its own, refusing layers stacked beyond what's built, as two aspects, the perfect over a modal, or a negation over a negation. An aspect is said the same on either side of a modal until stacked aspects are built. Joined events are each said as a clause at the shared tense.
- **Tests** build clauses through a few builders in `TestData`, as `LanguageDescription` builds its samples through its own.

## Names

The model retired `Statement`, `Force`, `Directive` as it stood, `Question`, `Modalized` as it stood, `Comparative`, `Addressed` and `ComplementClause`. It reuses `Directive` for the sentence type and `Modalized` for a modal over a state of affairs, which is what each term means. Settled:

- **`Sentence`** is clause combining's, and the realized output is `Rendering`, its words `RenderedWord`.
- **`Complement`** is a complement clause's, and be's complement is `Attribute`, what be says of its subject.
- **`AdverbialClause`** is an adverbial clause's, and `Adverbial` stays an adverb or a manner.
- **`Event` and `Proposition`** follow Noonan and `ComplementKind`, which they replace, and **`StateOfAffairs`** follows Dik and Hengeveld.

Open: the names in the names-to-revisit memory, `Done` for a passive's predicate among them, which could be settled at the same time.
