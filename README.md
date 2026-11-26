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

`Generator.Generate(int)` builds a `Language` from a seed, in stages, each depending on the earlier ones. [Phonology, spelling and the lexicon](docs/phonology.md) and [grammar](docs/grammar.md) describe each stage in full; in short:

### Sounds and words

- **[Phonology](docs/phonology.md#inventories):** vowel and consonant inventories built in series, sized and shaped as WALS and PHOIBLE have them, with long vowels and diphthongs in some languages; syllables from CV to SCLVF with onsets that rise in sonority, and limits on codas and on [syllable contact](docs/phonology.md#syllable-contact); and [stress](docs/phonology.md#stress) at a fixed place, drawn to heavy syllables, or free, as in Russian.
- **[Sound changes](docs/phonology.md#sound-changes):** an ordered list of regular changes from underlying to surface forms, such as assimilation, lenition, vowel harmony, what happens when vowels meet, and changes that follow syllables and stress, like vowel reduction and syncope. A change makes a phoneme or, where it makes a sound the language lacks, an allophone.
- **[Where words meet](docs/phonology.md#where-words-meet):** some changes apply between words too, final consonants may survive only in liaison, as in French, and particles are clitics, said as one word with their host.
- **[Orthography](docs/phonology.md#orthography):** one consistent spelling style per language, with no two phonemes spelled alike, and free stress marked where the rule wouldn't put it.
- **[Lexicon](docs/phonology.md#lexicon):** roots built from legal syllables, as long as they need to be to rarely sound alike, and coined on demand for any meaning, the same however much other text has been translated.

### Grammar

- **[Word order](docs/grammar.md#word-order) and [morphology](docs/grammar.md#morphology):** constituent order, head directionality and where obliques go; how fusional or isolating the language is, whether it prefixes or suffixes, and its paradigms.
- **[Alignment and cases](docs/grammar.md#alignment-and-cases):** nominative–accusative, ergative–absolutive or neither, and roles marked by cases or adpositions that divide a semantic map of roles among them.
- **[Noun phrases](docs/grammar.md#noun-phrases):** gender, pronouns, numerals and ordinals, possessors, articles, demonstratives, quantifiers and indefinites, and adjectives.
- **[Verbs](docs/grammar.md#verbs):** negation, commands and prohibitives, passives, the perfect, progressive and habitual, and modals, situational and epistemic.
- **[Clauses](docs/grammar.md#clauses):** copular clauses, questions, coordination, subordinate, relative and complement clauses, comparison and adverbs.

`Syntax.Realize` then turns a clause into words, and `Phonology.RealizePhrase` turns them into the sentence as it's said: each word with the clitics leaning on it, through sound changes, cluster repair and syllabification, and then what happens where words meet.

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

prints the language generated from seed 11: its phonology, spelling, lexicon, word order, paradigms and some example sentences. Leave out the seed to get a random one, which is printed first. Add `--phonetic`, here or to `translate`, to show how each sentence is said, in IPA, under it.

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

The [roadmap](docs/roadmap.md) lists what real text still needs, from questions and relative clauses to language families, and what's left for configuring generation, whose [spec format](docs/spec-format.md) and [design](docs/configuration.md) have documents of their own.
