namespace LanguageGenerator;

//discriminated unions can't come to C# fast enough
//what about the relationship between head-directionality and constituent word order??
//with head-initial, verb would come before objects. with head-final, verb comes after
public class WordOrder
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

    public static WordOrder Select(Random rand)
    {
        var roll = rand.NextDouble();
        var cumulative = 0.0;
        foreach (var (order, probability) in ConstituentWordOrders)
        {
            cumulative += probability;
            if (roll < cumulative)
            {
                return new WordOrder(rand, order, probability);
            }
        }
        //rounding can leave the cumulative total a hair under 1; give the remainder to the last order
        var (lastOrder, lastProbability) = ConstituentWordOrders[^1];
        return new WordOrder(rand, lastOrder, lastProbability);
    }

    public ConstituentWordOrder ConstituentWordOrder { get; private set; }
    public double Probability { get; private set; }
    //some languages are consistently head-initial, while others are consistently head-final
    //many are somewhere in the middle. The algorithm weights either one more likely based on
    //earlier choices
    public HeadDirectionality HeadDirectionality { get; private set; }
    //this one's related to constituent word order. TODO revise
    public HeadDirectionality VerbPhraseDirectionality { get; private set; }
    public HeadDirectionality NounPhraseDirectionality { get; private set; }
    public HeadDirectionality AdjectivePhraseDirectionality { get; private set; }
    public HeadDirectionality AdpositionalPhraseDirectionality { get; private set; }
    //we don't necessarily have this as a part of speech, so it's out for now
    //public HeadDirectionality DeterminerPhraseDirectionality { get; private set; }


    private WordOrder(Random rand, ConstituentWordOrder enumeratedWordOrder, double probability)
    {
        ConstituentWordOrder = enumeratedWordOrder;
        Probability = probability;

        //probability starts at 50%. every time things go < fraction,
        //the numerator gets relatively bigger
        var numerator = 1.0;
        var denominator = 2.0;

        if (rand.NextDouble() < numerator / denominator)
        {
            HeadDirectionality = HeadDirectionality.Initial;
            numerator++;
        }
        else
        {
            HeadDirectionality = HeadDirectionality.Final;
        }
        denominator++;

        if (rand.NextDouble() < numerator / denominator)
        {
            VerbPhraseDirectionality = HeadDirectionality.Initial;
            numerator++;
        }
        else
        {
            VerbPhraseDirectionality = HeadDirectionality.Final;
        }
        denominator++;

        if (rand.NextDouble() < numerator / denominator)
        {
            NounPhraseDirectionality = HeadDirectionality.Initial;
            numerator++;
        }
        else
        {
            NounPhraseDirectionality = HeadDirectionality.Final;
        }
        denominator++;

        if (rand.NextDouble() < numerator / denominator)
        {
            AdjectivePhraseDirectionality = HeadDirectionality.Initial;
            numerator++;
        }
        else
        {
            AdjectivePhraseDirectionality = HeadDirectionality.Final;
        }
        denominator++;

        if (rand.NextDouble() < numerator / denominator)
        {
            AdpositionalPhraseDirectionality = HeadDirectionality.Initial;
        }
        else
        {
            AdpositionalPhraseDirectionality = HeadDirectionality.Final;
        }
    }
}

public enum ConstituentWordOrder
{
    SVO,
    VSO,
    VOS,
    SOV,
    OSV,
    OVS
}

public enum HeadDirectionality
{
    Initial,
    Final
}
