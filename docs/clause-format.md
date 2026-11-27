# Clause format

`translate` reads a file of clauses and renders them in a generated language. Each clause is the English it means, then an indented line saying what it means in Leipzig glosses:

```
the hunter saw the wolves
  see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF
```

The verb comes first, then its arguments as `role:noun`. A line with no verb is a [fragment](#fragments). See [examples/scene.txt](../examples/scene.txt) for a whole file, and `ClauseFile` for the parser.

Words are English meanings, whatever their case, which the language coins roots for as needed.

Features left out take their unmarked values: present, imperfective, singular, and exclusive for 'we'. A value the language doesn't distinguish falls back on the nearest one it does, so a dual becomes a plural in a language without one.

Anything the generator can't express yet, like a superlative, is an error naming the line, never silently dropped.

## Verbs

| Gloss | Meaning | Example |
| --- | --- | --- |
| `NEG` | Negates the clause | `see.PST.NEG` |
| `PRF` | A perfect, as in 'has gone', or with `PST` 'had gone' | `repair.PRF  subject:nobody  object:3SG.N` |
| `PROG` | A progressive, as in 'is waiting', or with `PST` 'was waiting' | `mend.PST.PROG  subject:3SG.F  object:net.NDF` |
| `HAB` | A habitual, as in 'usually drinks', or with `PST` 'used to drink' | `walk.PST.HAB  subject:3SG.F  goal:there` |
| `IMP` | A command, with no tense or aspect | `drink.IMP.NEG  object:wine` |
| `PASS` | Passive, with what's done to as its subject and `agent:` saying who does it | `carry.PASS  subject:light.DEF  agent:(figure.NDF small)` |
| `ABIL` | Its subject can, as in 'you can swim' | `swim.ABIL  subject:2SG` |
| `PERM` | Its subject may, or is allowed to | `go.PERM  subject:2SG` |
| `OBLIG` | Its subject must, or has to | `find.OBLIG  subject:2SG  object:key.DEF` |
| `ADVS` | Its subject should, or ought to | `rest.ADVS  subject:2SG` |
| `Q` | A yes-no question | `see.PST.Q  subject:2SG  object:3PL` |

A command has no subject, unless it's `subject:2PL` for several people.

### Perfect, progressive and habitual

`PRF`, `PROG` and `HAB` are aspects, so they take the place of `PFV` and `IPFV`, and go with any tense. A language marks each with an affix, a word of its own, or an auxiliary verb, as WALS 68A counts the perfect: from 'have', as English *has gone* is, from 'finish', as in much of Southeast Asia, or with the copula, as French *est venu* is. A progressive is mostly made with the copula that says where, as Spanish *está esperando* is, or with 'sit', as Dutch *zit te wachten* is. An auxiliary takes the tense, agreement and negation, with the verb bare beside it, as a modal verb does, and is glossed by what it means, `have`, `finish`, `sit`, or the copula's gloss. About half of languages have no perfect, and say it as a perfective, in the past where it's present, as Russian *nikto ne počinil* 'nobody repaired' says 'nobody has repaired'; a language with no progressive says it as an imperfective. A modal can't take either yet, so 'must have gone' can't be said.

`HAB` is for what's done again and again, as English says with *used to*, a past *would*, or a word like *usually*. WALS has no chapter on it, so its odds are guesses after Bybee, Perkins and Pagliuca and Dahl, who find habituals more often affixes than progressives are, as Turkish *-Ir* is, and some made with the copula, as Hindi *jātā hai* 'goes, habitually' is. Most languages have none, and say a habit as an imperfective, as WALS 65A finds theirs covering habituals.

### Beginning and ceasing

Beginning or ceasing to do something is written with the verbs `begin` and `stop` and a clause starting with `to`, as in `begin.PST.PFV  subject:3SG.N  object:[to  hum]` for 'it began to hum', since that's how most languages say it. A language that marks it on the verb, with an inceptive or a cessative, as Russian *za-pel* 'started singing' does, says that clause as `hum.PST.PFV.INCEP` instead, or `CESS` for one with `stop`: what's begun takes the tense, aspect, negation and modifiers of `begin`, and goes as one clause. Its marker, an affix or a word of its own, goes nearer the verb than an aspect's and further than a passive's. Where what's begun has a subject of its own, is negated itself, is a clause with `be`, or begins, stops or wants something itself, the language says `begin` or `stop` as a verb, as it does `begin` or `stop` with no clause, as in 'the song stops'. `INCEP` and `CESS` can't be written in a clause file. Nobody counts inceptives or cessatives on the verb, so their odds are guesses too, a cessative rarer than an inceptive.

### Modals

`NEG` after a modal negates the modal, and `NEG` before it negates the verb, the way glosses put a morpheme outside what it applies to:

| Written | Means |
| --- | --- |
| `swim.ABIL.NEG  subject:2SG` | 'you can't swim' |
| `sing.NEG.OBLIG  subject:2SG` | 'you must not sing' |
| `go.OBLIG.NEG  subject:2SG` | 'you don't have to go' |
| `follow.NEG.ADVS  subject:2SG  object:3PL` | 'you shouldn't follow them' |

'Could' is `ABIL.PST`. A modal works with `be` and `PASS` too, as in `be.OBLIG  subject:2SG  adjective:careful`, but not with a command or a comparison.

A language marks each modal with an affix, a word of its own or a verb, as WALS 74A counts them. A modal verb takes the tense and agreement, with the verb bare beside it, and is glossed by what it means, `can`, `may`, `must` or `should`, while an affix or a word of its own is glossed `ABIL`, `PERM`, `OBLIG` or `ADVS`. The Leipzig rules have none of these; the first three are what grammars commonly use, and `ADVS`, for advisability, is coined here, since grammars have no common gloss for 'should'.

#### Epistemic modals

`DUB`, the dubitative, says the speaker thinks something may be so, as 'might', 'may', 'could', 'maybe' and 'perhaps' do, and `PRSM`, the presumptive, that it surely is, as 'must' and 'probably' do. They're the speaker's judgment of the whole clause rather than something about its subject, so the clause keeps the tense and aspect it would have without them, and they can take `PRF` and `PROG`, which the other modals can't yet:

| Written | Means |
| --- | --- |
| `leave.PRF.PRSM  subject:3SG.M` | 'he must have left' |
| `wait.PROG.DUB  subject:3PL  object:1PL` | 'maybe they're waiting for us' |
| `come.NEG.DUB  subject:3SG.F` | 'she might not come' |

WALS 75A counts how 240 languages say epistemic possibility: with an affix on the verb in 84, a verb in 65, and something else in 91, mostly adverbs and particles like English *maybe*, which are a particle here. Epistemic necessity isn't counted, so it's drawn the same way. An affix or particle goes on the verb as any modal's does, which leaves the verb its tense. A verb is said impersonally, with the clause as its complement, marked as the language marks what's said or known, as Greek *borí na éfige* 'it-may that he-left' and Hungarian *lehet, hogy elment* 'it may be that he left' are. It's glossed `might` or `must`, is present, since the judgment is the speaker's now, and takes 'it' as its subject, which a language that drops its pronouns leaves unsaid. A language that deranks such complements loses the clause's tense there, as it does under 'know'.

Per WALS 76A, 36 of 207 languages use their situational modals epistemically for both possibility and necessity, as English *may* and *must* are both, 66 for one of the two, and 105 for neither. Van der Auwera and Plungian find epistemic possibility growing out of permission and necessity out of obligation, so `DUB` shares a marker with `PERM` and `PRSM` with `OBLIG` where they overlap; which of the two the 66 share isn't counted, so it's even. A shared verb is personal for its situational use and impersonal for its epistemic one, as Greek *boró* 'I can' and *borí* 'it may' are.

### Questions

A yes-no question has `Q` on its verb. A question asking who, what, where, when, why or how has no `Q`, and its question word goes in the role it asks about. `why` and `how` fill no role, so they're written on their own:

| Written | Means |
| --- | --- |
| `see.PST.Q  subject:2SG  object:3PL` | 'did you see them?' |
| `swim.ABIL.Q  subject:2SG` | 'can you swim?' |
| `tell.PST  subject:who  object:2SG` | 'who told you?' |
| `want  subject:3PL  object:what` | 'what do they want?' |
| `be  subject:key.DEF  location:where` | 'where is the key?' |
| `get.PST  subject:2SG  object:DIST  source:where` | 'where did you get that?' |
| `come.PST.NEG  subject:2SG  why` | 'why didn't you come?' |
| `arrive.PST  subject:3PL  time:when` | 'when did they arrive?' |
| `come.PST  subject:2SG  goal:here  how` | 'how did you get here?' |

`where` asks about a place, so it's `location:where`, `goal:where` for 'where to' or `source:where` for 'where from', and `when` about a time, so it's `time:when`, taking the time's marker as `where` takes the place's, as Turkish *ne zaman* 'what time' is. `why` and `how` are words of their own, which go first where the language puts question words first, and otherwise where obliques go. A question asks one thing, and its question word fills a role directly, not inside a noun phrase, as 'whose' would, or a coordination. A question word can't take a configuration, as in 'under what?'. Questions with 'how long', 'how often', 'how many' or 'which', `noun:who` for 'who are you?', and a question joined to or under another clause can't be said yet.

How a language marks a yes-no question follows WALS 116A: a particle, which goes first, last or after the first word as WALS 92A counts them for OV and VO languages; an affix on the finite verb, outside everything else on it; the finite verb first, as German does; or intonation alone, which the text doesn't show. A content question puts its question word first, with the rest of its phrase, in the share of languages WALS 93A counts for each word order, as English does, and otherwise leaves it in place, as Mandarin and Japanese do, with `why` where obliques go. The particle and affix are glossed `Q`, as the Leipzig rules gloss a question marker, and the question words by what they ask, `who`, `what`, `where` and `why`. A question word takes its role's case and adposition as a noun would, so 'where from' can be 'where-ABL'.

### `be`

The verb `be` says what its subject is, with `noun:fool` or `adjective:big` in place of an object, or where it is, with `location:house`, or a configuration, as in `location.under:house` (see [Places](#places)).

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

`when:` is a word of its own saying when, like *now*, *then* or *yesterday*, and `often:` one saying how often or whether something still goes on, like *again* or *still*; the language coins a root for each. `manner:` says how, as an adjective the language makes into an adverb its own way, so 'carefully' is `manner:careful`, 'suddenly' `manner:sudden` and 'well' `manner:good`. Which role an adverb is in says how near the verb it goes, so nothing has to know English adverbs by name. `time:` and `duration:` take a noun phrase, as other roles do, and `be` takes them too. `INTS` on any adjective says 'very', or 'much' with `CMPR`, as `small.CMPR.INTS` is 'much smaller'.

*Here* and *there* fill a role saying where, as `where` does (see [Demonstratives](#demonstratives)), and so do *somewhere*, *nowhere* and *everywhere* (see [Quantifiers and indefinites](#quantifiers-and-indefinites)). Other words for where, like *inside* and *upstream*, can't be said yet, and neither can *ago* or *later* after a noun or a manner that compares, as in 'more slowly'. *Never*, *always* and *sometime* are times, in `time:` (see [Quantifiers and indefinites](#quantifiers-and-indefinites)).

Adverbs and manners go where obliques go, ordered as described under [Modifier order](#modifier-order). An adverb is its bare root. A manner adverb is the adjective itself, as German *schnell* is 'quick' and 'quickly'; the adjective with an affix, as English *-ly* and Turkish *-ce* make one, glossed `ADV` as the Leipzig rules have it; or the adjective with a word beside it, as Mandarin *de* and Japanese *ni*, glossed `ADV` too. WALS doesn't count these, so the odds are guesses. A time is marked like a place in most languages, sharing the location's case or adposition, and otherwise by an adposition of its own glossed `during`. A duration is marked like the object, as Latin *duos annos* 'for two years' is accusative, like a time, or by an adposition glossed `for`. 'Very' is a word on the side the language puts degree words, the comparative's side where that's a word too, and is glossed `INTS`, as grammars commonly gloss an intensifier.

### Places

`location:`, `goal:`, `source:` and `route:` say where something is, where it goes to, where it comes from and which way it goes. Each can also say where relative to its place, written after the role with a period:

| Written | Means |
| --- | --- |
| `be  subject:key.DEF  location.under:table.DEF` | 'the key is under the table' |
| `walk.PST  subject:3SG.M  goal.in:river.DEF` | 'he walked into the river' |
| `pull.IMP  object:wax.DEF  source.in:(ear.PL possessor:2SG)` | 'pull the wax out of your ears' |
| `crawl.PST  subject:cat.DEF  source.under:bed.DEF` | 'the cat crawled out from under the bed' |
| `walk.PST  subject:1PL  route.in:forest.DEF` | 'we walked through the forest' |
| `walk.PST  subject:1PL  route.beside:mill.DEF` | 'we walked past the mill' |
| `bark  subject:dog.NDF  location.behind:mill.DEF` | 'a dog barks behind the mill' |

The configurations are `in`, `on`, `under`, `over`, `behind`, `front` for 'in front of', `beside`, `between`, which is 'among' too, `around` and `against`. A role with none is plain: 'at', 'to', 'from' or 'via'.

This follows grammars of space in splitting a spatial phrase into its path, which the role says, and its configuration, which English fuses with it in *into*, *onto* and *out of*. Each language decides how much of it to say:

- **Which configurations it tells apart.** Languages merge neighbors on Levinson and Meira's map of topological relations, as Spanish *en* covers in, on and at, Japanese *ue* on and over, and German *an* on and against, and many leave in or on to their plain locative, as Japanese *ni* does. A configuration a language merges with 'at' is said just like the plain role. The merges' odds are guesses, since WALS doesn't count them.
- **How it marks each it tells apart.** An adposition, as English *under*, which goes beside the place with the role's case, if the role has one, as Latin *sub mensā* 'under the table' has the ablative, and with a case of its own otherwise. A role marked by an adposition keeps that outside it, as English says 'from under', except a location, whose 'at' the configuration's word replaces. Or a relational noun, a noun for part of the place, possessed by it, which then takes the role's marker as any noun would, as Japanese *tsukue no shita ni* is 'desk GEN bottom LOC' and Turkish *masa-nın alt-ın-da* 'table-GEN bottom-3SG-LOC'. Most are words like 'inside', 'top', 'back' and 'side', and some are the body parts Heine and Svorou find becoming them, like 'belly' for inside and 'face' for in front; each is an ordinary noun of the language. Or a localization, a category of the noun beside its case, as Finnish *talo-ssa* 'in the house' fuses one with its locative and Lezgian stacks one inside its case. Only a language whose nouns inflect, and whose location has a case, has localizations, and in and on are likelier ones than the rest. A language mostly sticks to one of the three, but can mix them, as Finnish has cases for in and on and postpositions for under and behind. Svorou finds relational nouns the commonest source of spatial words, and spatial cases beyond a locative are a minority, mostly in Uralic and the Caucasus, which the odds follow, though they're guesses too.

A localization is glossed as grammars of Daghestanian languages gloss them: `IN`, `SUPER` for on, `SUB` for under, `ANTE` for in front of, `APUD` for beside, `INTER` for between, `CIRC` for around and `CONT` for against, with `SUPRA` for over and `RETRO` for behind coined here. A route takes `PERL`, for the perlative, where it has a case of its own, and is otherwise marked like a place, an instrument, as Russian *šël lesom* 'went forest-INS' is 'went through the forest', or a source, or by an adposition glossed `via`.

### Modifier order

Languages largely agree on which modifiers go nearest their head, and differ mostly in which side they put them, so the order on one side mirrors the order on the other. `ModifierOrder` says this once for every language. Everything where obliques go is ranked by how near the verb it goes, nearest first:

1. what the verb calls for: `recipient:`, `goal:`, `source:`, `route:`, `agent:`, and a `standard:` in a VO language
2. how: `manner:`, `like:`, `instrument:`, `companion:`, `beneficiary:`
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
| `object:` | The object, which can be a clause in brackets (see [Clauses as objects](#clauses-as-objects)) |
| `quote:` | What was said, word for word, as a clause in brackets |
| `noun:`, `adjective:` | What the subject of `be` is |
| `location:` | Where |
| `goal:` | To where |
| `source:` | From where |
| `route:` | Which way, as in 'through' or 'along' |
| `recipient:` | To whom |
| `companion:` | With whom |
| `instrument:` | With what |
| `beneficiary:` | For whom or for what, as in 'made for someone' or 'wax for your ears' |
| `like:` | Like what |
| `time:` | When |
| `duration:` | For how long |
| `agent:` | Who does it, in a passive |
| `standard:` | What the subject of `be` is compared with |
| `addressee:` | Who the clause is said to |
| `when:` | When, as a word of its own |
| `often:` | How often, or whether still, as a word of its own |
| `manner:` | How, as an adjective |

Any clause can be said to someone, with `addressee:marta` or `addressee:(man old)`, a bare noun that comes first in the vocative case where the language has one. A command to a plural addressee, as in `addressee:child.PL`, is to all of them. Who's addressed is the whole sentence's, so it goes on the first line, not on a line joined to it, nor in a clause inside another unless that clause is quoted.

## Noun phrases

A noun is definite with `DEF`, as in `subject:dog.DEF` for 'the dog', and indefinite with `NDF` for 'a dog'. With neither it's bare, as 'dogs', 'water' and 'my dog' are. With `PROX` or `DIST` it's 'this dog' or 'that dog' (see [Demonstratives](#demonstratives)).

Anything else that goes with a noun goes in parentheses, noun first:

| What | Written | Means |
| --- | --- | --- |
| Adjectives | `object:(wolf.PL big gray)` | 'big gray wolves' |
| A number, in digits | `subject:(wolf 3)` | 'three wolves' |
| An ordinal, in digits with its English ending | `subject:(day 3rd)` | 'third day' |
| A possessor | `object:(house possessor:king)` | 'the king's house' |
| A pronoun as possessor | `object:(dog.PL possessor:3SG.F)` | 'her dogs' |
| A possessor with its own | `subject:(son possessor:(sister possessor:1SG))` | 'my sister's son' |
| A relative clause | `object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_])` | 'the net she was mending' (see [Relative clauses](#relative-clauses)) |
| A quantifier | `subject:(traveler.PL few)` | 'few travelers' (see [Quantifiers and indefinites](#quantifiers-and-indefinites)) |

A number makes the noun dual for two and paucal for a few, unless it says otherwise.

## Demonstratives

`PROX` on a noun is 'this' and `DIST` is 'that', in place of `DEF`, since a demonstrative already makes its noun definite. On their own, in place of a noun, they're 'this' and 'that' standing alone, and `here` and `there` fill a role saying where, as `where` does:

| Written | Means |
| --- | --- |
| `subject:village.PROX` | 'this village' |
| `object:(tree.PL.DIST old)` | 'those old trees' |
| `be  subject:DIST  noun:bargain.DEF` | 'that is the bargain' |
| `give.PST  subject:woman.DEF  recipient:2SG  object:PROX` | 'the woman gave you this' |
| `object:DIST.PL` | 'those' |
| `stand  subject:1SG  location:here` | 'I stand here' |
| `come.PST  subject:2SG  goal:here` | 'you came here' |
| `walk  subject:3PL  source:there` | 'they walk from there' |

`MED` is a middle distance: near the person spoken to, as in 'that cup in your hand', or neither near nor far, which Spanish *ese* and Japanese *so-* both cover, and `there.MED` is such a place. `DEM` is a demonstrative that says no distance. A distance the language doesn't tell apart falls back on the nearest one it does: a middle one is far in a language with only near and far, as English *that* points near the hearer too, and every one is the same in a language with no distances. `here` and `there` take no configuration, as in 'under here', and go only in `location:`, `goal:` and `source:`. An addressee takes no demonstrative yet.

How many distances a language tells apart follows WALS 41A: two in most languages, as English does, three in many, and none in a few, as French *ce* is both 'this' and 'that'. The 12 of 234 languages with four or more have three here. A demonstrative is a word before or after its noun, as WALS 88A counts them, outside its adjectives, numeral and article word, as Greenberg's Universal 20 has it, or, in a few languages, an affix on the noun, outside its number and an article's affix but inside its case. It agrees with its noun's gender in most languages that have gender, and in its number in many, as English *this* and *these* do. A demonstrative is glossed by its distance, as the Leipzig rules gloss `PROX` and `DIST`, with what it agrees in.

Standing alone, a demonstrative is a noun, taking its role's case, and is the same word that goes beside a noun in most languages, as English *this* is, and a stem of its own in the rest, as WALS 42A counts them. *Here* and *there* are those demonstratives in the role's case, as Japanese *koko-ni* and Turkish *bura-da* are 'this place' in the locative, in most languages, as Diessel finds demonstrative adverbs mostly built on demonstrative stems, and words of their own, glossed `here`, `there` and, for a far one beside a middle one, `yonder`, in the rest.

A definite article is the demonstrative in 69 of the 620 languages WALS 37A counts, with the far demonstrative's forms and place, glossed `DEF`. A definite article of its own stays beside a demonstrative in some languages, as Greek *aftó to spíti* 'this the house' and Hungarian *ez a ház* keep it, and English drops it. Nobody counts this, so the odds are a guess.

## Quantifiers and indefinites

A quantifier is a word in its noun's parentheses: `all`, `every`, `some`, `many`, `few`, `most` or `no`, with `every` for 'each' too. An indefinite pronoun fills a role as a noun does: `someone`, `something`, `nobody`, `nothing`, `everyone` and `everything`. `somewhere`, `nowhere` and `everywhere` go in `location:`, `goal:` and `source:` instead, as `here` does, and `sometime`, `never` and `always` in `time:`, as `when` does.

| Written | Means |
| --- | --- |
| `come  subject:(traveler.PL few)` | 'few travelers come' |
| `carry.PASS  subject:(light every)` | 'each light is carried' |
| `be  subject:3SG.F  adjective:tall.CMPR  standard:(man.PL.DEF most)` | 'she is taller than most of the men' |
| `sleep  subject:(dog.PL.DEF all 3)` | 'all three dogs sleep' |
| `come.PST  subject:(traveler no)` | 'no traveler came' |
| `see.PST  subject:1SG  object:something` | 'I saw something' |
| `return  subject:(nobody relative:[go  subject:_  goal.in:forest.DEF])` | 'nobody who goes into the forest comes back' |
| `bark  subject:dog.NDF  location:somewhere` | 'a dog barks somewhere' |
| `want.PST  subject:3SG.N  object:price.NDF  time:always` | 'it has always wanted a price' |
| `come.PST  subject:2SG  time:never` | 'you never came' |

A quantifier never goes with `NDF`, and `every` and `no` go with neither `DEF` nor a demonstrative. Only `all` goes with a number. An indefinite pronoun takes no glosses, and nothing in parentheses but a relative clause. English *anyone*, *anything*, *anywhere* and *ever* are `nobody`, `nothing`, `nowhere` and `never` in a negated clause, and `someone`, `something`, `somewhere` and `sometime` in a question. A clause with `nobody`, `nothing`, `nowhere`, `never` or `no` takes no `NEG`: the language adds negation itself where it needs it.

A quantifier is a word, which goes outside the numeral, before or after the noun, and doesn't agree. It goes on the side numerals do in most languages, as Greenberg and Dryer find, but nobody counts it, so the odds are a guess. The word for 'many' is the numerals' word for it, where they have one.

Indefinite pronouns follow WALS 46A. Most languages make them from their question words, as Japanese *dare-ka* 'who-INDF' is 'someone' and *dare-mo* 'who-UNIV' 'everyone', with a marker for each series, glossed `INDF`, `NEG` or `UNIV`, which goes on the question word as a word or an affix inside its case. Many make them from nouns for 'person', 'thing', 'place' and 'time' with the quantifier, as English *some-body* and *no-thing* do. A few have words of their own, glossed `someone`, `nothing` and so on. In some, 'someone' is made one way and 'something' the other, as in Semelai and Maori, and a place or a time goes as a thing does. Each inflects as a singular noun in its role's case.

Whether *nobody* takes the verb's negation too follows WALS 115A: it does in most languages, as Russian *nikto ne prišël* 'nobody not came' does; it doesn't in a few, as in English; and in some it does only where it follows the verb, as Spanish has *no vino nadie* but *nadie vino*. Where it comes is where its role goes in the language's order. With a modal, it's the modal that's negated, as in Russian *nikto ne možet* 'nobody not can'.

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

## Clauses as objects

A clause can be the object of a verb, written in square brackets in the object's place, the way glossed examples bracket an embedded clause. It starts with what kind of clause it is:

| Starts with | Says | Example |
| --- | --- | --- |
| `that` | Something said, known, thought, seen or heard, with a time of its own | `tell.PST.PFV  subject:1SG  recipient:2SG  object:[that  be.PST  subject:2SG  adjective:late]` |
| `to` | Something wanted, tried, begun or told to be done, whose time the main verb sets | `want  subject:3PL  object:[to  go  goal:home]` |

These follow Noonan's division of complements by time reference, which is what decides how languages mark them more than the verb does. A clause starting with `to` has no tense or aspect, since its time is the main verb's, and leaves out its subject when that's the main clause's subject, as in 'they want to go home'; 'I told you to wait' says it, as `object:[to  wait  subject:2SG]`. A clause starting with `that` always has a subject. A clause in brackets can hold another, as in `say  subject:3SG.F  object:[that  know  subject:1SG  object:[that  come.PST  subject:2SG]]`, and a question can be about one, as in `tell.PST  subject:who  recipient:2SG  object:[that  come.PST  subject:1SG]` for 'who told you that I was coming?'. A question inside one, as in 'did anyone see what happened?', can't be said yet, and neither can a command, or a clause in any role but the object.

What someone said word for word is `quote:[...]`, in place of an object, on the verb that says it. It's as the speaker said it, so its 'I' and 'you' are the speaker's, and it can be anything a line on its own can but a heading: a command, a question, a clause said to someone or a [fragment](#fragments):

```
"you are late," she says
  say  subject:3SG.F  quote:[be  subject:2SG  adjective:late]
```

A complement is marked as its language marks its kind: by a complementizer, glossed `COMP`, that's a word at its start, as English *that*, a word at its end, as Japanese *to*, or a suffix on its verb, as Korean *-ko*, or by nothing at all, as Mandarin sets a complement beside its verb. WALS doesn't count complementizers, so the odds are guesses after Dryer, who finds a word at the start in nearly every VO language and all three in OV ones. An event's complementizer is the proposition's in some languages, as Romance *que* is both.

Its verb may be deranked, bare of tense and agreement, as WALS 125A to 127A count for subordinate clauses: always, never, or where the two clauses share a subject, which a deranked clause leaves unsaid. Cristofaro finds the complements of 'say', 'know' and 'think' deranked far less often than those of 'want', so a proposition's mostly isn't. An event's follows WALS 124A, which counts how 'want' says the subject of what's wanted: left implicit, as Turkish *Ankara-ya git-mek ist-iyor-um* 'I want to go to Ankara' does with a deranked verb, in 144 languages, and expressed, as Greek *thélo na páo* 'I want that I go' does with a balanced one, in 72. A deranked complement can be marked like a role in place of its complementizer, with the object's case, as Turkish puts a nominalized clause in the accusative, or for an event with a goal's marker, as English *to go* is.

In the other 53 languages WALS counts, 'want' isn't a verb at all, but an affix on the verb of what's wanted, as Japanese *tabe-ta-i* 'eat-DESID-PRS' is 'wants to eat', in 45, or a word beside it, in 8. Either is glossed `DESID`, which grammars commonly coin, and takes the tense, agreement and negation that 'want' would have had. 'I want you to go' still needs a verb for 'want', since what's wanted isn't the wanter's own doing.

A complement that keeps its verb, and a quotation, go where the object does or last in the clause. In VO languages they nearly always go last, and in OV languages mostly where a word starts them, as Hindi *ki* and Persian *ke* clauses follow the verb while Japanese ones, marked at their end, go before it, as Dryer and Hawkins find. A deranked complement is a noun phrase, and goes where the object does. A quotation is marked with the complementizer, as Japanese *to* and Ewe *bé* mark both, since words for 'say' so often become complementizers, with a word of its own, as Turkish *diye* 'saying', or not at all, as in English; Güldemann finds quotatives all over Africa and Asia, but nobody counts them, so the odds are guesses. A quotative is glossed `QUOT`.

## Relative clauses

A relative clause goes in its noun's parentheses as `relative:[...]`, a clause of its own with `_` in the role its noun fills in it, as glossed examples mark a gap:

| Written | Means |
| --- | --- |
| `subject:(nobody relative:[go  subject:_  goal.in:forest.DEF])` | 'nobody who goes into the forest' |
| `object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_])` | 'the net she was mending' |
| `location.in:(clearing.NDF relative:[grow  subject:nothing  location:_])` | 'in a clearing where nothing grows' |
| `location:(place.DEF relative:[end  subject:field.PL.DEF  location:_])` | 'where the fields end' |

A relative clause has one `_`, in its subject, its object, a passive's agent or an oblique, and not inside a noun phrase or a clause in it; a clause starting with `to` in it can share a `_` subject, as in 'the man who wants to go'. One with no noun of its own, as 'what it was owed', takes a general one, `thing`, `person` or `place`, as many languages do. It can't be a command, a question or said to someone, and one with 'whose' or on a pronoun, as in 'he who hesitates', can't be said yet.

Where a relative clause goes follows WALS 96A: after its noun in all but 5 of 421 VO languages, and before it in 132 of 245 OV ones. It goes at the edge of the noun phrase, outside the article and the possessor. How it says its noun follows WALS 122A for subjects and 123A for obliques, and each of the subject, the object and the obliques has a setting:

- **A gap,** saying nothing at all, as Turkish *kitab-ı al-an öğrenci* 'the student who bought the book' leaves out its subject, in 125 of 142 languages for subjects and 55 of 88 for obliques. A gap leaves nothing of an oblique behind either, adposition and all, since stranding one, as English *the house I live in* does, is rare outside Germanic.
- **A relative pronoun,** first in the clause with its role's case and adposition, as German *den* in *der Mann, den ich sah* 'the man whom I saw', in 12 and 13, nearly all in Europe. It's the language's question words for 'who', 'what' and 'where' in most languages, as question words so often become relative pronouns, and a word of its own, glossed `REL`, in the rest.
- **A resumptive pronoun,** an ordinary pronoun in the gap's place, as Persian resumes an oblique, in 5 and 20.

WALS doesn't cross the two, so obliques are drawn given subjects, keeping its totals and the Accessibility Hierarchy, which Keenan and Comrie find has languages resume the roles low on it before the high ones: a language that resumes its subjects resumes its obliques too. WALS counts no direct objects, which are drawn like the subject, or more rarely like obliques. The 24 languages that keep the whole noun inside the relative clause, as internally headed and correlative ones do, aren't made here.

A relative clause is marked by a word at its edge beside its noun, as English *that* and Mandarin *de* are, by an affix on its verb, which leaves the verb a participle without tense or agreement, as Turkish *-an* and Korean *-n* do, or by nothing, as English *the net she was mending* is; either marker is glossed `REL`. Nobody counts these, so the odds are guesses after Lehmann and Keenan: a relative clause before its noun is mostly a participle, one after it mostly marked by a word, and one with a relative pronoun mostly by nothing else.

## Fragments

Game text is full of lines with no verb: headings, item descriptions, replies, greetings and calls. A line that starts with a role instead of a verb is a fragment, which says one of these:

| Written | Means |
| --- | --- |
| `noun:(key.NDF old iron)` | 'an old iron key', a phrase on its own |
| `beneficiary:(ear.PL possessor:2SG)` | 'for your ears', an oblique on its own |
| `goal.in:river.DEF  addressee:marta` | 'Marta, into the river', either of them said to someone |
| `label:item  noun:(key rusty)` | 'Item: Rusty Key', a heading |
| `interjection:hello  addressee:marta` | 'hello, Marta', an interjection, said to someone or not |
| `answer:no` | 'no', an answer, `answer:yes` or `answer:no` |
| `addressee:stranger` | 'stranger!', someone called with nothing else |

`noun:` takes anything a role does but a clause: a noun phrase, a pronoun, a demonstrative or several of them joined. An interjection is a word with no glosses, as `interjection:oh` and `interjection:alas` are, said on its own or to someone, and so is an answer, which is `answer:yes` or `answer:no`, never `interjection:no`. A fragment says one `noun:`, one oblique, one interjection or one answer, and asks nothing yet, as 'where?' would. A heading is its `label:`, what kind of thing follows, and its title as `noun:`, and is said to no one. A fragment can be quoted, as in `say  subject:3SG.M  quote:[beneficiary:(ear.PL possessor:2SG)]` for '"for your ears," he says', but a heading can't, and no clause can be joined to one with a conjunction or a subordinator.

A phrase on its own is in the case a noun is cited in, the nominative or the absolutive where the language has either, and an oblique is marked just as it would be in a clause. An addressee comes first, in the vocative where the language has one, as it does before a clause, but after an interjection or an answer, as in 'hello, Marta' and 'no, Marta'; nobody counts this, so it's a guess. An interjection is a root of its own, a syllable and now and then two, as interjections mostly are, however long the language's roots run, and so are 'yes' and 'no', glossed `yes` and `no`.

Every language has words of its own for 'yes' and 'no' here, as English does. Sadock and Zwicky find two other ways of answering a question: agreeing or disagreeing with how it was put, as Japanese *hai* 'that's right' answers 'didn't you go?' to mean 'I didn't', and repeating its verb, as Welsh and Finnish do, as in Finnish *tuletko?* 'are you coming?' answered *tulen* 'I'm coming'. Both need the question being answered, which a fragment doesn't know (see the [roadmap](roadmap.md#game-text)). A heading's label and title are each said as a noun is cited, apart from each other, with a pause between them that's written as a colon, so 'Item: Rusty Key' can come out as *mef: natki natkuf*; it's the only punctuation the output has.

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
