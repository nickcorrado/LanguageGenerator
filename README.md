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

`Generator.Generate(Random)` builds a `Language` in stages, each depending on the earlier ones:

- **Phonology:** vowel and consonant inventories, a syllable template (CV, CVF, SCLVF, ...) with an optional restriction on codas, and a strategy for repairing illegal clusters (epenthesis or deletion).
- **Sound changes:** an ordered list of regular changes from underlying to surface forms, so that a morpheme sounds different in different words: nasal and voicing assimilation, degemination, voicing and lenition between vowels, final devoicing, vowel harmony, front-back as in Finnish or tongue-root as in Akan, and either elision at morpheme boundaries or contraction anywhere when vowels meet, as in Attic Greek. Changes that depend on syllables run once a word has them: coda devoicing, the weakening of s to h in codas, compensatory lengthening, and the laxing of vowels in closed syllables. Each is a value (which sounds, in what surroundings, becoming what), and only ever produces phonemes the language has.
- **Orthography:** a spelling for each phoneme, from one consistent style per language (háček or digraph postalveolars, macron or acute long vowels, ...), with no two phonemes spelled alike. Spellings may run together, as in English "mishap".
- **Lexicon:** roots built from legal syllables. Root length follows a displaced Poisson whose mean is set by sampling surface forms until roots rarely sound alike, so languages with simple syllables, or sound changes that merge a lot, have longer words. The lexicon is open: asking for a meaning it doesn't have coins a root, seeded from the language and the meaning alone, so a word comes out the same however much other text has been translated. A language calls itself by its word for 'people', the plural of 'person', as *Bantu* and *Inuit* do. A root is redrawn if it would sound like a closed-class word that fills the same slot (a noun like a pronoun, an adjective like a numeral, a verb like a copula), since those would be confused; homophones in different slots, like English *there* and *their*, are left alone, as languages leave them.
- **Word order:** constituent order, the head directionality of each phrase type, and where obliques go: almost always after the verb and object in VO languages, and before them, between them or after them in OV ones, in WALS's proportions.
- **Morphology:** how fusional or isolating the language is, whether it prefixes or suffixes, which category values are zero-marked, and paradigms of exponents for nouns and verbs.
- **Alignment and cases:** nominative–accusative or ergative–absolutive, which cases exist, and whether each semantic role is marked by a case, an adposition or nothing. Adpositions are short words of their own, prepositions or postpositions as the language's adpositional phrases go, with 'to' marking recipients and goals alike and, in about a quarter of languages, one word for both 'with a friend' and 'with a knife'.
- **Negation:** a word before or after the verb, a prefix or suffix on it, or a word on each side, like French *ne … pas*, in WALS's proportions, with isolating languages using words where others would use affixes.
- **Noun gender:** none, masculine and feminine, those and a neuter, or animate and inanimate, in WALS's proportions, assigned by meaning (people by sex) and, in about half the languages with gender, by the root's last sound too. Agreeing adjectives, and in some languages verbs, take a marker for their noun's gender.
- **Numerals:** decimal, vigesimal with tens below twenty (Basque *hogeita hamazazpi*, 'twenty and seventeen', is 37), or words for only the first few numbers and then 'many', in WALS's proportions, before or after the noun as WALS has them. Compound numbers mostly put the largest part first, and in some languages a noun after a numeral stays singular, as in Turkish *üç kurt* 'three wolf'.
- **Possessors:** marked on the possessor with a genitive case or an adposition, on the possessed noun with an affix agreeing with the possessor (Hungarian *a király ház-a* 'the king house-3SG'), on both (Turkish *kral-ın ev-i*), or on neither, in WALS's proportions. Possessors go where the language's adpositions put their objects, per Greenberg's Universal 2, and where only the possessed noun is marked, the affix alone says 'my' or 'his'.
- **Adjectives:** before or after the noun, as each language's noun phrases go, and either bare, agreeing with the noun in number and case, or with the whole phrase marked once at its edges.
- **Copular clauses:** a short copula, glossed COP, which some languages leave out in the present, and adjectives that either take it or inflect like verbs themselves, as in Mandarin *māo dà* 'the cat is big', each in WALS's proportions. Most languages say where something is with a verb of their own, glossed be.at, like Spanish *estar* beside *ser*, and the rest with the copula.
- **Pronouns:** one short form per person and number, with inclusive and exclusive 'we' in some languages, and none at all in others; masculine and feminine (and sometimes neuter) forms in the third person, or the second and third, in about a third of languages; case markers shared with nouns where nouns have a case slot of their own; and whether subject pronouns are usually dropped, leaving the verb's agreement to say who.

`Syntax.Realize` then turns a clause into words, and `Phonology.Realize` turns each word into its surface form: sound changes, cluster repair and syllabification.

## Design principles

