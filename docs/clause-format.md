# Clause format

`translate` reads a file of clauses and renders them in a generated language. Each clause is the English it means, then an indented line saying what it means in Leipzig glosses:

```
the hunter saw the wolves
  see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF
```

The verb comes first, then its arguments as `role:noun`. See [examples/scene.txt](../examples/scene.txt) for a whole file, and `ClauseFile` for the parser.

Words are English meanings, whatever their case, which the language coins roots for as needed.

Features left out take their unmarked values: present, imperfective, singular, and exclusive for 'we'. A value the language doesn't distinguish falls back on the nearest one it does, so a dual becomes a plural in a language without one.

Anything the generator can't express yet, like a relative clause, is an error naming the line, never silently dropped.

## Verbs

| Gloss | Meaning | Example |
| --- | --- | --- |
| `NEG` | Negates the clause | `see.PST.NEG` |
| `IMP` | A command, with no tense or aspect | `drink.IMP.NEG  object:wine` |
| `PASS` | Passive, with what's done to as its subject and `agent:` saying who does it | `carry.PASS  subject:light.DEF  agent:(figure.NDF small)` |
| `ABIL` | Its subject can, as in 'you can swim' | `swim.ABIL  subject:2SG` |
| `PERM` | Its subject may, or is allowed to | `go.PERM  subject:2SG` |
| `OBLIG` | Its subject must, or has to | `find.OBLIG  subject:2SG  object:key.DEF` |
| `ADVS` | Its subject should, or ought to | `rest.ADVS  subject:2SG` |
| `Q` | A yes-no question | `see.PST.Q  subject:2SG  object:3PL` |

A command has no subject, unless it's `subject:2PL` for several people.

### Modals

`NEG` after a modal negates the modal, and `NEG` before it negates the verb, the way glosses put a morpheme outside what it applies to:

| Written | Means |
| --- | --- |
| `swim.ABIL.NEG  subject:2SG` | 'you can't swim' |
| `sing.NEG.OBLIG  subject:2SG` | 'you must not sing' |
| `go.OBLIG.NEG  subject:2SG` | 'you don't have to go' |
| `follow.NEG.ADVS  subject:2SG  object:3PL` | 'you shouldn't follow them' |

'Could' is `ABIL.PST`. A modal works with `be` and `PASS` too, as in `be.OBLIG  subject:2SG  adjective:careful`, but not with a command or a comparison. Other modals, like 'might' and 'maybe', can't be said yet.

A language marks each modal with an affix, a word of its own or a verb, as WALS 74A counts them. A modal verb takes the tense and agreement, with the verb bare beside it, and is glossed by what it means, `can`, `may`, `must` or `should`, while an affix or a word of its own is glossed `ABIL`, `PERM`, `OBLIG` or `ADVS`. The Leipzig rules have none of these; the first three are what grammars commonly use, and `ADVS`, for advisability, is coined here, since grammars have no common gloss for 'should'.

### Questions

A yes-no question has `Q` on its verb. A question asking who, what, where or why has no `Q`, and its question word goes in the role it asks about. `why` fills no role, so it's written on its own:

| Written | Means |
| --- | --- |
| `see.PST.Q  subject:2SG  object:3PL` | 'did you see them?' |
| `swim.ABIL.Q  subject:2SG` | 'can you swim?' |
| `tell.PST  subject:who  object:2SG` | 'who told you?' |
| `want  subject:3PL  object:what` | 'what do they want?' |
| `be  subject:key.DEF  location:where` | 'where is the key?' |
| `get.PST  subject:2SG  object:3SG.N  source:where` | 'where did you get that?' |
| `come.PST.NEG  subject:2SG  why` | 'why didn't you come?' |

`where` asks about a place, so it's `location:where`, `goal:where` for 'where to' or `source:where` for 'where from'. A question asks one thing, and its question word fills a role directly, not inside a noun phrase, as 'whose' would, or a coordination. Questions with 'when', 'how' or 'which', `noun:who` for 'who are you?', and a question joined to or under another clause can't be said yet.

How a language marks a yes-no question follows WALS 116A: a particle, which goes first, last or after the first word as WALS 92A counts them for OV and VO languages; an affix on the finite verb, outside everything else on it; the finite verb first, as German does; or intonation alone, which the text doesn't show. A content question puts its question word first, with the rest of its phrase, in the share of languages WALS 93A counts for each word order, as English does, and otherwise leaves it in place, as Mandarin and Japanese do, with `why` where obliques go. The particle and affix are glossed `Q`, as the Leipzig rules gloss a question marker, and the question words by what they ask, `who`, `what`, `where` and `why`. A question word takes its role's case and adposition as a noun would, so 'where from' can be 'where-ABL'.

### `be`

The verb `be` says what its subject is, with `noun:fool` or `adjective:big` in place of an object, or where it is, with `location:house`.

