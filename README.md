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

- **Phonology:** vowel and consonant inventories, a syllable template (CV, CVF, SCLVF, ...) as simple or complex as WALS's counts have them, with an optional restriction on codas, and a strategy for repairing illegal clusters (epenthesis or deletion). Inventories are built rather than pruned. A vowel system takes its size from WALS's counts and one of the spread-out shapes real systems have (*a i u*, *a e i o u*, the seven vowels of Italian, ...), with length for all of its vowels, all but the mid ones as in Classical Arabic, or none. Consonants come in series, so a language that voices its stops has *b d g* together, as does one with aspirates (*pʰ tʰ kʰ*), and a marked sound comes only with the sounds it implies (*z* with *s*, *ŋ* with *k*, *gʷ* with *kʷ*), at odds near its share of [PHOIBLE](https://phoible.org/)'s inventories. Most languages have one rhotic, a trill or tap far more often than English *ɹ*. The commonest sounds come first in a ranking that decides how often each turns up in words. Onset clusters rise in sonority, so *pl* and *kw* but not *nr*, and a root's later syllables always have an onset, so vowels meet only where morphemes do. Where a coda meets the next onset, many languages limit how far sonority may rise across the boundary, per Vennemann's Syllable Contact Law: some allow a small rise (*s.n* but not *t.l*), others none (*l.t* but not *n.l*), and their roots keep to it. Each word has one stressed syllable, counted over the whole word so that it moves as suffixes are added: at a fixed place, like the first syllable or the second-to-last, or drawn to heavy syllables, those with a long vowel or a coda, as in Latin, in WALS's proportions. Stress is always predictable, so a player can learn it from a few words.
- **Sound changes:** an ordered list of regular changes from underlying to surface forms, so that a morpheme sounds different in different words: nasal and voicing assimilation, degemination, voicing and lenition between vowels (aspirates weakening to fricatives as Greek's did), final devoicing, and vowel harmony, front-back as in Finnish or tongue-root as in Akan. Every language merges the same vowel twice into a long one, and has one way with vowels that meet at a morpheme boundary: eliding the first, coalescing them as Sanskrit does (*a + i* → *e*), contracting them as Attic Greek does (*e + o* → *ō*), putting a glide or a glottal stop between them, or leaving them be, with close vowels often becoming glides first (*i + a* → *ya*). Changes that depend on syllables run once a word has them: coda devoicing, the weakening of s to h in codas, compensatory lengthening, and the laxing of vowels in closed syllables, and those that follow the stress: unstressed vowels weakening to schwa as in English or rising as in Portuguese (*o* → *u*), long vowels shortening away from the stress, stressed vowels lengthening in open syllables as in Italian, and syncope, the loss of the vowel after the stress, as Latin *calidus* became Italian *caldo*. A language that restricts syllable contact may repair the contacts its suffixes make: a coda doubling the onset after it (Korean *sin-la* → *silla*), a sonorant onset hardening into a stop (the Kazakh plural *-lar* is *-dar* after a nasal and *-tar* after a voiceless stop), or a stop becoming a nasal before one (Korean *kuk-mul* → *kuŋmul*). Each is a value (which sounds, in what surroundings, becoming what, or what goes in between), and only ever produces phonemes the language has.
- **Orthography:** a spelling for each phoneme, from one consistent style per language (háček or digraph postalveolars, macron or acute long vowels, *ph* or Wade–Giles *p'* for aspirates, *ñ* or *ny*, ...), with no two phonemes spelled alike. A convention that would clash in a language, like Pinyin's *b* for a plain *p* in one that also has a voiced *b*, is drawn again. Spellings may run together, as in English "mishap".
- **Lexicon:** roots built from legal syllables. Root length follows a displaced Poisson whose mean is set by sampling surface forms until roots rarely sound alike, so languages with simple syllables, or sound changes that merge a lot, have longer words. The lexicon is open: asking for a meaning it doesn't have coins a root, seeded from the language and the meaning alone, so a word comes out the same however much other text has been translated. A language calls itself by its word for 'people', the plural of 'person', as *Bantu* and *Inuit* do. A root is redrawn if it would sound like a closed-class word that fills the same slot (a noun like a pronoun, an adjective like a numeral, a verb like a copula), since those would be confused; homophones in different slots, like English *there* and *their*, are left alone, as languages leave them.
- **Word order:** constituent order, the head directionality of each phrase type, and where obliques go: almost always after the verb and object in VO languages, and before them, between them or after them in OV ones, in WALS's proportions.
- **Morphology:** how fusional or isolating the language is, whether it prefixes or suffixes, which category values are zero-marked, and paradigms of exponents for nouns and verbs.
- **Alignment and cases:** nominative–accusative or ergative–absolutive, which cases exist, and whether each semantic role is marked by a case, an adposition or nothing. Adpositions are short words of their own, prepositions or postpositions as the language's adpositional phrases go, with 'to' marking recipients and goals alike and, in about a quarter of languages, one word for both 'with a friend' and 'with a knife'.
- **Negation:** a word before or after the verb, a prefix or suffix on it, or a word on each side, like French *ne … pas*, in WALS's proportions, with isolating languages using words where others would use affixes.
- **Commands:** an imperative with its own form for singular and plural, for only one of them or for both alike, often a bare stem, or none at all, leaving the ordinary second person, in WALS's proportions. Prohibitives use the imperative or the second person, with ordinary negation or a negator of their own, like Latin *nē*.
- **Noun gender:** none, masculine and feminine, those and a neuter, or animate and inanimate, in WALS's proportions, assigned by meaning (people by sex, as a noun file says) and, in about half the languages with gender, by the root's last sound too. Agreeing adjectives, and in some languages verbs, take a marker for their noun's gender.
- **Numerals:** decimal, vigesimal with tens below twenty (Basque *hogeita hamazazpi*, 'twenty and seventeen', is 37), or words for only the first few numbers and then 'many', in WALS's proportions, before or after the noun as WALS has them. Compound numbers mostly put the largest part first, and in some languages a noun after a numeral stays singular, as in Turkish *üç kurt* 'three wolf'. Ordinals follow WALS's types too: most languages have a word of their own for 'first', some for 'second' as well, and build the rest from cardinals with a marker on the whole numeral (*thirty-seven-th*), a prefix, a suffix or, in isolating languages, a word, as Mandarin puts *dì* before *sān* for 'third'; others build every ordinal, and some just use the cardinal.
- **Possessors:** marked on the possessor with a genitive case or an adposition, on the possessed noun with an affix agreeing with the possessor (Hungarian *a király ház-a* 'the king house-3SG'), on both (Turkish *kral-ın ev-i*), or on neither, in WALS's proportions. Possessors go where the language's adpositions put their objects, per Greenberg's Universal 2, and where only the possessed noun is marked, the affix alone says 'my' or 'his'.
- **Articles:** a definite article, an indefinite one, both or neither, in WALS's proportions. Each is a word at the edge of the noun phrase or an affix on the noun, and an indefinite article is often the word for 'one', for one thing only, so 'dogs' stays bare. In most languages with gender, articles agree with their noun's gender, and in some a definite article agrees in number too, as Spanish has *el*, *la*, *los* and *las*.
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
  see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF
```

The verb comes first, then its arguments as `role:noun`. Words are English meanings, whatever their case, which the language coins roots for as needed, and pronouns are a person and number, as in `subject:1SG` or `object:1PL.INCL`. A noun with adjectives goes in parentheses, noun first, as in `object:(wolf.PL big gray)`, and so does a number, in digits, as in `subject:(wolf 3)`, which makes the noun dual for two and paucal for a few unless it says otherwise, and an ordinal, in digits with its English ending, as in `subject:(day 3rd)`. A noun is definite with `DEF`, as in `subject:dog.DEF` for 'the dog', indefinite with `NDF` for 'a dog', and bare with neither, as 'dogs', 'water' and 'my dog' are. A possessor goes in the parentheses too: `object:(house possessor:king)` is 'the king's house', and `object:(dog.PL possessor:3SG.F)` 'her dogs'. A noun file says what nouns name, so that gender can follow from it: see below. Features left out take their unmarked values (present, imperfective, singular, and exclusive for 'we'), and a value the language doesn't distinguish falls back on the nearest one it does, so a dual becomes a plural in a language without one. A verb with `NEG`, as in `see.PST.NEG`, negates the clause, and the verb `be` says what its subject is, with `noun:fool` or `adjective:big` in place of an object, or where it is, with `location:house`. Obliques say where (`location:house`), to where (`goal:`), from where (`source:`), to whom (`recipient:`), with whom (`companion:`) and with what (`instrument:`). A verb with `IMP` is a command, as in `drink.IMP.NEG  object:wine`, with no tense or aspect and no subject, unless it's `subject:2PL` for several people. Anything the generator can't express yet, like a question, is an error naming the line, never silently dropped. See [examples/scene.txt](examples/scene.txt) and `ClauseFile`.

A noun file sits beside it, or wherever `--nouns` says, and tells the language what its nouns name, one to a line:

```
marta F
miller HUM
crow ANIM
river THING
```

`M` and `F` are for words that themselves mean a male or a female being, and for names, `HUM` is any other person, `ANIM` any other creature, and `THING` the rest. This says what a word means, not what gender it has, since one clause file can go into any language, and each language decides what to make of it. It never changes a root. The file is optional, but a noun that isn't in it counts as a thing, so a text without one gets gender only from form. `translate` reads `hollow.nouns.txt` for `hollow.clauses.txt` without being told.

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

[examples/hollow.txt](examples/hollow.txt), a short text adventure, is the test of what real text needs. The front end refused 49 of its 190 sentences. Refusals are the easy part, though: a refusal names what's missing, while a sentence that glosses can still lose half of what it said. *A dog barks somewhere behind the mill* comes out as 'a dog barks at the mill', *nobody believes him, because he drinks* as two unconnected clauses, and *at dusk* doesn't come out at all. So this list counts both.

### Clauses

Roughly in the order to build them, since the later ones lean on the earlier ones.

- **Adverbs.** Manner (*carefully*, *slowly*), time (*now*, *yesterday*, *last night*, *a month later*), place (*here*, *upstream*) and degree (*very*, *much*), along with durations like *for two hundred years*. All of them are left out of glosses today.
- **Spatial adpositions.** *In*, *on*, *under*, *behind*, *between* and *in front of* all collapse into `location:`, and *against* can't be said. They're often nouns, as in 'at the house's underside', and a language can use an adposition and a case together, so this comes with a rework of how role markers are chosen. A noun phrase should be able to take one too, for *the old woman from the gate*.
- **Existentials.** 'There is a dog in the house', which many languages say unlike 'the dog is in the house'.
- **Modals.** Ability, permission and obligation (*can*, *must*, *should*, *have to*), by verbs, affixes or particles. The largest group of refusals, since game text is full of them, as in *you must find the key*.
- **Questions.** Yes-no questions, and content questions with *who*, *what*, *where* and *why*, by a particle, by word order, or by intonation alone, with question words in place or fronted, in WALS's proportions.
- **Coordination.** *And*, *or* and *but* between clauses and between nouns. The front end gets around *and* by splitting *a table, a broken chair, and a chest* into three sentences, and can't do anything with *a rope or a boat*.
- **Subordinate clauses.** Conditionals, which are what game instructions are made of, then *when*, *before*, *until*, *because*, and purpose (*to open your bag*).
- **Complement clauses and reported speech.** *I told you that you were late*, *she wants to go home*. Today *"You're late," she says* glosses as a separate clause, *she says*, that doesn't say what she said.
- **Relative clauses.** *Nobody who goes into the forest*, *a clearing where nothing grows*, by a relativizer, a gap or a resumptive pronoun, before or after the noun as WALS has them.
- **Comparison.** The next largest group of refusals: *taller than most of the men*, *warmer than it should*, *it shines like water*. WALS's types are all reachable from what's above: a locational standard ('tall from the men'), a verb meaning 'exceed', two conjoined clauses ('she is tall, they are short'), or a particle.
- **Voice.** Passives, so that *each light is carried by a small figure* needn't be turned around, along with reflexives and causatives.
- **More tense and aspect.** A perfect (*has fallen*, *have gone missing*), which is glossed as a plain past today, a progressive, and phase verbs like *begin to*.

### Noun phrases

- **Demonstratives.** *This village* loses its *this*, and a bare *this* or *that* becomes 'it'.
- **Quantifiers and indefinites.** *All*, *each*, *most*, *few*, *someone*, *everything*, and above all *nobody*, which can only be 'anyone' with a negated verb, a word the language coins as if it were a noun. Languages differ in whether a negative indefinite takes ordinary negation too.
- **Nouns modifying nouns.** `(key small iron)` and `(bow short yew)` pass a noun off as an adjective, and the parser can't tell. These need a form of their own, a compound, a genitive or a derived adjective, as each language prefers.
- **Possessors of possessors.** *My sister's son*, since parentheses don't nest.
- **Measure phrases.** *A bowl of stew*, by a genitive or plain apposition, and numeral classifiers in the languages that have them.
- **Particles of focus.** *Only*, *still*, *just*, *again*, *even*.
- **Direction on verbs.** *Walk down*, *turn around*, *hold up*, whose particles are dropped today.

### Game text

- **Proper names.** *Marta* gets coined as a root like any word, and *Hollow* gets dropped. A name should come through recognizably, fitted to the language's sounds, and inflect or not as the language's names do.
- **Fragments.** Game text is full of headings and item names (*Item: Rusty Key*, *New quest: The Missing Children*), which need a form of their own for a bare noun phrase in place of an invented *the item is*.
- **Vocatives and interjections.** *Stranger*, *no*. `addressee:` is already reserved as a role.
- **Reference.** A pronoun's gender comes from the English, so 'it' stays neuter for a noun that's feminine in the language. Knowing what a pronoun refers to would fix that.

### Grammar in general

- **Inflection classes and irregularity.** Declensions and conjugations, stem changes, and irregular common words (*went*, *better*), which do as much as anything to make a language feel lived in.
- **Agreement.** Absolutive agreement, object agreement and differential object marking.
- **More verb categories.** Evidentiality and more moods.
- **Inalienable possession.** Separate marking for body parts and kin, as in many languages.
- **Politeness.** Formal 'you' and registers, for games whose characters have rank.

### Words

- **Word frequency.** A bundled lemma frequency table (with English length as the fallback for words it lacks), so frequent words come out shorter, per Zipf's law of abbreviation.
- **Derivation and compounding,** for words like *teacher* or *sunflower*, starting with agent nouns, which hollow.txt is full of (*hunter*, *miller*, *ferryman*), then participles (*broken*, *healing*), and motion nouns, the pairs like Latin *actor* and *actrix* or Spanish *panadero* and *panadera* that name a man or a woman with what are effectively two words, which may fall in different declensions. Checks against accidental homophones are harder than they sound: avoiding one would make a root depend on the words coined before it.
- **Colexification.** Every English word gets a root of its own, which makes every language a calque of English. Real languages divide meaning differently, with one word for 'hand' and 'arm', or 'blue' and 'green', in WALS's proportions.

### Sounds

- **Free stress,** which each word stores, as in Russian or Spanish, with accents for orthographies to mark it where it's unpredictable, as Spanish marks *teléfono*. It's harder to learn than the predictable stress there is today. Secondary stress, too, if anything needs it.
- **Diphthongs.** Vowels are single segments today, long or short. A vowel followed by a glide in its coda already sounds like one (*aj*, *aw*) but isn't written or treated as one, and the close vowels Greek contraction leaves beside other vowels are where Greek made its diphthongs.
- **Inventory sizes against real ones.** Inventories should have the size spread real ones have when counted over the sounds generated here, which PHOIBLE's inventories can check.
- **More sound changes.** Allophones that aren't phonemes of the language, and changes across word boundaries, like French liaison.

### Beyond one language

- **Language families.** Daughter languages that descend from a shared ancestor by regular sound change and grammatical drift, with the cognates that gives them, for a generated world's history. The ordered sound changes are a start on this.
- **Borrowing.** Loanwords adapted to the sounds of the borrowing language, between languages in contact.
- **Writing systems.** Generated scripts, alphabets, syllabaries and logographies, which is half of what a game like Chants of Sennaar is.
- **Place and personal names.** Built from the lexicon, as in Dwarf Fortress, so that a village's name means something in the language it's named in.
