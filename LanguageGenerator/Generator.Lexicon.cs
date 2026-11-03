namespace LanguageGenerator;

//roots, and how long they run
public static partial class Generator
{
    //basic meanings to fill the lexicon with, glossed the way the Leipzig rules gloss roots
    private static readonly string[] NounMeanings = ["person", "dog", "cat", "fish", "bird", "tree", "stone", "water", "fire", "sun", "house"];
    private static readonly string[] VerbMeanings = ["see", "eat", "drink", "sleep", "walk", "give", "know", "hear"];
    private static readonly string[] AdjectiveMeanings = ["big", "small", "good", "old"];

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
        var rule = phonology.SyllableRule;
        var lexicon = new Lexicon((meaning, partOfSpeech) =>
        {
            var rand = Sampling.For(seed, $"{partOfSpeech}:{meaning}");
            var root = new Root(GenerateForm(rand, rule, dropoff, extraSyllables), meaning);
            for (int attempt = 0; attempt < 10 && taken.TryGetValue(partOfSpeech, out var forms) && forms.Contains(Surface(root)); attempt++)
            {
                root = new Root(GenerateForm(rand, rule, dropoff, extraSyllables), meaning);
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
        return lexicon;
    }

    public const int MaxRootSyllables = 5;

    //how many syllables past the first a root has on average. Languages with few possible syllables
    //need longer words to tell them apart, so Hawaiian's roots run longer than English's: about as
    //many syllables as it takes to make RootsToDistinguish distinct forms. Tuned by eye; WALS has
    //nothing on word length
    public static double SelectExtraSyllables(Random rand, SyllableRule rule)
    {
        const double RootsToDistinguish = 100_000;
        var needed = Math.Log(RootsToDistinguish) / Math.Log(Math.Max(rule.DistinctSyllables, 2));
        var noise = 0.75 + 0.5 * rand.NextDouble();
        return Math.Clamp((needed - 1) * noise, 0.2, 2.0);
    }

    public static List<Phoneme> GenerateForm(Random rand, SyllableRule rule, double dropoff, double extraSyllables, int maxSyllables = MaxRootSyllables)
    {
        var form = new List<Phoneme>();
        var numberOfSyllables = Sampling.DisplacedPoisson(rand, extraSyllables, maxSyllables);

        for (int i = 0; i < numberOfSyllables; i++)
        {
            form.AddRange(GenerateSyllable(rand, rule, dropoff).Phonemes);
        }
        return form;
    }
}
