# LanguageGenerator

A procedural generator for naturalistic languages. It picks a phonology, phonological rules, an orthography, morphology and syntax, then produces text in the language with a full morphological markup: each word is segmented into morphemes and glossed by the [Leipzig rules](https://www.eva.mpg.de/lingua/resources/glossing-rules.php).

```
ash iw shliwiä
a-sh         iw           shliw-i-ä
dog[SG]-ERG  cat[SG.ABS]  see-PST-3SG
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

`Generator.Generate(Random)` builds a `Language` in stages, each depending on the earlier ones:

- **Phonology:** vowel and consonant inventories, a syllable template (CV, CVF, SCLVF, ...) with an optional restriction on codas, sandhi rules at morpheme boundaries, and a strategy for repairing illegal clusters (epenthesis or deletion).
- **Orthography:** a spelling for each phoneme, from one consistent style per language (háček or digraph postalveolars, macron or acute long vowels, ...), checked to be unambiguous with the Sardinas–Patterson test.
- **Lexicon:** roots built from legal syllables. Root length follows a displaced Poisson whose mean depends on how many distinct syllables the phonology allows, so languages with simple syllables have longer words.
- **Word order:** constituent order and the head directionality of each phrase type.
- **Morphology:** how fusional or isolating the language is, whether it prefixes or suffixes, which category values are zero-marked, and paradigms of exponents for nouns and verbs.
- **Alignment and cases:** nominative–accusative or ergative–absolutive, which cases exist, and whether each semantic role is marked by a case, an adposition or nothing.

`Syntax.Realize` then turns a clause into words, and `Phonology.Realize` turns each word into its surface form: sandhi, cluster repair and syllabification.

## Design principles

- **Typological frequencies, not uniform choices.** Where the [World Atlas of Language Structures](https://wals.info/) has counts, choices are drawn in proportion to them, conditioned on related features where WALS has the combination (prefixing given word order, for instance). Where no data exists, the odds are guesses, and the code says so next to them.
- **Tendencies in language, conventions in spelling.** Grammar varies the way real languages do, but an orthography is designed, so once a language picks a spelling convention it applies it everywhere.
- **Description is separate from generation.** A `Language` is plain data that knows nothing about how it was chosen; everything random lives in `Generator`.
- **Deterministic from a seed.** All randomness flows from one `Random` passed in explicitly, so a seed always reproduces the same language.
- **Closed sets in the type system.** Category values, morphemes, syllable slots, sandhi rules and role markers are C# 15 unions or closed hierarchies, so a switch over them is checked for exhaustiveness.

## Running it

It needs the .NET 11 preview SDK; `global.json` pins the version, which Visual Studio also needs in order to pick up a preview SDK.

```bash
dotnet run --project LanguageGenerator -- 11
```

prints the language generated from seed 11: its phonology, spelling, lexicon, word order, paradigms and some example sentences. Leave out the seed to get a random one, which is printed first.

```bash
dotnet test
```

runs the xUnit tests. Besides unit tests, they generate a sample of 300 languages and check properties every one must have, like every surface syllable being legal under its language's rules.

## What's next

- **Stable roots per word.** Each root should be seeded from the language's seed and its lemma, via a stable hash, so translating more text never changes words already generated.
- **Word frequency.** A bundled lemma frequency table (with English length as the fallback for words it lacks), so frequent words come out shorter, per Zipf's law of abbreviation.
- **Open vocabulary.** Roots coined on demand, with checks against accidental homophones, and eventually derivation and compounding for words like *teacher* or *sunflower*.
- **Broader grammar.** Definiteness and articles, oblique roles with real adposition words, absolutive agreement, and more verb categories.
- **Phonetic detail.** Contextual voicing, and identical-vowel hiatus.
