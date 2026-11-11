# Clause format

`translate` reads a file of clauses and renders them in a generated language. Each clause is the English it means, then an indented line saying what it means in Leipzig glosses:

```
the hunter saw the wolves
  see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF
```

The verb comes first, then its arguments as `role:noun`. See [examples/scene.txt](../examples/scene.txt) for a whole file, and `ClauseFile` for the parser.

Words are English meanings, whatever their case, which the language coins roots for as needed.

Features left out take their unmarked values: present, imperfective, singular, and exclusive for 'we'. A value the language doesn't distinguish falls back on the nearest one it does, so a dual becomes a plural in a language without one.

Anything the generator can't express yet, like a question, is an error naming the line, never silently dropped.

## Verbs

| Gloss | Meaning | Example |
| --- | --- | --- |
| `NEG` | Negates the clause | `see.PST.NEG` |
| `IMP` | A command, with no tense or aspect | `drink.IMP.NEG  object:wine` |
| `PASS` | Passive, with what's done to as its subject and `agent:` saying who does it | `carry.PASS  subject:light.DEF  agent:(figure.NDF small)` |
| `ABIL` | Its subject can, as in 'you can swim' | `swim.ABIL  subject:2SG` |
| `PERM` | Its subject may, or is allowed to | `go.PERM  subject:2SG` |
| `OBLIG` | Its subject must, or has to | `find.OBLIG  subject:2SG  object:key.DEF` |

A command has no subject, unless it's `subject:2PL` for several people.

### Modals

`NEG` after a modal negates the modal, and `NEG` before it negates the verb, the way glosses put a morpheme outside what it applies to:

| Written | Means |
| --- | --- |
| `swim.ABIL.NEG  subject:2SG` | 'you can't swim' |
| `sing.NEG.OBLIG  subject:2SG` | 'you must not sing' |
| `go.OBLIG.NEG  subject:2SG` | 'you don't have to go' |

'Could' is `ABIL.PST`. A modal works with `be` and `PASS` too, as in `be.OBLIG  subject:2SG  adjective:careful`, but not with a command or a comparison. Other modals, like 'should', 'might' and 'maybe', can't be said yet.

A language marks each modal with an affix, a word of its own or a verb, as WALS 74A counts them. A modal verb takes the tense and agreement, with the verb bare beside it, and is glossed by what it means, `can`, `may` or `must`, while an affix or a word of its own is glossed `ABIL`, `PERM` or `OBLIG`, as grammars gloss them.

### `be`

The verb `be` says what its subject is, with `noun:fool` or `adjective:big` in place of an object, or where it is, with `location:house`.

`be.IMP` says what or where to be, as in `be.IMP  adjective:careful`.

With an adjective, `be` can compare its subject with a standard: `be  subject:marta  adjective:tall  standard:man.PL.DEF` is 'Marta is taller than the men'.

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
| `agent:` | Who does it, in a passive |
| `standard:` | What the subject of `be` is compared with |
| `addressee:` | Who the clause is said to |

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
