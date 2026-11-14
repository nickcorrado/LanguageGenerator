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

Configuration is partly for languages the odds would never give, so a value no draw reaches is set directly instead. Only real contradictions are refused, before anything is generated, with the reason:

- A coda restriction needs a syllable template with a coda.
- Obliques can only go between the object and the verb where the object comes first.
- An inventory needs at least one sound, and each sound once.
- The dropoff is between 0 and 1.

A setting that does nothing in the language is accepted, like affixation in an isolating language, whose markers are all words of their own.

A spec and a seed give the same language only on the same version of the generator, though an older spec still fits a newer generator, whose new settings are just free.

## Inventories

`vowels` and `consonants` are arrays of IPA symbols, commonest first: the order is how often each sound turns up, the first the most, as in Mark Rosenfelder's generators. `dropoff` says how steeply: each sound is (1 − dropoff) times as likely as the one before it, so the usual 0.15 is gentle, and 0.5 makes each sound half as likely as the last.

A fixed inventory needn't look like a real one: z without s, which a drawn inventory never has, is fine. With six consonants or fewer, or two vowels or fewer, the command line warns that many more words will sound alike than usual.

The symbols are the generator's 53 phonemes, given exactly, so `ʦ` and not `ts`, and `aː` with the length mark and not a colon. Long vowels are sounds of their own:

- Vowels: a e i æ ɛ ə ɨ ɔ o u iː eː aː oː uː
- Consonants: the error for a symbol the generator doesn't have lists them all.

## Settings

| Setting | Values |
| --- | --- |
| `vowels`, `consonants` | An array of IPA symbols, commonest first |
| `dropoff` | A number between 0 and 1; 0.15 if free |
| `syllable-template` | `CV`, `CVF`, `CLV`, `CLVF`, `SCV`, `SCVF`, `SCLVF`, where C is a consonant, V a vowel, F a final consonant, L a liquid or glide after the first consonant and S a sibilant before it |
| `coda-restriction` | `Any`, `Sonorants`, `Nasals`, `SingleNasal` |
| `stress` | `Initial`, `Second`, `Third`, `Antepenultimate`, `Penultimate`, `Ultimate`, `UltimateIfHeavy`, `PenultimateIfHeavy`, `InitialIfHeavy`, `LastHeavy` |
| `cluster-repair` | `Epenthesis`, `Deletion` |
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
