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

    //an isolating language has no affixes to negate with, so it puts a word where an affix would go.
    //Double negation is two words, as French ne and pas are written; the first is often a clitic in
    //speech, but that's a distinction the generator doesn't make
    public static Negation GenerateNegation(Random rand, MorphologicalProfile morphology, Phonology phonology, double dropoff)
    {
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
        ExponentPosition[] positions = strategy switch
        {
            NegationStrategy.ParticleBefore => [ExponentPosition.PrecedingParticle],
            NegationStrategy.ParticleAfter => [ExponentPosition.FollowingParticle],
            NegationStrategy.Prefix => [ExponentPosition.Prefix],
            NegationStrategy.Suffix => [ExponentPosition.Suffix],
            NegationStrategy.Double => [ExponentPosition.PrecedingParticle, ExponentPosition.FollowingParticle],
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
        };

        //a negative is a frequent, short morpheme: a syllable, or for an affix sometimes just a
        //consonant, the way the generator makes other affixes
        Morpheme Marker(ExponentPosition position)
        {
            if (position is ExponentPosition.Prefix or ExponentPosition.Suffix)
            {
                List<Phoneme> form = rand.NextDouble() < 0.3
                    ? [phonology.Consonants[Sampling.TruncatedGeometric(rand, phonology.Consonants.Count, dropoff)]]
                    : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes];
                return new Affix(form, [Polarity.Negative]);
            }
            return new Particle([.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes], [Polarity.Negative]);
        }
        return new Negation(strategy, [.. positions.Select(p => (p, Marker(p)))]);
    }
}
