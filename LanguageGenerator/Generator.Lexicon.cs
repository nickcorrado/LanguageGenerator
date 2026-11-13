namespace LanguageGenerator;

//roots, and how long they run
public static partial class Generator
{
    //basic meanings to fill the lexicon with, glossed the way the Leipzig rules gloss roots
    private static readonly string[] NounMeanings = ["person", "dog", "cat", "fish", "bird", "tree", "stone", "water", "fire", "sun", "house"];
    private static readonly string[] VerbMeanings = ["see", "eat", "drink", "sleep", "walk", "give", "know", "hear"];
    private static readonly string[] AdjectiveMeanings = ["big", "small", "good", "old"];
    private static readonly string[] AdverbMeanings = ["now", "again"];

    //a lexicon that coins each root from its own Random, seeded by the language and the meaning,
    //so that a root never depends on which other words were coined, or in what order. A root is
    //redrawn from that same Random, a few times at most, if it sounds like a closed-class word that
    //fills the same slot, as a pronoun does a noun's, a numeral an adjective's and a copula a verb's:
    //'the person saw' and 'you saw' shouldn't sound alike. Homophones in different slots, like
    //English there and their, are left alone, as languages leave them. The basic meanings are
    //coined up front so that there are words to show
    public static Lexicon GenerateLexicon(int seed, double extraSyllables, Phonology phonology, double dropoff, IReadOnlyDictionary<PartOfSpeech, IEnumerable<Lexeme>> closedClasses)
    {
        string Surface(Root root) => string.Concat(phonology.Realize(new Word([root])).Phonemes.Select(p => p.IpaSymbol));
        var taken = closedClasses.ToDictionary(c => c.Key, c => c.Value.Select(l => Surface(l.Root)).ToHashSet());
        var lexicon = new Lexicon((meaning, partOfSpeech) =>
        {
            var rand = Sampling.For(seed, $"{partOfSpeech}:{meaning}");
            var root = new Root(GenerateForm(rand, phonology, dropoff, extraSyllables), meaning);
            for (int attempt = 0; attempt < 10 && taken.TryGetValue(partOfSpeech, out var forms) && forms.Contains(Surface(root)); attempt++)
            {
                root = new Root(GenerateForm(rand, phonology, dropoff, extraSyllables), meaning);
            }
            return root;
        });
        foreach (var meaning in NounMeanings)
        {
            lexicon.Noun(meaning);
        }
        foreach (var meaning in VerbMeanings)
        {
            lexicon.Verb(meaning);
        }
        foreach (var meaning in AdjectiveMeanings)
        {
            lexicon.Adjective(meaning);
        }
        foreach (var meaning in AdverbMeanings)
        {
            lexicon.Adverb(meaning);
        }
        return lexicon;
    }

    public const int MaxRootSyllables = 5;

    //how much languages differ in root length around what they need
    public static double SelectRootLengthNoise(Random rand) => 0.75 + 0.5 * rand.NextDouble();

    public const double RootCollisionRate = 3.4e-4;
    private const int RootsToSample = 400;
    private const double MostExtraSyllables = 2.5;

    //how many syllables past the first a root has on average: the fewest at which two roots sound
    //alike about RootCollisionRate of the time, which is about 10 homophones among 250 words. Languages
    //with little to work with need longer words, so Hawaiian's roots run longer than English's. It
    //samples real surface forms because counting what's possible says little: common phonemes make
    //for far fewer likely syllables than possible ones, and sound changes merge some of what's left.
    //The target trades homophones for length, and WALS has nothing on word length to settle it:
    //halving it halves the homophones and makes the average root a quarter of a syllable longer
    public static double SelectExtraSyllables(double noise, int seed, Phonology phonology, double dropoff)
    {
        double CollisionRate(double extraSyllables)
        {
            var rand = Sampling.For(seed, "root length");
            var forms = Enumerable.Range(0, RootsToSample)
                .Select(_ => string.Concat(phonology.Realize(new Word([new Root(GenerateForm(rand, phonology, dropoff, extraSyllables), "")])).Phonemes.Select(p => p.IpaSymbol)));
            var pairs = forms.GroupBy(form => form).Sum(same => same.Count() * (same.Count() - 1L) / 2);
            return pairs / (RootsToSample * (RootsToSample - 1) / 2.0);
        }

        var (low, high) = (0.0, MostExtraSyllables);
        for (int step = 0; step < 5; step++)
        {
            var middle = (low + high) / 2;
            (low, high) = CollisionRate(middle) > RootCollisionRate ? (middle, high) : (low, middle);
        }
        return Math.Clamp(high * noise, 0.2, MostExtraSyllables);
    }

    //how many times a syllable is drawn again for a contact the language doesn't allow. One with a
    //stop for its onset always fits, so this only runs out in an inventory with no stops left
    private const int ContactDraws = 20;

    public static List<Phoneme> GenerateForm(Random rand, Phonology phonology, double dropoff, double extraSyllables, int maxSyllables = MaxRootSyllables)
    {
        var form = new List<Phoneme>();
        var numberOfSyllables = Sampling.DisplacedPoisson(rand, extraSyllables, maxSyllables);

        //only the first syllable can go without an onset, as in aloha: vowels meeting inside a
        //root are rare in real languages, which mostly get hiatus where morphemes meet. A syllable
        //after a coda is drawn again until the two meet as the language allows, so roots keep to
        //its syllable contact
        Consonant? coda = null;
        for (int i = 0; i < numberOfSyllables; i++)
        {
            var syllable = GenerateSyllable(rand, phonology.SyllableRule, dropoff, needsOnset: i > 0);
            for (int draw = 1; draw < ContactDraws && coda is not null && syllable.Onset.Count > 0
                && !phonology.SyllableContact.Allows(coda, syllable.Onset[0]); draw++)
            {
                syllable = GenerateSyllable(rand, phonology.SyllableRule, dropoff, needsOnset: true);
            }
            form.AddRange(syllable.Phonemes);
            coda = syllable.Coda;
        }
        return form;
    }
}