- **Typological frequencies, not uniform choices.** Where the [World Atlas of Language Structures](https://wals.info/) has counts, choices are drawn in proportion to them, conditioned on related features where WALS has the combination (prefixing given word order, for instance). Where no data exists, the odds are guesses, and the code says so next to them.
- **Tendencies in language, conventions in spelling.** Grammar varies the way real languages do, but an orthography is designed, so once a language picks a spelling convention it applies it everywhere.
- **Description is separate from generation.** A `Language` is plain data that knows nothing about how it was chosen; everything random lives in `Generator`.
- **Deterministic from a seed.** All randomness flows from one `Random` passed in explicitly, so a seed always reproduces the same language.
- **Closed sets in the type system.** Category values, morphemes, syllable slots and role markers are C# 15 unions or closed hierarchies, so a switch over them is checked for exhaustiveness.
- **The pipeline's stages are separate projects.** `LanguageGenerator` is the generator, and has no dependencies, so a game can embed it; it also owns the clause format, the contract between the stages. `LanguageGenerator.Glossing` turns English into clause files with a language model, and is the only project with a model's SDK. `LanguageGenerator.Cli` is the command line over both. Each has its own test project.

## Running it

It needs the .NET 11 preview SDK; `global.json` pins the version, which Visual Studio also needs in order to pick up a preview SDK.

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
  see.PST.PFV  subject:hunter  object:wolf.PL
```

The verb comes first, then its arguments as `role:noun`. Words are English meanings, which the language coins roots for as needed, and pronouns are a person and number, as in `subject:1SG` or `object:1PL.INCL`. A noun with adjectives goes in parentheses, noun first, as in `object:(wolf.PL big gray)`, and so does a number, in digits, as in `subject:(wolf 3)`, which makes the noun dual for two and paucal for a few unless it says otherwise. A possessor goes in the parentheses too: `object:(house possessor:king)` is 'the king's house', and `object:(dog.PL possessor:3SG.F)` 'her dogs'. Features left out take their unmarked values (present, imperfective, singular, and exclusive for 'we'), and a value the language doesn't distinguish falls back on the nearest one it does, so a dual becomes a plural in a language without one. A verb with `NEG`, as in `see.PST.NEG`, negates the clause, and the verb `be` says what its subject is, with `noun:fool` or `adjective:big` in place of an object, or where it is, with `location:house`. Obliques say where (`location:house`), to where (`goal:`), from where (`source:`), to whom (`recipient:`), with whom (`companion:`) and with what (`instrument:`). Anything the generator can't express yet, like a question or a mood, is an error naming the line, never silently dropped. See [examples/scene.txt](examples/scene.txt) and `ClauseFile`.

### Glossing English

Writing clause files by hand gets old fast, so a language model can write them from plain English:

```bash
dotnet run --project LanguageGenerator.Cli -- gloss examples/story.txt
```

writes `examples/story.clauses.txt`, ready for `translate`. Any model will do: Claude by default (with `ANTHROPIC_API_KEY` or an `ant auth login` profile), or anything with an OpenAI-style endpoint, like OpenAI itself or a local model, with `--provider openai --model <name>` and, for a local server, `--base-url http://localhost:11434/v1`. Each provider is a small adapter behind `IChatModel`.

Every gloss is checked by `ClauseFile`'s parser, and one that doesn't parse goes back to the model with the parser's error, a couple of times at most. A sentence the format can't say, like a question, comes back as a `# can't express yet (question): ...` comment, so the output doubles as a list of what grammar real text needs next. The prompt is built from the parser's own tables of roles and glosses, so it keeps up as the format grows.

Tokens go only where they're needed. Sentences go in batches of 20, sharing one copy of the instructions (about 700 tokens); a rerun keeps every sentence already glossed in the output file, so editing the text sends only what changed, and hand edits to the glosses survive (`--again` glosses everything afresh, as after the format learns something new); and a run that fails partway saves what it finished. Once a text is glossed, translating it is deterministic, free and offline.

```bash
dotnet test
```

runs the xUnit tests. Besides unit tests, they generate a sample of 300 languages and check properties every one must have, like every surface syllable being legal under its language's rules.

## What it leaves out

Some of what real languages do is out of scope, by design:

- **Tone.** Syllables carry no tone or pitch accent.
- **Syllabic consonants.** Every syllable's nucleus is a vowel, so there's no Czech *vlk* 'wolf'.
- **Rarer consonants.** Inventories are drawn from common places and manners of articulation, with no clicks, ejectives or implosives.
- **Polysynthesis.** Morphology runs from analytic through agglutinative to fusional; no language builds whole clauses into one word, as polysynthetic languages do, and none is oligosynthetic, a type whose existence is doubted anyway.

## What's next

- **Running real text through the front end,** to see which grammar it most often can't express yet.
- **More of the clause.** Existentials ('there is a dog in the house', which many languages say unlike 'the dog is in the house'), mood and voice, and questions.
- **Word frequency.** A bundled lemma frequency table (with English length as the fallback for words it lacks), so frequent words come out shorter, per Zipf's law of abbreviation.
- **Derivation and compounding,** for words like *teacher* or *sunflower*. Checks against accidental homophones are harder than they sound: avoiding one would make a root depend on the words coined before it.
- **Broader grammar.** Definiteness and articles, spatial adpositions like 'under' and 'behind' (often nouns, as in 'at the house's underside'), absolutive agreement, and more verb categories.
- **More sound changes.** Stress, with the vowel reduction and syncope it brings, allophones that aren't phonemes of the language, and changes across word boundaries, like French liaison.
