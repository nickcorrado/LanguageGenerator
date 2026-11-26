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
  "consonant-dropoff": 0.3
}
```

A setting left out is free. A value fixes a setting to it, and an array of values lets it be any of them, so `"adpositions": ["Prepositions", "Postpositions"]` above says nothing more than leaving it out would. A setting the generator doesn't know is an error, and so is a value it doesn't have, naming the ones it does. `Settings` in the code declares every setting, and `SpecFile` reads and writes specs.

## How a spec is met

The generator draws each part of a language in stages, and a stage with a fixed setting is drawn again until it matches, so the settings it leaves free are drawn given the fixed ones: fixing SOV makes postpositions likelier, as it does in real languages. A seed whose language already matches a spec is unchanged by it.

Some stages depend on others, so they're drawn together: word order, morphology, alignment, role regions, cases, and how configurations are marked. They're drawn again together until all of their fixed settings match, so fixing a spatial case makes morphology that inflects likelier, and fixing isolating morphology makes VO likelier. Only the stages up to the last one with a fixed setting are drawn again, so fixing the alignment keeps the seed's role regions. The edges of the role and configuration maps are drawn independently of each other, so a fixed edge is just written in, and the seed keeps the rest.

Configuration is partly for languages the odds would never give, so a value no draw reaches is set directly instead. Where the stages drawn together can't all match, they're drawn one at a time, each given the ones before it, and what still doesn't match is written in.

Rules keep a value written in from contradicting what was drawn before it: a spatial case needs morphology that isn't isolating, so fixing one draws the morphology from the rest. The same rules refuse a spec that contradicts itself, before anything is generated, with the reason:

- A coda restriction needs a syllable template with a coda.
- A sound change that works on codas, like final devoicing or syncope, needs a template with a coda, and devoicing or weakening a coda needs a coda that can be any consonant. A syllable contact restriction needs a template with a coda, and a contact repair needs a restriction. Accent retraction needs free stress, since only a stored accent moves.
- A sound change needs the sounds it works with: unstressed reduction needs ə, unstressed raising a mid vowel besides i and u, harmony a pair of vowels it can match, the changes of length long vowels, diphthong formation and unstressed monophthongization a diphthong, glide formation and glide insertion j or w, glottal insertion ʔ or h, and Verner's law a voiceless fricative and its voiced counterpart. Where the inventory is free, it's drawn with them.
- Obliques can only go between the object and the verb where the object comes first.
- An oblique case needs its role to head its region: a dative can't be fixed along with the edge joining recipients to possessors, since the genitive would mark them both.
- A configuration marked by case needs nouns that inflect, and a case on the location's region.
- A configuration said like at needs edges joining it to at, one with a marker of its own can't be joined to at, and configurations joined into one region share its marker.
- An isolating language has no affixes, so negation and polar questions can't be a prefix or suffix, possession can't be marked on the possessed noun, and a comparative, similative, modal, perfect, progressive, habitual, inceptive, cessative, article or demonstrative can't be an affix.
- An article agreeing in gender needs genders, and an article that can agree: a definite article, or an indefinite one that isn't the word for 'one'.
- A demonstrative agreeing in gender needs genders, and one that's an affix can't stand alone, so its pronouns need stems of their own.
- Only a definite article of its own, a word or an affix, can stay beside a demonstrative.
- Modalities joined by an edge share a marker, so two marked differently can't be joined, and the same goes for subordinators marked in different places.
- An isolating language can't mark a subordinate clause or a complement with a suffix, or say 'want' or mark a relative clause with an affix.
- A quotation can only be marked with the complementizer where a proposition has one.
- 'And' between nouns that's the word for 'with' doesn't join clauses, so clauses can't be joined like nouns then.
- Possessive affixes can only be prefixes where possession is marked on the possessed noun.
- An inventory needs at least one sound, and each sound once, so not every vowel or every consonant can be forbidden.
- A count has to leave room for the sounds the spec requires and its sound changes need, can't be more than the sounds it doesn't forbid, and has to match an exact inventory.
- A dropoff is between 0 and 1.

A setting that does nothing in the language is accepted, like affixation in an isolating language, whose markers are all words of their own.

A spec and a seed give the same language only on the same version of the generator, and an older spec usually still fits a newer generator, whose new settings are just free. A setting that's renamed or split doesn't carry over: `dropoff` is now `consonant-dropoff` and `vowel-dropoff`.

## Inventories

`vowels` and `consonants` are arrays of IPA symbols, commonest first: the order is how often each sound turns up, the first the most, as in Mark Rosenfelder's generators. `consonant-dropoff` and `vowel-dropoff` say how steeply: each sound is (1 − dropoff) times as likely as the one before it, so the usual 0.09 for consonants is gentle, the usual 0.3 for vowels steep, and 0.5 makes each sound half as likely as the last. Both are set so that generated words use their sounds about as unevenly as NorthEuraLex's word lists do, where one or two vowels make up most of a word's but consonants spread out.

A fixed inventory needn't look like a real one: z without s, which a drawn inventory never has, is fine. With six consonants or fewer, or two vowels or fewer, the command line warns that many more words will sound alike than usual.

An inventory can instead give just the sounds it requires and forbids, and the rest are drawn as usual:

```json
"consonants": { "require": ["θ", "q"], "forbid": ["h"] }
```

The inventory is drawn again until it has every required sound and none forbidden, so the sounds left free are drawn given them: requiring z makes s all but certain, since z comes with s. Where no draw has them all, as for forbidding a, which every drawn vowel system has, the closest draw has the rest written in: a required sound goes where it would usually rank, and a forbidden one is taken out. That takes the generator's whole limit of draws, about half a second for each inventory.

`vowel-count` and `consonant-count` say how many sounds an inventory has: a number, an array of the numbers allowed, or a range, as `{ "min": 18, "max": 24 }`, either end of which can be left out. A count no draw gives is written in by cutting the rarest sounds or adding the commonest, keeping every sound the spec requires and every sound its sound changes need.

The symbols are the generator's 64 phonemes, given exactly, so `ʦ` and not `ts`, and `aː` with the length mark and not a colon. Long vowels and diphthongs are sounds of their own, and a diphthong is written as its two vowels, so `ai`:

- Vowels: a e i æ ɛ ə ɨ ɔ o u iː eː aː oː uː ai au ei ou oi ui
- Consonants: the error for a symbol the generator doesn't have lists them all.

## Allophones

A sound change can make a sound the language doesn't have as a phoneme, as Spanish says the b of *lobo* as β. Such a sound is an allophone: it's written as the phoneme it's said for, and it fills the same places in a syllable, so it shows only in the IPA. Where the change makes one of the language's own phonemes, the two merge, and the word is spelled with the new one. A language may say any sound it lacks this way, besides three that are never phonemes, the lax vowels ɪ and ʊ and the bilabial ɸ, unless the spec forbids it:

```json
"allophones": { "forbid": ["θ", "ɸ"] }
```

Forbidding a sound in `consonants` or `vowels` only keeps it out of the inventory, so a language without θ may still say its t as θ between vowels. To keep a sound out of the language altogether, forbid it in both. `allophones` can also be an exact array of the sounds allowed, which may be empty, for sound changes that make only phonemes.

## Where words meet

Four sound changes can also apply where one word ends and the next begins: nasal assimilation, intervocalic voicing, lenition and voicing assimilation. Each does so only in a language that has it within words, as Spanish lenition reaches across words in *la boca*, said [laˈβoka]. These changes alter only how a word is said, never how it's spelled, since spelling keeps a word the same wherever it stands. A language with final consonant loss silences a word's last obstruent after a vowel and says it again only before a vowel, as French *petit* is [pəti] but *petit ami* [pətit‿ami]. The consonant is still spelled, and it needs codas that can be any consonant.

Words without a root, particles, are clitics. Each leans on the word with a root beside it, on the side syntax put it, and is said as one word with it. That way a clitic can lose its vowel before a vowel, as French *le ami* is *l'ami*, or take its host's harmony. `clitic-stress` decides whether the stress falls by the rule over the whole group, as in Latin *virúmque*, or stays where the host alone has it, as in Spanish *dígamelo*. Clitics are still written as words of their own. The interlinear joins each one to its host with `=`, as the Leipzig rules do.

## Settings

| Setting | Values |
| --- | --- |
| `vowels`, `consonants` | An array of IPA symbols, commonest first, or `{ "require": [...], "forbid": [...] }` |
| `vowel-count`, `consonant-count` | A number of sounds from 1 to 21 vowels or 43 consonants, an array of them, or `{ "min": ..., "max": ... }` |
| `consonant-dropoff`, `vowel-dropoff` | A number between 0 and 1; 0.09 for consonants and 0.3 for vowels if free |
| `allophones` | An array of IPA symbols a sound change may make beyond the inventory, or `{ "require": [...], "forbid": [...] }`; every sound if free |
| `syllable-template` | `CV`, `CVF`, `CLV`, `CLVF`, `CGV`, `CGVF`, `SCV`, `SCVF`, `SCLVF`, `CVFF`, `CLVFF`, `SCLVFF`, where C is a consonant, V a vowel, F a final consonant (two of them a falling cluster, like nt or st, only at the end of a root), L a liquid or glide after the first consonant, G only a glide there, j or w, and S a sibilant before it |
| `coda-restriction` | `Any`, `Sonorants`, `Nasals`, `SingleNasal` |
| `stress` | `Initial`, `Second`, `Third`, `Antepenultimate`, `Penultimate`, `Ultimate`, `UltimateIfHeavy`, `PenultimateIfHeavy`, `InitialIfHeavy`, `LastHeavy`, where the rule puts stress, which free stress keeps for roots that store none |
| `free-stress` | `true` if each root may store which of its vowels is stressed, as in Russian |
| `cluster-repair` | `Epenthesis`, `Deletion` |
| `syllable-contact` | `Any`, `NoSteepRise`, `NoRise`, what may meet across a syllable boundary |
| `nasal-assimilation`, `degemination`, `intervocalic-voicing`, `lenition`, `voicing-assimilation`, `final-devoicing`, `glide-formation`, `diphthong-formation` | `true` if the language has the sound change |
| `vowel-harmony` | `None`, `FrontBack`, `TongueRoot` |
| `hiatus-resolution` | `None`, `VowelElision`, `VowelCoalescence`, `GreekContraction`, `GlideInsertion`, `GlottalInsertion`, for vowels that meet where morphemes do |
| `coda-devoicing`, `coda-weakening`, `compensatory-lengthening`, `closed-syllable-laxing`, `open-syllable-lengthening`, `unstressed-shortening`, `unstressed-monophthongization`, `syncope`, `pretonic-syncope`, `apocope`, `aphaeresis`, `verners-law`, `accent-retraction` | `true` if the language has the change, which applies once words have syllables |
| `unstressed-vowels` | `None`, `Reduction`, `Raising`, what happens to vowels without the stress |
| `contact-repair` | `None`, `CodaAssimilation`, `OnsetHardening`, `CodaNasalization` |
| `nasal-assimilation-across-words`, `intervocalic-voicing-across-words`, `lenition-across-words`, `voicing-assimilation-across-words` | `true` if the change also applies where words meet, which needs it within words too |
| `final-consonant-loss` | `true` if a word's last obstruent after a vowel goes silent, except before a vowel, as in French liaison |
| `clitic-stress` | `Group`, `Host`, whether a clitic and its host are stressed by the rule as one word or where the host alone is |
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
| `noun-number` | `None`, `SingularPlural`, `SingularDualPlural`, the numbers nouns inflect for |
| `tense` | `None`, `PresentPast`, `PresentPastFuture`, the tenses verbs inflect for |
| `aspect` | `true` if verbs inflect for perfective and imperfective |
| `verb-agreement` | `true` if verbs agree with their subject in person and number |
| `alignment` | `NominativeAccusative`, `ErgativeAbsolutive`, `None` |
| `roles-companion-instrument`, `roles-recipient-goal`, `roles-goal-location`, `roles-source-possessor`, `roles-possessor-recipient`, `roles-instrument-location`, `roles-location-source`, `roles-instrument-source`, `roles-agent-instrument`, `roles-agent-source`, `roles-agent-location`, `roles-time-location`, `roles-route-location`, `roles-route-instrument`, `roles-route-source` | `true` if the edge of the role map joins its two roles, so they share a marker |
| `genitive-case`, `dative-case`, `locative-case`, `ablative-case`, `instrumental-case`, `comitative-case`, `allative-case`, `perlative-case`, `vocative-case` | `true` if the language has the case. Core cases follow the alignment |
| `configurations-at-in`, `configurations-at-on`, `configurations-at-beside`, `configurations-in-on`, `configurations-in-between`, `configurations-on-over`, `configurations-on-against` | `true` if the edge of the configuration map joins its two configurations, so they share a marker, or for an edge to at, are said as plainly as 'at' |
| `in-marker`, `on-marker`, `under-marker`, `over-marker`, `behind-marker`, `in-front-marker`, `beside-marker`, `between-marker`, `around-marker`, `against-marker` | `Adposition`, `RelationalNoun` or `Case`, how the configuration's region is marked, or `At` if it's said like at. Under, behind, in front and around are never said like at |
| `configuration-marking` | `Adposition`, `RelationalNoun`, `Case`, the way of marking configurations most of the language's regions follow |
| `imperatives` | `SingularAndPlural`, `SingularOnly`, `PluralOnly`, `OneForBoth` or `None`, which numbers have an imperative of their own |
| `prohibitive-uses-imperative` | `true` if 'don't eat' uses the imperative's form of the verb |
| `prohibitive-negator` | `true` if a prohibitive has a negator of its own, as Latin has nē beside nōn |
| `negation` | `ParticleBefore`, `ParticleAfter`, `Prefix`, `Suffix`, `Double`, for a word on each side of the verb |
| `polar-questions` | `InitialParticle`, `FinalParticle`, `SecondParticle`, `Prefix`, `Suffix`, `Inversion`, `Intonation` |
| `fronts-question-words` | `true` if a question word goes first in its clause, as English who does |
| `passive` | `true` if the language has a passive |
| `modalities-ability-permission`, `modalities-permission-obligation`, `modalities-obligation-advisability` | `true` if the edge of the modality map joins its two modalities, so they share a marker, as English can is both 'able to' and 'allowed to' |
| `ability-modal`, `permission-modal`, `obligation-modal`, `advisability-modal` | `Affix`, `Verb` or `Particle`, how 'can', 'may', 'must' and 'should' are marked |
| `modalities-permission-dubitative`, `modalities-obligation-presumptive` | `true` if an epistemic modal shares the marker of the situational one it grew from, as English may and must are both |
| `dubitative-modal`, `presumptive-modal` | `Affix`, `Verb`, said impersonally with the clause as its complement, or `Particle`, how an epistemic 'might' and 'must' are marked |
| `perfect` | `None`, `Affix`, `Particle`, or an auxiliary verb: `Have`, `Be` or `Finish`, how a perfect like 'has gone' is marked |
| `progressive` | `None`, `Affix`, `Particle`, or an auxiliary verb: `Be`, the copula that says where, or `Sit`, how a progressive like 'is waiting' is marked |
| `habitual` | `None`, `Affix`, `Particle`, or the auxiliary `Be`, how a habitual like 'used to go' is marked |
| `inceptive` | `None`, where it's said with a verb for 'begin', `Affix` or `Particle`, how beginning to do something is marked on the verb |
| `cessative` | `None`, where it's said with a verb for 'stop', `Affix` or `Particle`, how ceasing to do something is marked on the verb |
| `possession` | `Dependent`, marking the possessor, `Head`, marking the possessed noun, `Double`, marking both, or `None` |
| `possessive-affixes` | `Prefix` or `Suffix`, the side of the possessed noun its affix goes on, where possession is marked there |
| `genders` | `None`, `MasculineFeminine`, `MasculineFeminineNeuter`, `AnimateInanimate` |
| `gender-assignment` | `Semantic`, by what a noun means, or `SemanticAndFormal`, by how it ends too |
| `first-person-plural` | `Plain`, one 'we', `SameAsSingular`, with the word for 'I', or `InclusiveExclusive`, one 'we' with the hearer and one without |
| `gendered-pronouns` | `None`, `ThirdSingular`, for he and she, `Third`, for the plural too, or `ThirdAndSecond`, for 'you' too |
| `drops-subjects` | `true` if a subject pronoun is usually left out |
| `predicative-adjectives` | `Verbal`, if an adjective said of something acts like a verb, or `Nonverbal`, if it needs a copula |
| `zero-copula` | `true` if the copula is left out in the present, as in Russian |
| `locational-copula` | `true` if the language has a verb of its own for where something is, like Spanish estar |
| `adjective-agreement` | `None`, `Agreement`, if adjectives take their noun's markers, or `Phrasal`, if the phrase is marked once at its edge |
| `numeral-base` | `Decimal`, `Vigesimal`, or `Restricted`, with words for only the first few numbers |
| `numerals-before-noun` | `true` if a numeral goes before its noun |
| `ordinals` | `Cardinal`, `FirstThenCardinal`, `Derived`, `FirstThenDerived`, `FirstSecondThenDerived`, how 'first', 'second' and the rest are said |
| `distance-contrast` | `None`, `TwoWay` or `ThreeWay`, how many distances demonstratives tell apart, as WALS 41A has it |
| `demonstrative-position` | `Before` or `After` the noun, or `Prefix` or `Suffix` on it, as WALS 88A has it |
| `demonstrative-gender-agreement` | `true` if a demonstrative agrees with its noun's gender |
| `demonstrative-pronouns-own-stem` | `true` if a demonstrative standing alone has a stem of its own, rather than being the word beside a noun, as WALS 42A has it |
| `here-from-demonstratives` | `true` if 'here' and 'there' are the demonstratives standing alone, marked as a place is, rather than words of their own |
| `definite-article` | `None`, `Word`, `Demonstrative`, the far demonstrative used as an article, or `Affix` |
| `indefinite-article` | `None`, `Word`, `One`, the word for 'one', or `Affix` |
| `article-gender-agreement` | `true` if an article agrees with its noun's gender |
| `article-with-demonstrative` | `true` if the definite article stays beside a demonstrative, as Greek *aftó to spíti* 'this the house' keeps it |
| `indefinite-pronouns` | `InterrogativeBased`, `GenericNounBased`, `Special` or `Mixed`, what 'someone' and the rest are made from, as WALS 46A has it. `Mixed` makes 'someone' from a noun and 'something' from a question word, where it wasn't drawn the other way around |
| `negative-indefinite-negation` | `Always`, `Never` or `AfterVerb`, whether 'nobody' takes the verb's negation too, as WALS 115A has it |
| `quantifiers-before-noun` | `true` if a quantifier like 'all' goes before its noun |
| `and-is-with` | `true` if 'and' between nouns is the word for 'with', as in 'the dog with the cat slept' |
| `clause-and` | `SameAsNouns`, `OwnWord`, or `Juxtaposition`, setting clauses side by side, as nouns are then too unless 'and' is 'with' |
| `subordinators-when-if`, `subordinators-when-after`, `subordinators-after-because`, `subordinators-purpose-because`, `subordinators-before-until`, `subordinators-when-because` | `true` if the edge of the subordinator map joins its two subordinators, so they share a marker, as German wenn is both 'if' and 'when' |
| `if-subordinator`, `when-subordinator`, `before-subordinator`, `after-subordinator`, `until-subordinator`, `because-subordinator`, `purpose-subordinator` | `Initial` or `Final`, a word at the start or end of the clause, or `Suffix`, on its verb |
| `if-clause-first`, `when-clause-first`, `before-clause-first`, `after-clause-first`, `until-clause-first`, `because-clause-first`, `purpose-clause-first` | `true` if the clause comes before the main one |
| `purpose-balance`, `reason-balance`, `temporal-balance` | `Balanced`, if the verb keeps its ordinary form, `Deranked`, if it loses its tense and agreement, or `Either`, deranked where the clauses share a subject. `temporal-balance` is for 'when' clauses, and the other temporal and conditional ones follow it |
| `proposition-complementizer`, `event-complementizer` | `None`, `Initial` or `Final`, a word at the start or end of the clause, or `Suffix`, on its verb, for a clause saying what's said, known or seen, and one saying what's wanted, tried or begun |
| `proposition-balance`, `event-balance` | `Balanced`, `Deranked` or `Either`, as for subordinate clauses. `event-balance` is WALS 124A's question of 'want': `Deranked` leaves the subject of what's wanted implicit, and `Balanced` expresses it |
| `quotative` | `None`, `Complementizer`, marking a quotation with a proposition's complementizer, or `Word`, with a word of its own |
| `complements-last` | `true` if a complement that keeps its verb, and a quotation, go last in the clause, rather than where the object goes |
| `desiderative` | `Verb`, `Affix` or `Particle`, how 'want' is said, as WALS 124A has it |
| `relative-clauses-before-noun` | `true` if a relative clause goes before its noun |
| `subject-relativization`, `object-relativization`, `oblique-relativization` | `Gap`, `Pronoun` or `Resumptive`, how a relative clause says its noun in that role: not at all, with a relative pronoun, or with an ordinary pronoun |
| `relativizer` | `None`, `Particle`, a word beside the noun, or `Affix`, making the verb a participle |
| `comparison` | `Locational`, marking the standard as a place, `Exceed`, with a verb meaning 'exceed', `Conjoined`, with two clauses side by side, or `Particle`, with a word like than |
| `comparative`, `similative` | `Affix` or `Word`, how 'more' and 'like' are marked |
