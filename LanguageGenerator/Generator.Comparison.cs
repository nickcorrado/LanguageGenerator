namespace LanguageGenerator;

//comparison
public static partial class Generator
{
    private enum ComparisonType { Locational, Conjoined, Particle }

    //WALS 121A, of 167 languages: 78 locational, 34 conjoined and 22 with a particle, leaving out its
    //33 with a verb meaning 'exceed'
    private static readonly (ComparisonType Value, int Languages)[] ComparisonCounts =
        [(ComparisonType.Locational, 78), (ComparisonType.Conjoined, 34), (ComparisonType.Particle, 22)];

    //which place a locational standard is marked as. Stassen finds 'from', the separative, the
    //commonest by far, then 'to' and 'at', but WALS doesn't split them, so the odds are guesses
    private static readonly (Role Value, int Weight)[] LocationalRoles = [(Role.Source, 60), (Role.Goal, 20), (Role.Location, 20)];

    //a particle is a one-syllable word, like the language's adpositions, and goes on their side of
    //the standard, as English than does
    public static Comparison GenerateComparison(Random rand, WordOrder wordOrder, Phonology phonology, double dropoff) =>
        Sampling.Categorical(rand, ComparisonCounts) switch
        {
            ComparisonType.Locational => new LocationalComparison(Sampling.Categorical(rand, LocationalRoles)),
            ComparisonType.Particle => new ParticleComparison(
                new Lexeme(new Root(GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1), "than"), PartOfSpeech.Adposition),
                wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial),
            _ => new ConjoinedComparison(),
        };
}
