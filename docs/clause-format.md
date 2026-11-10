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

A command has no subject, unless it's `subject:2PL` for several people.

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
