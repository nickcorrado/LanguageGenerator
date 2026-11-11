# Roadmap

[examples/hollow.txt](../examples/hollow.txt), a short text adventure, is the test of what real text needs. The front end refused 49 of its 190 sentences. Refusals are the easy part, though: a refusal names what's missing, while a sentence that glosses can still lose half of what it said. *A dog barks somewhere behind the mill* comes out as 'a dog barks at the mill', *nobody believes him, because he drinks* as two unconnected clauses, and *at dusk* doesn't come out at all. So this list counts both.

## Clauses

Roughly in the order to build them, since the later ones lean on the earlier ones.

- **Adverbs.** Manner (*carefully*, *slowly*), time (*now*, *yesterday*, *last night*, *a month later*), place (*here*, *upstream*) and degree (*very*, *much*), along with durations like *for two hundred years*. All of them are left out of glosses today.
- **Spatial adpositions.** *In*, *on*, *under*, *behind*, *between* and *in front of* all collapse into `location:`, and *against* can't be said. They're often nouns, as in 'at the house's underside', and a language can use an adposition and a case together, so this comes with a rework of how role markers are chosen. A noun phrase should be able to take one too, for *the old woman from the gate*.
- **Existentials.** 'There is a dog in the house', which many languages say unlike 'the dog is in the house'.
- **Modals.** Ability, permission and obligation (*can*, *must*, *should*, *have to*), by verbs, affixes or particles. The largest group of refusals, since game text is full of them, as in *you must find the key*.
- **Questions.** Yes-no questions, and content questions with *who*, *what*, *where* and *why*, by a particle, by word order, or by intonation alone, with question words in place or fronted, in WALS's proportions.
- **More coordination.** Where the conjunction goes, which WALS doesn't count and is always between the two things here, though Latin *-que* follows the second; 'both … and' and 'either … or'; dropping a repeated subject (*she turns and walks away*); and coordinated addressees (*Marta and Ann, come*).
- **More subordinate clauses.** Subordinators share forms in regular ways ('when' and 'if', 'after' and 'because', 'before' and 'until'), which a semantic map could model as the role map does; reduced clauses often take a case, purpose the dative or allative and reason the ablative, which would tie them to the role map; deranked copular clauses; *although*; and the subject of a deranked clause in the genitive, as many languages put it.
- **Complement clauses and reported speech.** *I told you that you were late*, *she wants to go home*. Today *"You're late," she says* glosses as a separate clause, *she says*, that doesn't say what she said.
- **Relative clauses.** *Nobody who goes into the forest*, *a clearing where nothing grows*, by a relativizer, a gap or a resumptive pronoun, before or after the noun as WALS has them.
- **More comparison.** WALS's fourth type, with a verb meaning 'exceed', as in Swahili or Yoruba; similes (*it shines like water*); comparatives of adverbs (*slower than before*), inside a noun phrase (*stronger arms than yours*) or with a clause as the standard (*warmer than it should*); superlatives; and a comparative form of the adjective, like English *-er*.
- **More voice.** Antipassives, which ergative languages favor (WALS 108A), starting with the kind that demotes an indefinite object; reflexives; and causatives.
- **More tense and aspect.** A perfect (*has fallen*, *have gone missing*), which is glossed as a plain past today, a progressive, and phase verbs like *begin to*.

## Noun phrases

- **Demonstratives.** *This village* loses its *this*, and a bare *this* or *that* becomes 'it'.
- **Quantifiers and indefinites.** *All*, *each*, *most*, *few*, *someone*, *everything*, and above all *nobody*, which can only be 'anyone' with a negated verb, a word the language coins as if it were a noun. Languages differ in whether a negative indefinite takes ordinary negation too.
- **Nouns modifying nouns.** `(key small iron)` and `(bow short yew)` pass a noun off as an adjective, and the parser can't tell. These need a form of their own, a compound, a genitive or a derived adjective, as each language prefers.
- **Measure phrases.** *A bowl of stew*, by a genitive or plain apposition, and numeral classifiers in the languages that have them.
- **Particles of focus.** *Only*, *still*, *just*, *again*, *even*.
- **Direction on verbs.** *Walk down*, *turn around*, *hold up*, whose particles are dropped today.