`be.IMP` says what or where to be, as in `be.IMP  adjective:careful`.

### Comparison

With an adjective, `be` can compare its subject: `CMPR` on the adjective says 'more', and `standard:` what the subject is compared with, if anything is.

| Written | Means |
| --- | --- |
| `be  subject:marta  adjective:tall.CMPR  standard:man.PL.DEF` | 'Marta is taller than the men' |
| `be  subject:road.DEF  adjective:long.CMPR` | 'the road is longer' |
| `shine  subject:3SG.N  like:water` | 'it shines like water' |

`CMPR` goes only on the adjective `be` says of its subject, not on one in a noun phrase or joined to another, and a comparison can't be negated, take a modal or be a command yet. `like:` says what something is or does something like, with any verb but `be`.

How a language marks the standard follows WALS 121A: marked like a place, mostly 'from', as in Turkish ('tall from the men'); with a particle like English *than*; as the object of a verb meaning 'exceed', as in Yoruba ('he is big, exceeds the elephant'); or as two clauses, 'Marta is tall, the men are not tall'. Every language marks a comparative adjective with no standard, with a word or an affix glossed `CMPR`, as Turkish says *daha büyük* 'more big', and some also mark it beside a standard, as English *-er* does. A degree word goes before the adjective in a language whose adjective phrases are head-final and after it in one whose are head-initial, which leans with the order of verb and object as WALS 83A and 91A count together. 'Like' is a word on the side of the phrase the language's adpositions go, or an affix on its edge glossed `SIM`.

### Adverbs

A clause can say when, how often, how and for how long, with any number of each:

| Written | Means |
| --- | --- |
| `wake.PST  subject:2SG  when:later  manner:sudden` | 'later, you woke suddenly' |
| `start.PST  subject:3PL  often:again` | 'they started again' |
| `climb  subject:2SG  manner:careful` | 'you climb carefully' |
| `sleep.PST  subject:3PL  time:(night last)` | 'they slept last night' |
| `stand.PST  subject:1SG  duration:(year.PL 200)` | 'I stood for two hundred years' |
| `be  subject:1SG  adjective:tired.INTS` | 'I am very tired' |
| `walk  subject:3SG  manner:slow.INTS` | 'she walks very slowly' |

`when:` is a word of its own saying when, like *now*, *then* or *yesterday*, and `often:` one saying how often or whether something still goes on, like *again*, *always* or *still*; the language coins a root for each. `manner:` says how, as an adjective the language makes into an adverb its own way, so 'carefully' is `manner:careful`, 'suddenly' `manner:sudden` and 'well' `manner:good`. Which role an adverb is in says how near the verb it goes, so nothing has to know English adverbs by name. `time:` and `duration:` take a noun phrase, as other roles do, and `be` takes them too. `INTS` on any adjective says 'very', or 'much' with `CMPR`, as `small.CMPR.INTS` is 'much smaller'.

Words for where, like *here*, *there* and *inside*, can't be said yet, and neither can *ago* or *later* after a noun, a manner that compares, as in 'more slowly', or a question asking when, how or for how long.

