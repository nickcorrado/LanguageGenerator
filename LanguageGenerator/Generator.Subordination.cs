namespace LanguageGenerator;

//subordinate clauses
public static partial class Generator
{
    //WALS 94A crossed with 83A: how a subordinate clause is marked, in OV and VO languages. A word at
    //its start in 61 OV and 304 VO languages, at its end in 91 and 2, and a suffix on its verb in 56
    //and 1; the 3 and 3 with a word inside the clause are left out. 58 OV and 31 VO languages mix
    //these, so each of their subordinators is drawn on its own
    private static readonly (ExponentPosition Value, int Languages)[] OVSubordinatorCounts =
        [(ExponentPosition.PrecedingParticle, 61), (ExponentPosition.FollowingParticle, 91), (ExponentPosition.Suffix, 56)];
    private static readonly (ExponentPosition Value, int Languages)[] VOSubordinatorCounts =
        [(ExponentPosition.PrecedingParticle, 304), (ExponentPosition.FollowingParticle, 2), (ExponentPosition.Suffix, 1)];
    private const double OVMixed = 58.0 / (61 + 91 + 56 + 58);
    private const double VOMixed = 31.0 / (304 + 2 + 1 + 31);

    //WALS 125A to 127A, Cristofaro's counts of balanced, deranked and either: 38, 102 and 30 for
    //purpose clauses, 84, 51 and 39 for 'when' clauses, and 90, 42 and 37 for reason clauses.
    //The other temporal and conditional clauses, which WALS doesn't count, follow 'when'
    private static readonly (Balance Value, int Languages)[] PurposeBalance = [(Balance.Balanced, 38), (Balance.Deranked, 102), (Balance.Either, 30)];
    private static readonly (Balance Value, int Languages)[] WhenBalance = [(Balance.Balanced, 84), (Balance.Deranked, 51), (Balance.Either, 39)];
    private static readonly (Balance Value, int Languages)[] ReasonBalance = [(Balance.Balanced, 90), (Balance.Deranked, 42), (Balance.Either, 37)];

    //how often each kind of clause comes before the main one. Guesses after Diessel's study of their
    //order: in OV languages nearly all go first; in VO languages a conditional goes first, as
    //Greenberg's Universal 14 has it, 'when' and 'after' clauses mostly do, and 'before', 'until',
    //reason and purpose clauses mostly follow, in the order their events happen
    private static double BeforeMainChance(Subordinator subordinator, bool objectFirst) => objectFirst
        ? (subordinator == Subordinator.Purpose ? 0.8 : 0.9)
        : subordinator switch
        {
            Subordinator.If => 0.85,
            Subordinator.When => 0.6,
            Subordinator.After => 0.65,
            Subordinator.Before => 0.35,
            Subordinator.Until => 0.15,
            Subordinator.Because => 0.2,
            Subordinator.Purpose => 0.15,
            _ => throw new ArgumentOutOfRangeException(nameof(subordinator), subordinator, null),
        };

    //a short word or affix for each subordinator, kept from sounding alike. An isolating language
    //puts a word at the clause's end where another would use a suffix
    public static Subordination GenerateSubordination(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var objectFirst = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Final;
        var counts = objectFirst ? OVSubordinatorCounts : VOSubordinatorCounts;
        var mixed = rand.NextDouble() < (objectFirst ? OVMixed : VOMixed);
        var shared = Sampling.Categorical(rand, counts);
        var whenBalance = Sampling.Categorical(rand, WhenBalance);
        var balances = new Dictionary<Subordinator, Balance>
        {
            [Subordinator.Purpose] = Sampling.Categorical(rand, PurposeBalance),
            [Subordinator.Because] = Sampling.Categorical(rand, ReasonBalance),
        };

        var forms = new DistinctForms();
        var markers = new Dictionary<Subordinator, SubordinatorMarking>();
        foreach (var subordinator in Enum.GetValues<Subordinator>())
        {
            var position = mixed ? Sampling.Categorical(rand, counts) : shared;
            if (position == ExponentPosition.Suffix && morphology.Fusion == Fusion.Isolating)
            {
                position = ExponentPosition.FollowingParticle;
            }
            var affix = position == ExponentPosition.Suffix;
            var form = forms.Draw(() => affix ? MarkerForm(rand, true, phonology, dropoff) : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff, needsOnset: false).Phonemes]);
            List<Grammeme> meaning = [Linkage.For(subordinator)];
            markers[subordinator] = new SubordinatorMarking(affix ? new Affix(form, meaning) : new Particle(form, meaning), position,
                balances.GetValueOrDefault(subordinator, whenBalance), rand.NextDouble() < BeforeMainChance(subordinator, objectFirst));
        }
        return new Subordination(markers);
    }
}
