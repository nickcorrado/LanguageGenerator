# Spec format

A spec fixes some of a language's settings before it's generated, and the rest are drawn as usual. Pass one to the command line with `--spec`:

```
LanguageGenerator.Cli 7 --spec ergative-vos.json
LanguageGenerator.Cli translate examples/hollow.clauses.txt 7 --spec ergative-vos.json
```

A spec is a JSON object naming each setting it fixes:

```json
{
  // comments are allowed
  "constituent-order": "VOS",
  "adpositions": ["Prepositions", "Postpositions"],
  "alignment": "ErgativeAbsolutive",
  "consonants": ["t", "k", "n", "s", "m", "l", "p"],
  "vowels": ["a", "i", "u"],
  "mirrors-adjectives": true,
  "dropoff": 0.3
}
```

A setting left out is free. A value fixes a setting to it, and an array of values lets it be any of them, so `"adpositions": ["Prepositions", "Postpositions"]` above says nothing more than leaving it out would. A setting the generator doesn't know is an error, and so is a value it doesn't have, naming the ones it does. `Settings` in the code declares every setting, and `SpecFile` reads and writes specs.

## How a spec is met

The generator draws each part of a language in stages, and a stage with a fixed setting is drawn again until it matches, so the settings it leaves free are drawn given the fixed ones: fixing SOV makes postpositions likelier, as it does in real languages. A seed whose language already matches a spec is unchanged by it.

Some stages depend on others, so they're drawn together: word order, morphology, alignment, role regions, cases, and how configurations are marked. They're drawn again together until all of their fixed settings match, so fixing a spatial case makes morphology that inflects likelier, and fixing isolating morphology makes VO likelier. Only the stages up to the last one with a fixed setting are drawn again, so fixing the alignment keeps the seed's role regions. The edges of the role and configuration maps are drawn independently of each other, so a fixed edge is just written in, and the seed keeps the rest.

Configuration is partly for languages the odds would never give, so a value no draw reaches is set directly instead. Where the stages drawn together can't all match, they're drawn one at a time, each given the ones before it, and what still doesn't match is written in.

Rules keep a value written in from contradicting what was drawn before it: a spatial case needs morphology that isn't isolating, so fixing one draws the morphology from the rest. The same rules refuse a spec that contradicts itself, before anything is generated, with the reason:

- A coda restriction needs a syllable template with a coda.
- A sound change that works on codas, like final devoicing or syncope, needs a template with a coda, and devoicing or weakening a coda needs a coda that can be any consonant. A syllable contact restriction needs a template with a coda, and a contact repair needs a restriction.
- A sound change needs the sounds it works with: unstressed reduction needs ə, unstressed raising a mid vowel besides i and u, harmony a pair of vowels it can match, the changes of length long vowels, glide formation and glide insertion j or w, and glottal insertion ʔ or h. Where the inventory is free, it's drawn with them.
- Obliques can only go between the object and the verb where the object comes first.
- An oblique case needs its role to head its region: a dative can't be fixed along with the edge joining recipients to possessors, since the genitive would mark them both.
- A configuration marked by case needs nouns that inflect, and a case on the location's region.
- A configuration said like at needs edges joining it to at, one with a marker of its own can't be joined to at, and configurations joined into one region share its marker.
- An isolating language has no affixes, so negation and polar questions can't be a prefix or suffix, possession can't be marked on the possessed noun, and a comparative, similative or modal can't be an affix.
- Modalities joined by an edge share a marker, so two marked differently can't be joined.
- Possessive affixes can only be prefixes where possession is marked on the possessed noun.
- An inventory needs at least one sound, and each sound once, so not every vowel or every consonant can be forbidden.
- A count has to leave room for the sounds the spec requires and its sound changes need, can't be more than the sounds it doesn't forbid, and has to match an exact inventory.
- The dropoff is between 0 and 1.

A setting that does nothing in the language is accepted, like affixation in an isolating language, whose markers are all words of their own.

A spec and a seed give the same language only on the same version of the generator, though an older spec still fits a newer generator, whose new settings are just free.

## Inventories