## Game text

- **Proper names.** *Marta* gets coined as a root like any word, and *Hollow* gets dropped. A name should come through recognizably, fitted to the language's sounds, and inflect or not as the language's names do.
- **Fragments.** Game text is full of headings and item names (*Item: Rusty Key*, *New quest: The Missing Children*), which need a form of their own for a bare noun phrase in place of an invented *the item is*.
- **Interjections and bare vocatives.** *No*, *hello*, and a vocative on its own, like *Stranger!*, which has no clause to go with.
- **Reference.** A pronoun's gender comes from the English, so 'it' stays neuter for a noun that's feminine in the language. Knowing what a pronoun refers to would fix that.

## Grammar in general

- **Inflection classes and irregularity.** Declensions and conjugations, stem changes, and irregular common words (*went*, *better*), which do as much as anything to make a language feel lived in.
- **Agreement.** Absolutive agreement, object agreement and differential object marking.
- **More verb categories.** Evidentiality and more moods.
- **Inalienable possession.** Separate marking for body parts and kin, as in many languages.
- **Politeness.** Formal 'you' and registers, for games whose characters have rank.

## Words

- **Word frequency.** A bundled lemma frequency table (with English length as the fallback for words it lacks), so frequent words come out shorter, per Zipf's law of abbreviation.
- **Derivation and compounding,** for words like *teacher* or *sunflower*, starting with agent nouns, which hollow.txt is full of (*hunter*, *miller*, *ferryman*), then participles (*broken*, *healing*), and motion nouns, the pairs like Latin *actor* and *actrix* or Spanish *panadero* and *panadera* that name a man or a woman with what are effectively two words, which may fall in different declensions. Checks against accidental homophones are harder than they sound: avoiding one would make a root depend on the words coined before it.
- **Colexification.** Every English word gets a root of its own, which makes every language a calque of English. Real languages divide meaning differently, with one word for 'hand' and 'arm', or 'blue' and 'green', in WALS's proportions.

## Sounds

- **Free stress,** which each word stores, as in Russian or Spanish, with accents for orthographies to mark it where it's unpredictable, as Spanish marks *teléfono*. It's harder to learn than the predictable stress there is today. Secondary stress, too, if anything needs it.
- **Diphthongs.** Vowels are single segments today, long or short. A vowel followed by a glide in its coda already sounds like one (*aj*, *aw*) but isn't written or treated as one, and the close vowels Greek contraction leaves beside other vowels are where Greek made its diphthongs.
- **Inventory sizes against real ones.** Inventories should have the size spread real ones have when counted over the sounds generated here, which PHOIBLE's inventories can check.
- **More sound changes.** Allophones that aren't phonemes of the language, and changes across word boundaries, like French liaison.

## Beyond one language

- **Language families.** Daughter languages that descend from a shared ancestor by regular sound change and grammatical drift, with the cognates that gives them, for a generated world's history. The ordered sound changes are a start on this.
- **Borrowing.** Loanwords adapted to the sounds of the borrowing language, between languages in contact.
- **Writing systems.** Generated scripts, alphabets, syllabaries and logographies, which is half of what a game like Chants of Sennaar is.
- **Place and personal names.** Built from the lexicon, as in Dwarf Fortress, so that a village's name means something in the language it's named in.

## Configuring generation

Fixing some of a language's properties before it's generated, like its alignment, its word order or its phonemic inventory, and drawing the rest as usual. This is a plan, not a feature yet. A patch to a throwaway copy, which fixed vowels, consonants, syllable template, word order, fusion and alignment in about seven lines, suggests the generator suits it well:

- 3,024 languages covering every combination of word order, alignment, fusion and syllable template rendered all of hollow.txt's clauses and every paradigm without an error.
- 500 random inventories and 13 hand-picked extreme ones, like stops only or a single consonant and vowel, gave no errors and no sounds outside the inventory. Only an empty vowel or consonant list crashed.
- Fixing properties to the values a seed already gave reproduced the same language 300 times out of 300.
- Changing alignment or word order left every root the same in 200 of 200 languages. Changing the vowels changed the roots in all 200.