Adverbs and manners go where obliques go, ordered as described under [Modifier order](#modifier-order). An adverb is its bare root. A manner adverb is the adjective itself, as German *schnell* is 'quick' and 'quickly'; the adjective with an affix, as English *-ly* and Turkish *-ce* make one, glossed `ADV` as the Leipzig rules have it; or the adjective with a word beside it, as Mandarin *de* and Japanese *ni*, glossed `ADV` too. WALS doesn't count these, so the odds are guesses. A time is marked like a place in most languages, sharing the location's case or adposition, and otherwise by an adposition of its own glossed `during`. A duration is marked like the object, as Latin *duos annos* 'for two years' is accusative, like a time, or by an adposition glossed `for`. 'Very' is a word on the side the language puts degree words, the comparative's side where that's a word too, and is glossed `INTS`, as grammars commonly gloss an intensifier.

### Modifier order

Languages largely agree on which modifiers go nearest their head, and differ mostly in which side they put them, so the order on one side mirrors the order on the other. `ModifierOrder` says this once for every language. Everything where obliques go is ranked by how near the verb it goes, nearest first:

1. what the verb calls for: `recipient:`, `goal:`, `source:`, `agent:`, and a `standard:` in a VO language
2. how: `manner:`, `like:`, `instrument:`, `companion:`
3. where: `location:`
4. how often or how long: `often:`, `duration:`
5. when: `when:`, `time:`
6. why: `why`

An OV language puts a `standard:` right before the adjective instead, as Japanese *Taroo yori takai* 'Taro-from tall' and Turkish *kedi-den büyük* 'cat-from big' do, since Dryer finds the order of adjective and standard follows the order of object and verb.

After the verb they go nearest first, as English says 'she sang beautifully in the hall every night'; before it they're mirrored, as Japanese and Turkish say 'every night in the hall beautifully sang', so the nearest is always beside the verb. Those of one rank keep the order they're written in, so the order a clause file writes them in matters only within a rank.

Adjectives are written in English order, which already puts the nearest last, so they need no ranks: before the noun they keep it, and after it most languages mirror it, so 'big gray wolves' is 'wolves gray big', as in Welsh and Indonesian. A quarter keep the English order after the noun instead, as some Romance does; WALS doesn't count this, so that's a guess from Cinque's survey.

## Roles

| Role | Says |
| --- | --- |
| `subject:` | The subject |
| `object:` | The object |
| `noun:`, `adjective:` | What the subject of `be` is |
| `location:` | Where |
| `goal:` | To where |
| `source:` | From where |
| `recipient:` | To whom |
| `companion:` | With whom |
| `instrument:` | With what |
| `like:` | Like what |
| `time:` | When |
| `duration:` | For how long |
| `agent:` | Who does it, in a passive |
| `standard:` | What the subject of `be` is compared with |
| `addressee:` | Who the clause is said to |
| `when:` | When, as a word of its own |
| `often:` | How often, or whether still, as a word of its own |
| `manner:` | How, as an adjective |

Any clause can be said to someone, with `addressee:marta` or `addressee:(man old)`, a bare noun that comes first in the vocative case where the language has one. A command to a plural addressee, as in `addressee:child.PL`, is to all of them.

## Noun phrases

A noun is definite with `DEF`, as in `subject:dog.DEF` for 'the dog', and indefinite with `NDF` for 'a dog'. With neither it's bare, as 'dogs', 'water' and 'my dog' are.

Anything else that goes with a noun goes in parentheses, noun first:

| What | Written | Means |
| --- | --- | --- |
| Adjectives | `object:(wolf.PL big gray)` | 'big gray wolves' |
| A number, in digits | `subject:(wolf 3)` | 'three wolves' |
| An ordinal, in digits with its English ending | `subject:(day 3rd)` | 'third day' |
| A possessor | `object:(house possessor:king)` | 'the king's house' |
| A pronoun as possessor | `object:(dog.PL possessor:3SG.F)` | 'her dogs' |
| A possessor with its own | `subject:(son possessor:(sister possessor:1SG))` | 'my sister's son' |

A number makes the noun dual for two and paucal for a few, unless it says otherwise.

## Coordination

Noun phrases joined by 'and' or 'or' go in parentheses with the conjunction first, and each of them can be anything a role takes, including another noun phrase in parentheses:

| Written | Means |
| --- | --- |
| `subject:(and dog.DEF cat.DEF)` | 'the dog and the cat' |
| `object:(and (purse.NDF empty) sword.NDF)` | 'an empty purse and a sword' |
| `object:(or rope.NDF boat.NDF)` | 'a rope or a boat' |
| `subject:(and 2SG 1SG)` | 'you and I', which a verb agrees with as 'we' |
| `adjective:(and warm crowded)` | 'warm and crowded', said of the subject of `be` |

Clauses joined by 'and', 'or' or 'but' stay one entry: the first clause, then each joined one on an indented line of its own starting with its conjunction:

```
hunters sit by the fire and argue
  sit  subject:hunter.PL  location:fire.DEF
  and  argue  subject:hunter.PL
```

'But' joins only clauses.

## Subordinate clauses

A clause under another goes on an indented line of its own after the main clause, starting with what it says. The language decides which comes first, so the main clause is always written first, whatever order the English has:

```
if you hear the song, cover your ears
  cover.IMP  object:(ear.PL possessor:2SG)
  if  hear  subject:2SG  object:song.DEF
```

| Starts with | Says |
| --- | --- |
| `if` | On the condition that |
| `when` | At the time that |
| `before` | Before |
| `after` | After |
| `until` | Up to the time that |
| `because` | For the reason that |
| `purpose` | In order to |

A purpose clause can leave out its subject when it's the main clause's, as in `purpose  cross  object:river.DEF` for 'to cross the river'. A clause under another can't be a command or have an addressee of its own.

## Pronouns

Pronouns are a person and number, as in `subject:1SG` or `object:1PL.INCL`.

## Noun files

A noun file sits beside the clause file, or wherever `--nouns` says, and tells the language what its nouns name, so that gender can follow from it. It has one noun to a line:

```
marta F
miller HUM
crow ANIM
river THING
```

`M` and `F` are for words that themselves mean a male or a female being, and for names, `HUM` is any other person, `ANIM` any other creature, and `THING` the rest.

This says what a word means, not what gender it has, since one clause file can go into any language, and each language decides what to make of it. It never changes a root.

The file is optional, but a noun that isn't in it counts as a thing, so a text without one gets gender only from form. `translate` reads `hollow.nouns.txt` for `hollow.clauses.txt` without being told.