`vowels` and `consonants` are arrays of IPA symbols, commonest first: the order is how often each sound turns up, the first the most, as in Mark Rosenfelder's generators. `dropoff` says how steeply: each sound is (1 − dropoff) times as likely as the one before it, so the usual 0.15 is gentle, and 0.5 makes each sound half as likely as the last.

A fixed inventory needn't look like a real one: z without s, which a drawn inventory never has, is fine. With six consonants or fewer, or two vowels or fewer, the command line warns that many more words will sound alike than usual.

An inventory can instead give just the sounds it requires and forbids, and the rest are drawn as usual:

```json
"consonants": { "require": ["θ", "q"], "forbid": ["h"] }
```

The inventory is drawn again until it has every required sound and none forbidden, so the sounds left free are drawn given them: requiring z makes s all but certain, since z comes with s. Where no draw has them all, as for forbidding a, which every drawn vowel system has, the closest draw has the rest written in: a required sound goes where it would usually rank, and a forbidden one is taken out. That takes the generator's whole limit of draws, about half a second for each inventory.

`vowel-count` and `consonant-count` say how many sounds an inventory has: a number, an array of the numbers allowed, or a range, as `{ "min": 18, "max": 24 }`, either end of which can be left out. A count no draw gives is written in by cutting the rarest sounds or adding the commonest, keeping every sound the spec requires and every sound its sound changes need.

The symbols are the generator's 53 phonemes, given exactly, so `ʦ` and not `ts`, and `aː` with the length mark and not a colon. Long vowels are sounds of their own:

- Vowels: a e i æ ɛ ə ɨ ɔ o u iː eː aː oː uː
- Consonants: the error for a symbol the generator doesn't have lists them all.

## Settings

