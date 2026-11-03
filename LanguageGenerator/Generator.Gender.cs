namespace LanguageGenerator;

//noun gender and the markers that agree with it
public static partial class Generator
{
    //WALS 30A for how many genders, leaving out its 36 languages with four or more. 31A says some of
    //the two-gender systems aren't sex-based; splitting them about four to one into masculine and
    //feminine and animate and inanimate is a guess
    private static readonly (NounGenders Value, int Languages)[] NounGenderCounts =
    [
        (NounGenders.None, 145),
        (NounGenders.MasculineFeminine, 40),
        (NounGenders.AnimateInanimate, 10),
        (NounGenders.MasculineFeminineNeuter, 26),
    ];
    //WALS 32A
    private static readonly (GenderAssignment Value, int Languages)[] GenderAssignmentCounts =
        [(GenderAssignment.Semantic, 53), (GenderAssignment.SemanticAndFormal, 59)];

    //a marker per gender for words that agree with a noun, all on one side, with the first gender
    //unmarked, as the masculine or the animate usually is. Whether verbs agree too, where they agree
    //with their subject at all, is a guess at even odds
    public static GenderSystem GenerateGenders(Random rand, MorphologicalProfile morphology, Dictionary<PartOfSpeech, Paradigm> paradigms, Phonology phonology, double dropoff)
    {
        var kind = Sampling.Categorical(rand, NounGenderCounts);
        var assignment = Sampling.Categorical(rand, GenderAssignmentCounts);
        var position = rand.NextDouble() < morphology.PrefixChance ? ExponentPosition.Prefix : ExponentPosition.Suffix;
        var verbsAgree = rand.NextDouble() < 0.5 && paradigms[PartOfSpeech.Verb].Categories.Has<Person>();
        var system = new GenderSystem { Genders = kind, Assignment = assignment, Markers = null, MarkerPosition = position, VerbsAgree = false };
        if (kind == NounGenders.None || morphology.Fusion == Fusion.Isolating)
        {
            return system;
        }

        var markers = new Dictionary<Gender, Affix>();
        var forms = new DistinctForms();
        foreach (var (gender, i) in system.Values.Select((g, i) => (g, i)))
        {
            markers[gender] = new Affix(i == 0 ? [] : forms.Draw(() => VowelOrSyllable(rand, phonology, dropoff)), [gender]);
        }
        return new GenderSystem { Genders = kind, Assignment = assignment, Markers = markers, MarkerPosition = position, VerbsAgree = verbsAgree };
    }
}
