# Phonology, spelling and the lexicon

How a language's sounds are chosen, how they change from underlying to surface forms, how they're spelled, and how its roots are built from them. Each part depends on the ones before it. The [grammar](grammar.md) has the rest of what `Generator.Generate` chooses.

## Inventories

Inventories are built rather than pruned. The commonest sounds come first in a ranking that decides how often each turns up in words.

### Vowels

A vowel system takes its size from WALS's counts and one of the spread-out shapes real systems have (*a i u*, *a e i o u*, the seven vowels of Italian, ...), with length for all of its vowels, all but the mid ones as in Classical Arabic, or none.

About three languages in ten have diphthongs too (*ai*, *au*, *ei*, *ou*, *oi*, *ui*), each only beside both of its vowels. A diphthong is one sound and one syllable's nucleus, and in a language with them a glide closing a syllable merges with the vowel before it into the diphthong they make (*kaj* → *kai*), since a vowel gliding toward *i* or *u* is a diphthong.

### Consonants

Consonants come in series, so a language that voices its stops has *b d g* together, as does one with aspirates (*pʰ tʰ kʰ*). A marked sound comes only with the sounds it implies (*z* with *s*, *ŋ* with *k*, *gʷ* with *kʷ*, *ʎ* with *l*), though the softened *β* and *ɣ* of Spanish *lobo* and *lago* come on their own, at odds near its share of [PHOIBLE](https://phoible.org/)'s inventories. Most languages have one rhotic, a trill or tap far more often than English *ɹ*.

## Syllables

A syllable template (CV, CVF, SCLVF, ...) is as simple or complex as WALS's counts have them, with an optional restriction on codas, and a strategy for repairing illegal clusters: epenthesis or deletion.

### Onsets

Onset clusters rise in sonority, so *pl* and *kw* but not *nr*, and some languages allow only a glide after the first consonant, as Korean, Mandarin and Swahili do (*kwa*, *pja*, but no *pla*). A root's later syllables always have an onset, so vowels meet only where morphemes do.

### Codas

In some complex languages two consonants may close a root's last syllable, as in English *hand*, *felt* or *best*, falling in sonority and limited to the easy clusters: a nasal before a stop shares its place (*mp*, *nt*, *ŋk*), and a fricative before one is voiceless (*st*, *ft*).

### Syllable contact

Where a coda meets the next onset, many languages limit how far sonority may rise across the boundary, per Vennemann's Syllable Contact Law: some allow a small rise (*s.n* but not *t.l*), others none (*l.t* but not *n.l*), and their roots keep to it. Suffixes can still make contacts the law forbids, which some languages [repair](#syllable-contact-repairs).

## Stress

Each word with a root has one stressed syllable, counted over the whole word so that it moves as suffixes are added. In WALS's proportions, it falls at a fixed place, like the first syllable or the second-to-last, or is drawn to heavy syllables, those with a long vowel, a diphthong or a coda, as in Latin.

In about one language in twenty stress is free, as in Russian:

- each root may store which of its vowels is stressed and keep it however it's inflected;
- an affix may store one of its own, or stress the syllable before it, as Turkish negative *-mA* does;
- the first accent in the word wins, so a root that stores none takes its stress from its endings, and from the rule where they have none.

That makes stress move through a paradigm, as Russian *ruk-á* 'hand' has *rúk-u* in the accusative.

Words without a root, like articles and other particles, have no stress, so their vowels weaken where unstressed vowels do, as English *the* is [ðə].

## Sound changes

Each language has an ordered list of regular changes from underlying to surface forms, so that a morpheme sounds different in different words. Each change is a value: which sounds, in what surroundings, becoming what, or what goes in between.

### Assimilation and lenition

- Nasal and voicing assimilation, and degemination.
- Voicing and lenition between vowels: *b* and *g* weakening to *β* and *ɣ* as Spanish's do, aspirates to fricatives as Greek's did.
- Final devoicing.
- In a language with a labialized velar, a velar and *w* in one onset merging into it, as Latin *qu* was one sound, *kʷ*.

### Vowel harmony

Front-back, as in Finnish, or tongue-root, as in Akan.

### Vowels that meet

Every language merges the same vowel twice into a long one, and has one way with vowels that meet at a morpheme boundary:

- eliding the first;
- coalescing them, as Sanskrit does (*a + i* → *e*);
- contracting them, as Attic Greek does (*e + o* → *ō*);
- putting a glide or a glottal stop between them;
- or leaving them be.

Close vowels often become glides first (*i + a* → *ya*), and a language with diphthongs often makes one of a vowel and a close vowel after it (*a + i* → *ai*), as Greek did.

### Changes that follow syllables

These run once a word has its syllables: coda devoicing, the weakening of *s* to *h* in codas, compensatory lengthening, and the laxing of vowels in closed syllables.

### Changes that follow the stress

- Unstressed vowels weakening to schwa, as in English, or rising, as in Portuguese (*o* → *u*).
- Long vowels shortening away from the stress.
- Diphthongs becoming single vowels away from the stress, as Latin *ae* and *au* became Romance *e* and *o*.
- Stressed vowels lengthening in open syllables, as in Italian.
- Verner's law, which voices a fricative unless the stress falls just before it, as Germanic's did.
- Where stress is free, accent retraction, which moves a stored accent off a word's last syllable, as Neo-Štokavian Serbo-Croatian's did.

### Losing unstressed vowels

- Syncope just after the stress, as Latin *calidus* became Italian *caldo*, or just before it, as *verecundia* became Spanish *vergüenza*.
- Apocope at the end of a word, as *mare* became *mar*.
- Aphaeresis at the start, as English *esquire* became *squire*.

### Syllable contact repairs

A language that restricts [syllable contact](#syllable-contact) may repair the contacts its suffixes make:

- a coda doubling the onset after it (Korean *sin-la* → *silla*);
- a sonorant onset hardening into a stop (the Kazakh plural *-lar* is *-dar* after a nasal and *-tar* after a voiceless stop);
- or a stop becoming a nasal before one (Korean *kuk-mul* → *kuŋmul*).

### Phonemes and allophones

A change that makes another of the language's phonemes merges the two. One that makes a sound the language lacks makes an allophone, which is spelled as its phoneme and shows only in the IPA: intervocalic voicing gives a language without *b d g* its *p t k* said as [b d g] between vowels, as Korean does. Changes that merge or insert sounds, and harmony, make only phonemes. The lax vowels *ɪ* and *ʊ* and the bilabial *ɸ* are only ever allophones.

## Where words meet

`Phonology.RealizePhrase` turns a sentence's words into the sentence as it's said: each word with the clitics leaning on it, through sound changes, cluster repair and syllabification, and then what happens where words meet.

Some of a language's changes also apply between words, as Spanish lenition does in *la boca* [laˈβoka], changing how words are said but never how they're spelled. A language may also lose its final consonants except before a vowel, as French did, which keeps them in spelling and says them in liaison (*petit ami* [pətit‿ami]).

Particles are clitics, said as one word with the word they lean on, so they can lose a vowel to it or take its harmony. They take their stress either with it by the rule, as Latin *virúmque* does, or from it alone, as Spanish *dígamelo* does.

## Orthography

Each phoneme has a spelling, from one consistent style per language:

- háček or digraph postalveolars;
- macron or acute long vowels;
- *ai* or Tagalog's *ay* for diphthongs;
- *ph* or Wade–Giles *p'* for aspirates;
- *ñ* or *ny*, with *ll* or *ly* beside them for *ʎ*;
- and so on.

No two phonemes are spelled alike. A convention that would clash in a language, like Pinyin's *b* for a plain *p* in one that also has a voiced *b*, is drawn again. Spellings may run together, as in English "mishap".

A language with free stress marks it where its rule wouldn't put it, as Spanish writes *teléfono* but *casa*, with an acute or a grave that none of its vowels already uses.

## Lexicon

Roots are built from legal syllables. Root length follows a displaced Poisson whose mean is set by sampling surface forms until roots rarely sound alike, so languages with simple syllables, or sound changes that merge a lot, have longer words.

The lexicon is open: asking for a meaning it doesn't have coins a root, seeded from the language and the meaning alone, so a word comes out the same however much other text has been translated. A language calls itself by its word for 'people', the plural of 'person', as *Bantu* and *Inuit* do.

A root is redrawn if it would sound like a closed-class word that fills the same slot (a noun like a pronoun, an adjective like a numeral, a verb like a copula), since those would be confused. Homophones in different slots, like English *there* and *their*, are left alone, as languages leave them.
