# LanguageGenerator

A procedural generator for naturalistic languages. It picks a phonology, phonological rules, an orthography, morphology and syntax, then produces text in the language with a full morphological markup: each word is segmented into morphemes and glossed by the [Leipzig rules](https://www.eva.mpg.de/lingua/resources/glossing-rules.php).

```
siv swozhgè sroshle
siv       swozh-gè  sro-shle
dog[NOM]  ACC-cat   see-PST
'the dog saw the cat'
```

## Why

This is a proof of concept: general-purpose language generation is possible, and not nearly as hard as it's usually assumed to be. It's aimed at games that want languages of their own, such as a world generator like Dwarf Fortress, which lists procedural languages among its future features, or a language-deciphering game like Chants of Sennaar, which needs languages consistent enough to be learned.

The markup is the point. A gloss records what each piece of a word means, which makes generated text something a program can reason about. A game can translate it, teach it to the player piece by piece, or check a guess.

### Where it's headed

The intended use is a pipeline:

1. English text from a game's script goes in.
2. An NLP step lemmatizes and tags it (*saw* → *see*/VERB), producing the features a gloss needs.
3. The generator realizes each sentence in a generated language, coining roots on demand for whatever words appear.
4. Out come translated text and its interlinear glosses.

So nothing should depend on hand-curating the vocabulary: the lexicon has to be open, and anything about a word the generator needs to know, like how frequent it is, has to come from data it can look up.

## How a language is generated

`Generator.Generate(int)` builds a `Language` from a seed, in stages, each depending on the earlier ones:

- **Phonology:** vowel and consonant inventories, a syllable template (CV, CVF, SCLVF, ...) as simple or complex as WALS's counts have them, with an optional restriction on codas, and a strategy for repairing illegal clusters (epenthesis or deletion). Inventories are built rather than pruned. A vowel system takes its size from WALS's counts and one of the spread-out shapes real systems have (*a i u*, *a e i o u*, the seven vowels of Italian, ...), with length for all of its vowels, all but the mid ones as in Classical Arabic, or none. Consonants come in series, so a language that voices its stops has *b d g* together, as does one with aspirates (*pʰ tʰ kʰ*), and a marked sound comes only with the sounds it implies (*z* with *s*, *ŋ* with *k*, *gʷ* with *kʷ*), at odds near its share of [PHOIBLE](https://phoible.org/)'s inventories. Most languages have one rhotic, a trill or tap far more often than English *ɹ*. The commonest sounds come first in a ranking that decides how often each turns up in words. Onset clusters rise in sonority, so *pl* and *kw* but not *nr*, and a root's later syllables always have an onset, so vowels meet only where morphemes do. Where a coda meets the next onset, many languages limit how far sonority may rise across the boundary, per Vennemann's Syllable Contact Law: some allow a small rise (*s.n* but not *t.l*), others none (*l.t* but not *n.l*), and their roots keep to it. Each word has one stressed syllable, counted over the whole word so that it moves as suffixes are added: at a fixed place, like the first syllable or the second-to-last, or drawn to heavy syllables, those with a long vowel or a coda, as in Latin, in WALS's proportions. Stress is always predictable, so a player can learn it from a few words.
- **Sound changes:** an ordered list of regular changes from underlying to surface forms, so that a morpheme sounds different in different words: nasal and voicing assimilation, degemination, voicing and lenition between vowels (aspirates weakening to fricatives as Greek's did), final devoicing, and vowel harmony, front-back as in Finnish or tongue-root as in Akan. Every language merges the same vowel twice into a long one, and has one way with vowels that meet at a morpheme boundary: eliding the first, coalescing them as Sanskrit does (*a + i* → *e*), contracting them as Attic Greek does (*e + o* → *ō*), putting a glide or a glottal stop between them, or leaving them be, with close vowels often becoming glides first (*i + a* → *ya*). Changes that depend on syllables run once a word has them: coda devoicing, the weakening of s to h in codas, compensatory lengthening, and the laxing of vowels in closed syllables, and those that follow the stress: unstressed vowels weakening to schwa as in English or rising as in Portuguese (*o* → *u*), long vowels shortening away from the stress, stressed vowels lengthening in open syllables as in Italian, and syncope, the loss of the vowel after the stress, as Latin *calidus* became Italian *caldo*. A language that restricts syllable contact may repair the contacts its suffixes make: a coda doubling the onset after it (Korean *sin-la* → *silla*), a sonorant onset hardening into a stop (the Kazakh plural *-lar* is *-dar* after a nasal and *-tar* after a voiceless stop), or a stop becoming a nasal before one (Korean *kuk-mul* → *kuŋmul*). Each is a value (which sounds, in what surroundings, becoming what, or what goes in between), and only ever produces phonemes the language has.
- **Orthography:** a spelling for each phoneme, from one consistent style per language (háček or digraph postalveolars, macron or acute long vowels, *ph* or Wade–Giles *p'* for aspirates, *ñ* or *ny*, ...), with no two phonemes spelled alike. A convention that would clash in a language, like Pinyin's *b* for a plain *p* in one that also has a voiced *b*, is drawn again. Spellings may run together, as in English "mishap".
- **Lexicon:** roots built from legal syllables. Root length follows a displaced Poisson whose mean is set by sampling surface forms until roots rarely sound alike, so languages with simple syllables, or sound changes that merge a lot, have longer words. The lexicon is open: asking for a meaning it doesn't have coins a root, seeded from the language and the meaning alone, so a word comes out the same however much other text has been translated. A language calls itself by its word for 'people', the plural of 'person', as *Bantu* and *Inuit* do. A root is redrawn if it would sound like a closed-class word that fills the same slot (a noun like a pronoun, an adjective like a numeral, a verb like a copula), since those would be confused; homophones in different slots, like English *there* and *their*, are left alone, as languages leave them.
- **Word order:** constituent order, the head directionality of each phrase type, and where obliques go: almost always after the verb and object in VO languages, and before them, between them or after them in OV ones, in WALS's proportions.
- **Morphology:** how fusional or isolating the language is, whether it prefixes or suffixes, which category values are zero-marked, and paradigms of exponents for nouns and verbs.
- **Alignment and cases:** nominative–accusative, ergative–absolutive or neither, in WALS's proportions, which cases exist, and whether each semantic role is marked by a case, an adposition or nothing. The oblique roles sit on a semantic map, a graph whose edges are the ways languages mark two roles alike: recipients with goals, as English *to* does, goals with locations, as Japanese *ni* does, possessors with sources, as Romance *de* does, companions with instruments, as English *with* does in about a quarter of languages, and a few more. Each language divides the map into connected regions, one marker apiece, so a role without a marker of its own takes its neighbors'. How many regions get a case follows WALS's counts of case inventories, favoring the genitive, dative and locative as Blake's case hierarchy does, and the rest get an adposition, a short word of its own, before or after its noun as the language's adpositional phrases go.
- **Negation:** a word before or after the verb, a prefix or suffix on it, or a word on each side, like French *ne … pas*, in WALS's proportions, with isolating languages using words where others would use affixes.
- **Commands:** an imperative with its own form for singular and plural, for only one of them or for both alike, often a bare stem, or none at all, leaving the ordinary second person, in WALS's proportions. Prohibitives use the imperative or the second person, with ordinary negation or a negator of their own, like Latin *nē*.
- **Noun gender:** none, masculine and feminine, those and a neuter, or animate and inanimate, in WALS's proportions, assigned by meaning (people by sex, as a noun file says) and, in about half the languages with gender, by the root's last sound too. Agreeing adjectives, and in some languages verbs, take a marker for their noun's gender.
- **Numerals:** decimal, vigesimal with tens below twenty (Basque *hogeita hamazazpi*, 'twenty and seventeen', is 37), or words for only the first few numbers and then 'many', in WALS's proportions, before or after the noun as WALS has them. Compound numbers mostly put the largest part first, and in some languages a noun after a numeral stays singular, as in Turkish *üç kurt* 'three wolf'. Ordinals follow WALS's types too: most languages have a word of their own for 'first', some for 'second' as well, and build the rest from cardinals with a marker on the whole numeral (*thirty-seven-th*), a prefix, a suffix or, in isolating languages, a word, as Mandarin puts *dì* before *sān* for 'third'; others build every ordinal, and some just use the cardinal.
- **Possessors:** marked on the possessor with a genitive case or an adposition, on the possessed noun with an affix agreeing with the possessor (Hungarian *a király ház-a* 'the king house-3SG'), on both (Turkish *kral-ın ev-i*), or on neither, in WALS's proportions. Possessors go where the language's adpositions put their objects, per Greenberg's Universal 2, and where only the possessed noun is marked, the affix alone says 'my' or 'his'.
- **Articles:** a definite article, an indefinite one, both or neither, in WALS's proportions. Each is a word at the edge of the noun phrase or an affix on the noun, and an indefinite article is often the word for 'one', for one thing only, so 'dogs' stays bare. In most languages with gender, articles agree with their noun's gender, and in some a definite article agrees in number too, as Spanish has *el*, *la*, *los* and *las*.
- **Adjectives:** before or after the noun, as each language's noun phrases go, and either bare, agreeing with the noun in number and case, or with the whole phrase marked once at its edges.
- **Copular clauses:** a short copula, glossed COP, which some languages leave out in the present, and adjectives that either take it or inflect like verbs themselves, as in Mandarin *māo dà* 'the cat is big', each in WALS's proportions. Most languages say where something is with a verb of their own, glossed be.at, like Spanish *estar* beside *ser*, and the rest with the copula.
- **Passives:** a passive marked on the verb, next to its root, in WALS's proportions for each alignment: most nominative-accusative languages have one, but few ergative ones, whose patients already take the intransitive subject's case. The agent is a role on the semantic map, marked like an instrument, a source or a place, or by a word of its own like English *by*. A language without a passive says the active, with the agent as subject, or 'they' where nobody says who.
- **Coordination:** 'and', 'or' and 'but' between noun phrases, adjectives and clauses. 'And' between nouns is the word for 'with' in nearly half of languages, as WALS 63A counts them, so 'the dog with the cat slept', and the same word joins clauses in some languages and not others, or none at all, as WALS 64A has it. Things joined by 'and' are plural to what agrees with them, or dual where the language has one, and 'you and I' is 'we'.
- **Subordinate clauses:** clauses saying if, when, before, after, until, because and in order to, each marked by a word at the start of the clause, a word at its end or a suffix on its verb, as WALS 94A counts them by word order. In OV languages they nearly always come first; in VO languages conditionals do, as Greenberg's Universal 14 has it, and reason and purpose clauses mostly follow. Some languages strip a subordinate verb of its tense and agreement, as WALS 125A to 127A count for purpose, 'when' and reason clauses, and leave out a subject the two clauses share. Subordinators share markers along a semantic map of their own, as German *wenn* is both 'if' and 'when' and English *since* both 'after' and 'because', and a reduced clause can be marked like a role on the role map, as a purpose clause is 'for drinking' with a dative or English *to drink* with its 'to'.
- **Modals:** 'can', 'may', 'must' and 'should', each by an affix on the verb, as Turkish *-ebil*, a word of its own, or a verb, as French *pouvoir*, in WALS 74A's proportions. A modal verb takes the tense and agreement and leaves the verb bare beside it, before it in VO languages and after it in OV ones, as auxiliaries go. Ability and permission often share a marker, as English *can* does, and so do 'must' and 'should', as French *devoir* does, along a semantic map like the subordinators', and a shared marker is glossed by what it means in each use.
- **Questions:** yes-no questions by a particle, first, last or after the first word, as Mandarin *ma* and Russian *li* go, by an affix on the verb, by putting the verb first, as German does, or by intonation alone, in WALS 116A's and 92A's proportions; and questions with 'who', 'what', 'where' and 'why', whose question word goes first, as in English, or stays in its place, as in Mandarin, in the share WALS 93A counts for each word order. A question word takes its role's case, as Turkish *nere-den* 'where from' does.
- **Comparison:** 'Marta is taller than the men' as WALS 121A's types have it: with the standard marked like a place, mostly 'from', as in Turkish ('tall from the men'), with a particle like English *than*, as the object of a verb meaning 'exceed', as in Yoruba ('she is tall, exceeds the men'), or as two clauses, 'Marta is tall, the men are not tall'. Every language can say 'taller' with no standard, by a word like Turkish *daha* 'more', before or after the adjective as WALS 91A counts, or an affix like English *-er*, and some mark the adjective beside a standard too, as English does and most languages of the other types don't. Similes, 'it shines like water', take a word for 'like' or an affix on what something is like.
- **Pronouns:** one short form per person and number, with inclusive and exclusive 'we' in some languages, and none at all in others; masculine and feminine (and sometimes neuter) forms in the third person, or the second and third, in about a third of languages; case markers shared with nouns where nouns have a case slot of their own; and whether subject pronouns are usually dropped, leaving the verb's agreement to say who.

`Syntax.Realize` then turns a clause into words, and `Phonology.Realize` turns each word into its surface form: sound changes, cluster repair and syllabification.

## Design principles

- **Typological frequencies, not uniform choices.** Where the [World Atlas of Language Structures](https://wals.info/) has counts, choices are drawn in proportion to them, conditioned on related features where WALS has the combination (prefixing given word order, for instance). Where no data exists, the odds are guesses, and the code says so next to them.
- **Tendencies in language, conventions in spelling.** Grammar varies the way real languages do, but an orthography is designed, so once a language picks a spelling convention it applies it everywhere.
- **Description is separate from generation.** A `Language` is plain data that knows nothing about how it was chosen; everything random lives in `Generator`.
- **Deterministic from a seed.** Each stage draws from a `Random` of its own, made from the seed and the stage's name, so a seed always reproduces the same language, and changing how one stage draws changes only that stage and those that use its result.
- **Closed sets in the type system.** Category values, morphemes, syllable slots and role markers are C# 15 unions or closed hierarchies, so a switch over them is checked for exhaustiveness.
- **The pipeline's stages are separate projects.** `LanguageGenerator` is the generator, and has no dependencies, so a game can embed it; it also owns the clause format, the contract between the stages. `LanguageGenerator.Glossing` turns English into clause files with a language model, and is the only project with a model's SDK. `LanguageGenerator.Cli` is the command line over both. Each has its own test project.

## Running it

It needs the .NET 11 SDK.

```bash
dotnet run --project LanguageGenerator.Cli -- 11
```

prints the language generated from seed 11: its phonology, spelling, lexicon, word order, paradigms and some example sentences. Leave out the seed to get a random one, which is printed first.

### Translating your own text

```bash
dotnet run --project LanguageGenerator.Cli -- translate examples/scene.txt 11
```

translates a file of clauses into seed 11's language, glossing each one. Each clause is the English it means, then an indented line saying what it means in Leipzig glosses:

```
the hunter saw the wolves
  see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF
```

The verb comes first, then its arguments as `role:noun`. Anything the generator can't express yet, like a question, is an error naming the line, never silently dropped. The [clause format](docs/clause-format.md) has the full syntax and describes the noun file that tells a language what its nouns name. See [examples/scene.txt](examples/scene.txt) for a whole file.

### Glossing English

Writing clause files by hand gets old fast, so a language model can write them from plain English:

```bash
dotnet run --project LanguageGenerator.Cli -- gloss examples/story.txt
```

writes `examples/story.clauses.txt`, ready for `translate`. Any model will do: Claude by default (with `ANTHROPIC_API_KEY` or an `ant auth login` profile), or anything with an OpenAI-style endpoint, like OpenAI itself or a local model, with `--provider openai --model <name>` and, for a local server, `--base-url http://localhost:11434/v1`. Each provider is a small adapter behind `IChatModel`.

Every gloss is checked by `ClauseFile`'s parser, and one that doesn't parse goes back to the model with the parser's error, a couple of times at most. A sentence the format can't say, like a question, comes back as a `# can't express yet (question): ...` comment, so the output doubles as a list of what grammar real text needs next. The prompt is built from the parser's own tables of roles and glosses, so it keeps up as the format grows.

It also writes the noun file, `examples/story.nouns.txt`, by asking once for every noun the clauses use, each with a sentence that uses it. Asking once means a word can't come out differently from one sentence to the next, and the lines already in the file are always kept, even with `--again`, so a word's class changes only if you change it. Several texts can share a file with `--nouns`.

Tokens go only where they're needed. Sentences go in batches of 20, sharing one copy of the instructions (about 700 tokens); a rerun keeps every sentence already glossed in the output file, so editing the text sends only what changed, and hand edits to the glosses survive (`--again` glosses everything afresh, as after the format learns something new); and a run that fails partway saves what it finished. Once a text is glossed, translating it is deterministic, free and offline.

```bash
dotnet test
```

runs the xUnit tests. Besides unit tests, they generate a sample of 300 languages and check properties every one must have, like every surface syllable being legal under its language's rules.

## What it leaves out

Some of what real languages do is out of scope, by design:

- **Tone.** Syllables carry no tone or pitch accent. Stress is a stress accent, as in Spanish or Russian, and there's no intonation over a sentence.
- **Syllabic consonants.** Every syllable's nucleus is a vowel, so there's no Czech *vlk* 'wolf'.
- **Rarer consonants.** Inventories hold sounds a player can learn to say, so there are aspirates, glottal stops and uvular *q*, but no clicks, ejectives or implosives, and no retroflex or pharyngeal series. That makes them smaller than WALS's counts, which include all of those.
- **Polysynthesis.** Morphology runs from analytic through agglutinative to fusional; no language builds whole clauses into one word, as polysynthetic languages do, and none is oligosynthetic, a type whose existence is doubted anyway.

## What's next

The [roadmap](docs/roadmap.md) lists what real text still needs, from questions and relative clauses to language families, and the plan for configuring generation.
