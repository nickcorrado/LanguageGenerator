namespace LanguageGenerator;

//comparison
public static partial class Generator
{
    private enum ComparisonType { Locational, Exceed, Conjoined, Particle }

    // https://wals.info/feature/121A, of 167 languages: 78 locational, 33 with a verb meaning
    //'exceed', 34 conjoined and 22 with a particle
    private static readonly (ComparisonType Value, int Languages)[] ComparisonCounts =
        [(ComparisonType.Locational, 78), (ComparisonType.Exceed, 33), (ComparisonType.Conjoined, 34), (ComparisonType.Particle, 22)];

    //which place a locational standard is marked as. Stassen finds 'from', the separative, the
    //commonest by far, then 'to' and 'at', but WALS doesn't split them, so the odds are guesses
    private static readonly (Role Value, int Weight)[] LocationalRoles = [(Role.Source, 60), (Role.Goal, 20), (Role.Location, 20)];

    //a particle is a one-syllable word, like the language's adpositions, and goes on their side of
    //the standard, as English than does. The verb 'exceed' is short, as common verbs are
    public static Comparison GenerateComparison(Random rand, WordOrder wordOrder, Phonology phonology, double dropoff) =>
        Sampling.Categorical(rand, ComparisonCounts) switch
        {
            ComparisonType.Locational => new LocationalComparison(Sampling.Categorical(rand, LocationalRoles)),
            ComparisonType.Particle => new ParticleComparison(
                new Lexeme(new Root(GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1), "than"), PartOfSpeech.Adposition),
                wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial),
            ComparisonType.Exceed => new ExceedComparison(
                new Lexeme(new Root(GenerateForm(rand, phonology, dropoff, 0.5, maxSyllables: 2), "exceed"), PartOfSpeech.Verb)),
            _ => new ConjoinedComparison(),
        };

    //a degree word goes before the adjective in a language whose adjective phrases are head-final,
    //and after it in one whose are head-initial, which word order draws from WALS 83A and 91A
    //together, so that it leans the way the language's verb and object go
    private static bool DegreeWordBefore(WordOrder wordOrder) => wordOrder.AdjectivePhraseDirectionality == HeadDirectionality.Final;

    //WALS doesn't count how a comparative adjective is marked, so these are guesses: most languages
    //say 'more' with a word of its own, as Turkish daha and Mandarin geng, and fewer with an affix,
    //as English -er and Finnish -mpi do. An isolating language uses a word in place of an affix
    private const double DegreeAffixChance = 0.25;

    //Stassen finds a comparative adjective alongside a standard mostly among particle comparatives,
    //as in English and the rest of Europe; elsewhere the adjective is usually plain, as in Turkish
    //evden büyük 'big from the house'. The odds are guesses. A conjoined comparison says 'big' and
    //'not big', so it never says 'bigger'
    private static double DegreeWithStandardChance(Comparison comparison) => comparison switch
    {
        ParticleComparison => 0.6,
        ConjoinedComparison => 0,
        _ => 0.15,
    };

    public static DegreeMarking GenerateDegree(Random rand, Comparison comparison, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var affix = morphology.Fusion != Fusion.Isolating && rand.NextDouble() < DegreeAffixChance;
        DegreeMarker marker = affix
            ? new DegreeAffix(new Affix(MarkerForm(rand, true, phonology, dropoff), [Degree.Comparative]),
                rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix)
            : new DegreeWord(new Particle(MarkerForm(rand, false, phonology, dropoff), [Degree.Comparative]), DegreeWordBefore(wordOrder));
        return new DegreeMarking(marker, rand.NextDouble() < DegreeWithStandardChance(comparison));
    }

    //WALS doesn't count similatives either. Haspelmath finds 'like' most often a word, an adposition
    //or a particle, as English like, Turkish gibi and Russian kak are, and less often an affix, as
    //Quechua -hina is, so the odds are guesses. A word goes on the side the language's adpositions
    //go, and isn't the comparative's than, though some languages do use one word for both
    private const double SimilativeAffixChance = 0.3;

    public static Similative GenerateSimilative(Random rand, Comparison comparison, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        if (morphology.Fusion != Fusion.Isolating && rand.NextDouble() < SimilativeAffixChance)
        {
            return new SimilativeAffix(new Affix(MarkerForm(rand, true, phonology, dropoff), [Case.Similative]),
                rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix);
        }
        var forms = new DistinctForms();
        if (comparison.Value is ParticleComparison particle)
        {
            forms.Take(particle.Than.Root.Form);
        }
        return new SimilativeWord(new Lexeme(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1)), "like"), PartOfSpeech.Adposition),
            wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial);
    }
}