| Setting | Values |
| --- | --- |
| `vowels`, `consonants` | An array of IPA symbols, commonest first, or `{ "require": [...], "forbid": [...] }` |
| `vowel-count`, `consonant-count` | A number of sounds from 1 to 15 vowels or 38 consonants, an array of them, or `{ "min": ..., "max": ... }` |
| `dropoff` | A number between 0 and 1; 0.15 if free |
| `syllable-template` | `CV`, `CVF`, `CLV`, `CLVF`, `SCV`, `SCVF`, `SCLVF`, where C is a consonant, V a vowel, F a final consonant, L a liquid or glide after the first consonant and S a sibilant before it |
| `coda-restriction` | `Any`, `Sonorants`, `Nasals`, `SingleNasal` |
| `stress` | `Initial`, `Second`, `Third`, `Antepenultimate`, `Penultimate`, `Ultimate`, `UltimateIfHeavy`, `PenultimateIfHeavy`, `InitialIfHeavy`, `LastHeavy` |
| `cluster-repair` | `Epenthesis`, `Deletion` |
| `syllable-contact` | `Any`, `NoSteepRise`, `NoRise`, what may meet across a syllable boundary |
| `nasal-assimilation`, `degemination`, `intervocalic-voicing`, `lenition`, `voicing-assimilation`, `final-devoicing`, `glide-formation` | `true` if the language has the sound change |
| `vowel-harmony` | `None`, `FrontBack`, `TongueRoot` |
| `hiatus-resolution` | `None`, `VowelElision`, `VowelCoalescence`, `GreekContraction`, `GlideInsertion`, `GlottalInsertion`, for vowels that meet where morphemes do |
| `coda-devoicing`, `coda-weakening`, `compensatory-lengthening`, `closed-syllable-laxing`, `open-syllable-lengthening`, `unstressed-shortening`, `syncope` | `true` if the language has the change, which applies once words have syllables |
| `unstressed-vowels` | `None`, `Reduction`, `Raising`, what happens to vowels without the stress |
| `contact-repair` | `None`, `CodaAssimilation`, `OnsetHardening`, `CodaNasalization` |
| `constituent-order` | `SOV`, `SVO`, `VSO`, `VOS`, `OVS`, `OSV` |
| `adpositions` | `Prepositions`, `Postpositions` |
| `adjective-order` | `NounAdjective`, `AdjectiveNoun` |
| `degree-word-order` | `AdjectiveDegree`, `DegreeAdjective`, for 'big very' or 'very big' |
| `oblique-position` | `BeforeVerbAndObject`, `BetweenObjectAndVerb`, `AfterVerbAndObject` |
| `mirrors-adjectives` | `true` if adjectives after the noun mirror their order before it, `false` if they keep it |
| `fusion` | `Concatenative`, `Mixed`, `Isolating` |
| `affixation` | `StronglySuffixing`, `WeaklySuffixing`, `Equal`, `WeaklyPrefixing`, `StronglyPrefixing` |
| `case-with-number`, `tam-with-agreement`, `tense-with-aspect`, `case-with-localization` | `true` if the two share one marker |
| `zero-singular`, `zero-core-case`, `zero-present` | `true` if the singular, the intransitive subject's case or the present has no marker |
| `zero-third-person` | `None`, `Singular`, `All` |
| `alignment` | `NominativeAccusative`, `ErgativeAbsolutive`, `None` |
| `roles-companion-instrument`, `roles-recipient-goal`, `roles-goal-location`, `roles-source-possessor`, `roles-possessor-recipient`, `roles-instrument-location`, `roles-location-source`, `roles-instrument-source`, `roles-agent-instrument`, `roles-agent-source`, `roles-agent-location`, `roles-time-location`, `roles-route-location`, `roles-route-instrument`, `roles-route-source` | `true` if the edge of the role map joins its two roles, so they share a marker |
| `genitive-case`, `dative-case`, `locative-case`, `ablative-case`, `instrumental-case`, `comitative-case`, `allative-case`, `perlative-case`, `vocative-case` | `true` if the language has the case. Core cases follow the alignment |
| `configurations-at-in`, `configurations-at-on`, `configurations-at-beside`, `configurations-in-on`, `configurations-in-between`, `configurations-on-over`, `configurations-on-against` | `true` if the edge of the configuration map joins its two configurations, so they share a marker, or for an edge to at, are said as plainly as 'at' |
| `in-marker`, `on-marker`, `under-marker`, `over-marker`, `behind-marker`, `in-front-marker`, `beside-marker`, `between-marker`, `around-marker`, `against-marker` | `Adposition`, `RelationalNoun` or `Case`, how the configuration's region is marked, or `At` if it's said like at. Under, behind, in front and around are never said like at |
| `configuration-marking` | `Adposition`, `RelationalNoun`, `Case`, the way of marking configurations most of the language's regions follow |
| `negation` | `ParticleBefore`, `ParticleAfter`, `Prefix`, `Suffix`, `Double`, for a word on each side of the verb |
| `polar-questions` | `InitialParticle`, `FinalParticle`, `SecondParticle`, `Prefix`, `Suffix`, `Inversion`, `Intonation` |
| `fronts-question-words` | `true` if a question word goes first in its clause, as English who does |
| `passive` | `true` if the language has a passive |
| `modalities-ability-permission`, `modalities-permission-obligation`, `modalities-obligation-advisability` | `true` if the edge of the modality map joins its two modalities, so they share a marker, as English can is both 'able to' and 'allowed to' |
| `ability-modal`, `permission-modal`, `obligation-modal`, `advisability-modal` | `Affix`, `Verb` or `Particle`, how 'can', 'may', 'must' and 'should' are marked |
| `possession` | `Dependent`, marking the possessor, `Head`, marking the possessed noun, `Double`, marking both, or `None` |
| `possessive-affixes` | `Prefix` or `Suffix`, the side of the possessed noun its affix goes on, where possession is marked there |
| `genders` | `None`, `MasculineFeminine`, `MasculineFeminineNeuter`, `AnimateInanimate` |
| `gender-assignment` | `Semantic`, by what a noun means, or `SemanticAndFormal`, by how it ends too |
| `comparison` | `Locational`, marking the standard as a place, `Exceed`, with a verb meaning 'exceed', `Conjoined`, with two clauses side by side, or `Particle`, with a word like than |
| `comparative`, `similative` | `Affix` or `Word`, how 'more' and 'like' are marked |
