namespace LanguageGenerator;

//constituent order, each phrase's head direction, and where obliques go
public static partial class Generator
{
    //I've rounded off the real percentages to more even numbers for convenience
    private static readonly (ConstituentWordOrder Order, double Probability)[] ConstituentWordOrders =
    [
        (ConstituentWordOrder.SOV, 0.45),
        (ConstituentWordOrder.SVO, 0.40),
        (ConstituentWordOrder.VSO, 0.10),
        (ConstituentWordOrder.VOS, 0.03),
        (ConstituentWordOrder.OSV, 0.01),
        (ConstituentWordOrder.OVS, 0.01),
    ];

    public static WordOrder SelectWordOrder(Random rand)
    {
        var roll = rand.NextDouble();
        var cumulative = 0.0;
        //rounding can leave the cumulative total a hair under 1; the remainder goes to the last order
        var order = ConstituentWordOrders[^1].Order;
        foreach (var (candidate, probability) in ConstituentWordOrders)
        {
            cumulative += probability;
            if (roll < cumulative)
            {
                order = candidate;
                break;
            }
        }

        //the verb phrase's direction follows from the order itself; the others are drawn by it.
        //initializers run in order, so the draws always happen in this order
        var objectFirst = WordOrder.ObjectPrecedesVerb(order);
        return new WordOrder
        {
            ConstituentWordOrder = order,
            AdpositionalPhraseDirectionality = Sampling.Categorical(rand, objectFirst ? ObjectVerbAdpositionCounts : VerbObjectAdpositionCounts),
            NounPhraseDirectionality = Sampling.Categorical(rand, objectFirst ? ObjectVerbNounPhraseCounts : VerbObjectNounPhraseCounts),
            AdjectivePhraseDirectionality = Sampling.Categorical(rand, objectFirst ? ObjectVerbAdjectivePhraseCounts : VerbObjectAdjectivePhraseCounts),
        };
    }

    //each phrase's head-initial and head-final counts, by the order of object and verb. Languages
    //with no dominant order of either kind are left out, since every generated language has one

    // https://wals.info/feature/95A: prepositions are head-initial and postpositions head-final.
    //adposition order follows the verb almost without exception
    private static readonly (HeadDirectionality Value, int Languages)[] ObjectVerbAdpositionCounts =
        [(HeadDirectionality.Initial, 14), (HeadDirectionality.Final, 472)];
    private static readonly (HeadDirectionality Value, int Languages)[] VerbObjectAdpositionCounts =
        [(HeadDirectionality.Initial, 456), (HeadDirectionality.Final, 42)];

    // https://wals.info/feature/97A: noun-adjective is head-initial. Adjective order barely
    //follows the verb; Dryer's chapter is known for showing that
    private static readonly (HeadDirectionality Value, int Languages)[] ObjectVerbNounPhraseCounts =
        [(HeadDirectionality.Initial, 332), (HeadDirectionality.Final, 216)];
    private static readonly (HeadDirectionality Value, int Languages)[] VerbObjectNounPhraseCounts =
        [(HeadDirectionality.Initial, 456), (HeadDirectionality.Final, 114)];

    // https://wals.info/combinations/83A_91A: an adjective before its degree word ("big very") is
    //head-initial
    private static readonly (HeadDirectionality Value, int Languages)[] ObjectVerbAdjectivePhraseCounts =
        [(HeadDirectionality.Initial, 72), (HeadDirectionality.Final, 128)];
    private static readonly (HeadDirectionality Value, int Languages)[] VerbObjectAdjectivePhraseCounts =
        [(HeadDirectionality.Initial, 107), (HeadDirectionality.Final, 88)];

    //WALS 84A, leaving out its 189 languages with no dominant order
    private static readonly (ObliquePosition Value, int Languages)[] ObjectVerbObliqueCounts =
    [
        (ObliquePosition.BeforeVerbAndObject, 48),
        (ObliquePosition.BetweenObjectAndVerb, 45),
        (ObliquePosition.AfterVerbAndObject, 27),
    ];
    private static readonly (ObliquePosition Value, int Languages)[] VerbObjectObliqueCounts =
        [(ObliquePosition.BeforeVerbAndObject, 3), (ObliquePosition.AfterVerbAndObject, 210)];

    //obliques nearly always follow the verb and its object where the object follows the verb, while
    //languages with the object first split three ways
    public static ObliquePosition SelectObliquePosition(Random rand, ConstituentWordOrder order) =>
        Sampling.Categorical(rand, WordOrder.ObjectPrecedesVerb(order) ? ObjectVerbObliqueCounts : VerbObjectObliqueCounts);
}
