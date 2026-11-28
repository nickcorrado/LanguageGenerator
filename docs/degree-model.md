# The degree model: a design

A proposal for what comparison and the other degrees of a property mean before any language says them, as the [clause model](clause-model.md) is for the clause: the types `ClauseFile` builds and `Syntax` realizes. It replaces `Compared`, `Attribute`'s case for a comparison, and `Adjective.Intensified`. Nothing here is built yet, and [What's built first](#whats-built-first) says what a first change would build and what it would leave for later.

## Why

A comparison is `Compared(Adjective Adjective, Argument? Standard)`, one of the things be can say of its subject. That was the right step from the `Comparative` wrapper there was, but it's comparison in one place, where Stassen's types count it, and not comparison as such:

- **Only be's adjective has a degree.** An adjective in a noun phrase and one a manner is made from can't be compared at all, so *stronger arms than yours* and *slower than before* are refused. The parser refuses `CMPR` everywhere but on be's adjective, and the refusal patches what the type can't hold.
- **A standard is only a noun phrase.** *Warmer than it should* and *slower than before* compare with what a clause says, and are refused.
- **'Very' and 'much' are one flag.** `small.CMPR.INTS`, 'much smaller', is a comparative of an intensified adjective, said with the language's word for 'very'. Many languages tell them apart, as German *sehr groß* 'very big' and *viel größer* 'much bigger' do, and French *très* and *beaucoup plus*, and some don't, as Turkish *çok* is both.
- **A comparison is the only degree.** Equatives, superlatives, 'too' and 'enough' have nowhere to go, and neither does the 'so' of *so close that you could touch them*, which Hollow glosses as 'very close'.

## Principles

The clause model's, which this follows:

- **Meaning, not form.** How a language spreads a comparison over its clause, as a standard marked like a place, a verb 'exceed' or two clauses, is its choice, and Syntax's to say. The model says what's compared with what, on what scale.
- **Each distinction once, at the level the literature puts it.** The parts of a comparison follow Dixon's and Haspelmath's, degree and standard follow the degree semantics of Kennedy and Heim, the standard's clause follows Bresnan's comparative deletion, and the copula-like verbs follow Dixon.
- **A dependency between fields is said by where the field is.**

## The parts of a comparison

Dixon, and Haspelmath for equatives, name five parts, in *Marta is taller than the men*:

| Part | In the example | Where it goes |
| --- | --- | --- |
| Comparee, what's compared | *Marta* | Not written: what the adjective is said of |
| Parameter, the property | *tall* | The adjective |
| Degree marker | *-er* | The adjective's grade |
| Standard marker | *than* | The language's, as WALS 121A types it |
| Standard, what it's compared with | *the men* | The grade |

The degree semantics of Kennedy and Heim says the same thing: a gradable adjective measures something on a scale, a comparative relates two points on that scale, and the standard says the second point. The standard is an argument of the comparative, and in syntax the *than* phrase is the degree word's complement, so both put it with the adjective's degree, not with the clause.

Stassen's types are about form, how a language says a comparison across its clause, and WALS 121A counts them for an adjective be says of its subject. A language's type still says the comparison wherever its adjective is: see [How it's read and said](#how-its-read-and-said).

## The grade

```
record Adjective(string Meaning, Grade? Grade)       // a plain adjective has none
union Grade(Intensified, Comparative, Equative, Superlative, Excessive, Sufficient, Consecutive, DegreeGap)
```

Every adjective has a grade or none, wherever it is: what be says of its subject, one of a noun's adjectives, a manner's, or one of a coordination's. The comparee is what the adjective is said of: be's subject, the noun, or for a manner the event's subject, so *she runs faster than me* compares her with me.

### Intensified

```
record Intensified                         // 'very big'
```

'Very', one of Bolinger's degree words. The others, moderators like 'rather' and 'quite', diminishers like 'a little', and maximizers like 'completely', are cases beside it, not built yet.

### Comparative

```
record Comparative(Direction Direction, Standard? Standard, Differential? Differential)   // 'bigger (than the house)', 'much bigger'
enum Direction { More, Less }
```

- **A standard or none.** With none, the comparative is 'more' than something already known, as *the moss is thicker* is.
- **More or less.** Stassen leaves comparatives of inferiority out of his typology, and many languages have none, saying 'not as big' or 'smaller' instead. *Less* is in the model and not built, which is where the roadmap has it now.
- **The differential** says by how much, below.

### Equative

```
record Equative(Standard? Standard)        // 'as big as the house'
```

Haspelmath's equatives, which mark the parameter, as *as big*, and the standard, as *as the house*, and very often borrow the standard's marker from the similative's 'like'. 'Not as big as' is a negation over it.

### Superlative

```
record Superlative(Direction Direction, Argument? Domain)   // 'the biggest (of the houses)', 'the least'
```

The domain is what it's the most of, as *of the houses* or *in the village* is. Bobaljik argues a superlative's form contains the comparative's, as Czech *nej-větší* 'most-bigger' shows it, and many languages say it as a comparative whose standard is everything else, 'bigger than all', which is a choice of form, not a difference in meaning. Szabolcsi's and Heim's relative readings, as in *who climbed the highest mountain?*, where the mountain is higher than anyone else climbed, aren't provided for here; they compare the clause's participants rather than the mountains, and would want a domain of their own.

### Excessive, sufficient and consecutive

```
record Excessive(StateOfAffairs? Prevented)   // 'too heavy (to lift)'
record Sufficient(StateOfAffairs? Allowed)    // 'big enough (to hold it)'
record Consecutive(Propositional Result)      // 'so close that you could touch them'
```

Meier's degree constructions with a clause: 'too' and 'enough' say what the degree rules out or allows, a clause whose time the main clause sets, as a purpose clause's is, so it's a state of affairs; 'so … that' says what came of the degree, at a time of its own, so it's a propositional, as a `Circumstance` is. 'So' with no clause, as in *I'm so tired*, is a degree word like 'very'.

### The degree gap

```
record DegreeGap                           // the degree a standard's clause says
```

Only in a standard that's a clause, below.

## The standard

```
union Standard(Argument, Propositional)    // 'than the men'; 'than it should be', with a DegreeGap
```

- **A noun phrase,** a phrasal standard, compares the same property of something else: *the men*'s height.
- **A clause,** a clausal standard, says a degree its own way: *than it should be*, *than before*, *than I expected*. The clause has a gap where its degree goes, the `DegreeGap` grade on its own gradable word, as a relative clause has a `Gap` where its noun goes. Chomsky analyzes comparatives as the same movement relatives have, and Bresnan's comparative deletion is what then leaves the gradable word out.
- **The standard's clause keeps its own adjective,** because it can differ from the comparison's: *the table is longer than it is wide* is a subcomparative, which compares two properties on one scale. Where the two are the same, as they nearly always are, leaving the second out is form, and Syntax's to do.
- **Both kinds overlap,** as *taller than the men* and *taller than the men are* mean the same thing, as an adjective and a relative clause saying it do. Languages tell them apart, as English *than me* and *than I am* are, and many have only the phrasal kind, so both stay, and the clause file writes what the English says.

A gap and a standard depend on each other the way a relative clause and its gap do: exactly one `DegreeGap` in a standard's clause and none elsewhere. The parser checks it, as it checks a relative clause's `Gap`; the types can't say 'one hole' without a clause type for each place a hole can be.

## The differential

```
union Differential(Much, Little, Measure)   // 'much bigger', 'a little bigger', 'two years older'
record Measure(NounPhrase Amount)
```

By how much the comparee exceeds the standard, which the degree semantics calls the differential, apart from the degree words of a plain adjective. *Much* is what `CMPR.INTS` glosses today.

## Copula-like verbs

Dixon's copula-like verbs say an attribute of their subject as be does, with a meaning of their own: *the moss grows thicker*, *you look terrible*, *your footsteps sound very loud*, *it feels warmer than it should*. Hollow says each of these with be today, losing the verb, and says *tastes like a wet boot* and *smells of beer* with verbs of their own and a `like:`, which are the same verbs.

```
record Being(Attribute Attribute, Copula Copula)
union Copula(Plain, Becoming, Remaining, Appearing)
record Appearing(Sense? Sense)             // 'seem', or 'look', 'sound', 'feel', 'smell', 'taste'
enum Sense { Sight, Hearing, Touch, Smell, Taste }
```

- **Becoming and remaining** are the change into a state and its staying, *grow* and *become*, *stay* and *remain*. Many languages say becoming with an inchoative on the adjective or the copula, as Turkish *kalın-laş* 'thick-become' does, which is form.
- **Appearing** is Viberg's source-based perception, where the subject is what's perceived, as in *it looks*, against the experiencer-based *I see it*. Viberg's hierarchy of the senses, sight before hearing before touch before smell and taste, says which senses a language gives verbs of their own: a lower sense more often shares a higher one's verb. 'Seem' has no sense.
- **The sense is a field of `Appearing`,** not beside the copula, since only appearing has one.

This is a change to be's predicate, apart from grade, and only one of Hollow's comparisons needs it. The design puts it here because *it feels warmer than it should* needs both.

## The whole

```
Adjective(Meaning, Grade?)                in be's Attribute, a noun's adjectives, a manner, an adjective coordination
  Grade = Intensified
        | Comparative(Direction, Standard?, Differential?)
        | Equative(Standard?)
        | Superlative(Direction, Domain?)
        | Excessive(StateOfAffairs?) | Sufficient(StateOfAffairs?) | Consecutive(Propositional)
        | DegreeGap                        in a standard's clause only
    Standard = Argument | Propositional, with one DegreeGap
    Differential = Much | Little | Measure(NounPhrase)
Being(Attribute, Copula)
  Copula = Plain | Becoming | Remaining | Appearing(Sense?)
```

`Attribute` loses `Compared`. Similes stay as they are: *like water* is the similative role, likeness in manner or kind, which Haspelmath separates from an equative's likeness in degree, and *she is like her mother* is be with that oblique as its attribute.

## Roadmap items

What the model has to take without another redesign, beyond what's built first:

| Item | Where it goes | Still to build |
| --- | --- | --- |
| Equatives, 'as big as' | `Equative`, a grade | A stage after Haspelmath's equative types, its standard marker often the similative's |
| Superlatives, 'the biggest of the houses' | `Superlative`, a grade, with its domain | A stage for its marker, an affix built on the comparative's or a word, or 'bigger than all' |
| 'Too', 'enough' and 'so … that' | `Excessive`, `Sufficient` and `Consecutive`, grades | Their markers, and the clause each takes |
| 'Less' and 'least' | `Direction.Less` | Its marker, or 'not as' where a language has none |
| 'A little bigger', 'two years older' | `Differential`'s `Little` and `Measure` | Their marking, as a measure is often in a case of its own |
| A language's own 'much' | `Much` | A word apart from 'very' in the languages that have one, which today say 'much' with 'very' |
| 'Rather', 'a little', 'completely' on a plain adjective | Cases of `Grade` beside `Intensified` | Bolinger's degree words |
| Copula-like verbs, 'grows thicker', 'looks terrible' | `Being`'s `Copula` | Their verbs, an inchoative where becoming is one, and Viberg's sharing of senses |
| Ellipsis in a standard's clause, 'than before' for 'than they started before' | Nothing new | Leaving out what the standard's clause shares with the comparison's, as English does |
| Relative superlatives | A domain of the clause's participants | The domain |
| Comparing amounts, 'more bread', 'eats more' | A quantifier and an adverbial, Bresnan's 'much' compared | Both |
| Comparing gradable adverbs, 'sooner', 'more often' | A grade on the adverb | Which adverbs are gradable |

## What's built first

Comparatives as they should be, wherever an adjective is:

- **The model:** `Grade` on `Adjective`, with `Intensified`, `Comparative` with its standard and the `Much` differential, `Standard` and `DegreeGap`. `Compared` and `Adjective.Intensified` go. The other grades, `Direction`, `Little` and `Measure` wait for the change that says them, as the [roadmap items](#roadmap-items) list, so no type lands with nothing to build it.
- **Lifted refusals:** *stronger arms than yours*, *slower than before* and *warmer than it should*, the last said with be until copula-like verbs are built, as Hollow says *you look terrible* today.
- **Everything else in the output stays,** which the same checks the clause model's steps had would show: `CMPR.INTS` is `Much`, said with 'very' as today.

Equatives, superlatives, the clause-taking grades and copula-like verbs each need a stage of their own and typology of their own, so each is a change of its own, and moves to [the roadmap](roadmap.md) once this is built.

## How it's read and said

### The clause file

- **Grades are glosses on the adjective,** as `CMPR` and `INTS` are: on be's `adjective:`, a noun's adjective, and a `manner:`. `INTS` is 'very' on a plain adjective and 'much' on a comparative, so `small.CMPR.INTS` reads as it does now.
- **`standard:`** at the clause's level goes with the clause's one comparative, be's adjective or a manner. In a noun phrase, inside its parentheses, it goes with the noun's comparative: `(arm.PL strong.CMPR standard:(arm.PL possessor:2SG))`.
- **A standard that's a clause** is in brackets, with `_` for its gradable word where it's the comparison's: `standard:[start  subject:3PL  manner:_  time:before]`, `standard:[be.ADVIS  subject:3SG.N  adjective:_]`. How a subcomparative writes its own adjective is open; Hollow has none.

### Syntax

- **The degree marker** goes on the adjective as it does now, wherever the adjective is. In a manner, it's next to the root, inside the marker that makes the adverb.
- **A phrasal standard** is marked by the language's type, as now, and goes where the adjective's position puts it:

| Type | Be's adjective | In a noun phrase | A manner |
| --- | --- | --- | --- |
| Locational | The role's marker, as now | The same, on the adjective's side as its modifiers go | The same, where obliques go |
| Particle | The particle, the standard in the plain case | The same | The same |
| Exceed | 'Exceed' and the standard as its object | A relative clause that compares, 'arms that are strong, exceed yours' | 'Exceed' after the verb, as a serial verb is |
| Conjoined | Two clauses, as now | A relative clause that compares, with the clause denying it | Two clauses, the second denying the manner of the standard |

  Exceed and conjoined comparisons are clause constructions, so inside a noun phrase they become a relative clause, said by the language's relativization.
- **A clausal standard** takes the type's marking too: after a particle as a balanced clause, as English *than it should* and Russian *čem* have it; deranked in the standard's role in a locational language, as Turkish *bekle-diğ-im-den* 'expect-NMLZ-1SG-ABL' is 'than I expected' and Japanese *omotta yori* 'than (I) thought'; deranked as the object of 'exceed'; and in a conjoined comparison, the standard's clause itself, denied. Its gradable word is left out where it's the comparison's, and every other part said, as 'than they started before', until ellipsis is built.

## Names

Open:

- **`Grade`**, after the grades of comparison of older grammars, since `Degree` is the grammeme whose values `CMPR` glosses. The grammeme could be renamed instead, though the other grammemes are named for their categories as `Degree` is.
- **`Consecutive`**, the traditional grammars' name for a 'so … that' clause, and **`Prevented`** and **`Allowed`** for the clauses of 'too' and 'enough'.
- **`Plain`** for be itself among the copulas.
- **`Intensified` and `Comparative`** are also Syntax method names, which would shadow the types in patterns, as `Complement` did, so the methods would be renamed.
