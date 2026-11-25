namespace LanguageGenerator;

//negation
public static partial class Generator
{
    //WALS 143A, leaving out its 87 languages where a second negative is optional and its 64 that use
    //two of these types in different constructions. A negative auxiliary verb counts as a word here
    private static readonly (NegationStrategy Value, int Languages)[] NegationCounts =
    [
        (NegationStrategy.ParticleBefore, 525),
        (NegationStrategy.ParticleAfter, 171),
        (NegationStrategy.Prefix, 162),
        (NegationStrategy.Suffix, 202),
        (NegationStrategy.Double, 114),
    ];

    /*
      an isolating language has no affixes to negate with, so it puts a word where an affix would go.
      Double negation is two words, as French ne and pas are written; the first is often a clitic in
      speech, but that's a distinction the generator doesn't make. A language's prohibitive negator is
      drawn the same way, with what it means in place of plain negation
     */
    public static Negation GenerateNegation(Random rand, MorphologicalProfile morphology, Phonology phonology, Grammeme? meaning = null)
    {
        var grammemes = (IReadOnlyList<Grammeme>)[meaning ?? Polarity.Negative];
        var strategy = Sampling.Categorical(rand, NegationCounts);
        if (morphology.Fusion == Fusion.Isolating)
        {
            strategy = strategy switch
            {
                NegationStrategy.Prefix => NegationStrategy.ParticleBefore,
                NegationStrategy.Suffix => NegationStrategy.ParticleAfter,
                _ => strategy,
            };
        }
        var positions = NegationPositions(strategy);

        Morpheme Marker(ExponentPosition position) => position is ExponentPosition.Prefix or ExponentPosition.Suffix
            ? CoinAffix(rand, phonology, MarkerForm(rand, true, phonology), grammemes)
            : new Particle(MarkerForm(rand, false, phonology), grammemes);
        return new Negation(strategy, [.. positions.Select(p => (p, Marker(p)))]);
    }

    /// <summary>Where each of a negation strategy's markers goes.</summary>
    public static IReadOnlyList<ExponentPosition> NegationPositions(NegationStrategy strategy) => strategy switch
    {
        NegationStrategy.ParticleBefore => [ExponentPosition.PrecedingParticle],
        NegationStrategy.ParticleAfter => [ExponentPosition.FollowingParticle],
        NegationStrategy.Prefix => [ExponentPosition.Prefix],
        NegationStrategy.Suffix => [ExponentPosition.Suffix],
        NegationStrategy.Double => [ExponentPosition.PrecedingParticle, ExponentPosition.FollowingParticle],
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
    };

    //a frequent, short morpheme: a syllable, or for an affix sometimes just a consonant, the way the
    //generator makes other affixes
    private static List<Phoneme> MarkerForm(Random rand, bool affix, Phonology phonology) => affix && rand.NextDouble() < 0.3
        ? [phonology.Consonants[Sampling.TruncatedGeometric(rand, phonology.Consonants.Count, phonology.Dropoff.Consonants)]]
        : [.. GenerateSyllable(rand, phonology.SyllableRule, phonology.Dropoff, needsOnset: false, codaCluster: false).Phonemes];
}
