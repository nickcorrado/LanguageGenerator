namespace LanguageGenerator;

//possessive phrases
public static partial class Generator
{
    //WALS 24A, leaving out its 6 languages that mark possession some other way
    private static readonly (PossessiveMarking Value, int Languages)[] PossessiveMarkingCounts =
    [
        (PossessiveMarking.Dependent, 98),
        (PossessiveMarking.Head, 78),
        (PossessiveMarking.Double, 22),
        (PossessiveMarking.None, 32),
    ];

    //possessors go where adpositions' objects do, before the noun with postpositions and after it
    //with prepositions: Greenberg's Universal 2, which has few exceptions. An isolating language
    //can't mark the possessed noun with an affix, so it marks the possessor, as Mandarin does with
    //de. The affixes agreeing with a possessor are each a short vowel or a syllable, all on one side
    public static Possession GeneratePossession(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var marking = Sampling.Categorical(rand, PossessiveMarkingCounts);
        if (morphology.Fusion == Fusion.Isolating && marking is PossessiveMarking.Head or PossessiveMarking.Double)
        {
            marking = PossessiveMarking.Dependent;
        }
        var possessorFirst = wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Final;
        if (marking is not (PossessiveMarking.Head or PossessiveMarking.Double))
        {
            return new Possession(marking, possessorFirst, null, ExponentPosition.Suffix);
        }

        var position = rand.NextDouble() < morphology.PrefixChance ? ExponentPosition.Prefix : ExponentPosition.Suffix;
        var affixes = new Dictionary<(Person, Number), Affix>();
        var forms = new DistinctForms();
        foreach (var person in new[] { Person.First, Person.Second, Person.Third })
        {
            foreach (var number in new[] { Number.Singular, Number.Plural })
            {
                var form = forms.Draw(() => VowelOrSyllable(rand, phonology, dropoff));
                affixes[(person, number)] = new Affix(form, [person, number, Possessive.Possessed]);
            }
        }
        return new Possession(marking, possessorFirst, affixes, position);
    }
}