Those runs show that the pipeline holds together, not that every combination reads well.

- **The spec.** `Generate` would take a `LanguageSpec` beside the seed, with each setting either free or one of a set of values. That's about 45 single-choice settings and 15 structured ones. Inventories would be IPA strings and everything else enum names, so a spec is a small JSON file, and `--spec file.json` gets the feature to the command line before there's any frontend. Whether a blank field can simply mean free, or every field has to be stated, is still to decide.
- **Fixing a value by redrawing.** Most settings need no change to their stage: it's drawn again until the result matches, which keeps the conditional odds right (fixing SOV still draws adposition order from the OV counts) and leaves a seed's language alone when its first draw already matches. Giving up after a set number of draws is a safety net for conflicts nothing else caught.
- **Conflicts between stages.** This is the real design work. Generation only runs forward, and it often overrides a value without saying so: isolating morphology turns prefixed negation into a particle and head-marked possession into dependent marking, and takes away gender markers; syllable contact and coda sound changes need a template with codas; verbal predicative adjectives can't have adjective agreement; vowel harmony and unstressed reduction need particular vowels. A table of perhaps 10 to 20 rules, like "prefixed negation requires not isolating", would reject impossible specs, narrow the earlier stages, and tell a frontend what to grey out and why. A test that generates random specs and checks that each is either rejected or satisfied would catch any rule that's missing.
- **Inventories.** Exact inventories already work, apart from validating that each list has something in it and ranking the sounds by markedness as generated inventories are. Very small ones need a warning: a typical language has 10 to 15 homophone pairs among 250 roots, a stops-only one 32, and a glides-only one 110. Partial inventories (these sounds required, those forbidden, this many vowels) are the most new code, since consonants are built by a long chain of separate decisions and requiring a specific set can't be done by redrawing. Inventories can only use the existing 53 phonemes.
- **Other structured settings.** Sound changes are an ordered list drawn change by change, so requiring or forbidding one means replacing those draws with a helper that consults the spec, and checking that the change can apply.
- **Keeping it up as the generator grows.** Everything on this list will want settings of its own, so each setting should be declared once, as a name, group, values and stage, with the spec, the JSON and the form controls all built from that list. Exposing a new feature is then a few declarations and maybe a conflict rule. A stage nobody exposes just stays random, so settings can lag behind the generator. The command line and a frontend should render one structured description rather than each having their own, which means splitting `LanguageDescription`, which only writes text today. What can't be designed away is rework of existing stages, since settings mirror the shape of what they control: the rework of role markers above would invalidate settings for case sets, and free stress, diphthongs, inflection classes and more agreement would each change settings too. A spec that no longer fits should fail with a clear error rather than be reinterpreted.
- **A frontend.** A console app would be unwieldy for this beyond the spec file. Blazor, run locally, would share the C# types and build its forms from the declared settings, and generation at about 32 ms makes a live preview easy. It could move to WebAssembly later for a public site, though its speed there is unmeasured. Settings would be grouped on the left and the language on the right, with its paradigms and sample glossed sentences, and an inventory would be a clickable IPA chart with each sound required, forbidden or free. The main workflow would be to generate, lock what you like and reroll the rest, which independent stages make nearly free.
- **Order to build it.** The spec, the declared settings, the single-choice settings, validation and `--spec` first, at a rough guess about a week. Then partial inventories, sound changes, the conflict rules and the random-spec test, about another week. Settings for case sets should wait for the role marker rework. The frontend, one to two weeks, should wait until the morphosyntax above settles, or be only a thin generated form until then.
- **What it leaves out.** Fixing a later property doesn't reshape earlier ones, so fixing isolating won't make VO likelier. A seed and a spec reproduce a language only on the same version of the generator, so saving a finished language for good would be a separate feature, as would writing your own roots and affixes.
