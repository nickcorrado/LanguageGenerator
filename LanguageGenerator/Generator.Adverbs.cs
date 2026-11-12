namespace LanguageGenerator;

//adverbs: 'very', manner adverbs, and how a duration is marked
public static partial class Generator
{
    //'very' is a word beside the adjective, as nearly every language's is, on the side the language
    //puts degree words, as its comparative's is where that's a word too. Its form is kept apart
    //from the comparative's
    public static DegreeWord GenerateIntensifier(Random rand, DegreeMarking degree, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var forms = new DistinctForms();
        if (degree.Marker.Value is DegreeWord comparative)
        {
            forms.Take(comparative.Marker.Form);
        }
        return new DegreeWord(new Particle(forms.Draw(() => MarkerForm(rand, false, phonology, dropoff)), [Degree.Intensive]), DegreeWordBefore(wordOrder));
    }

    private enum MannerType { Bare, Affix, Word }

    //WALS doesn't count how languages make manner adverbs, so these are guesses: many use the
    //adjective itself, as German and Mandarin often do, many an affix, as English -ly, Turkish -ce
    //and Russian -o, and fewer a word beside it, as Mandarin de and Japanese ni. An isolating
    //language has no affix. A word mostly follows the adjective, as both of those do
    private static readonly (MannerType Value, int Weight)[] MannerCounts = [(MannerType.Bare, 40), (MannerType.Affix, 40), (MannerType.Word, 20)];
    private const double MannerWordBeforeChance = 0.3;

    public static MannerMarking GenerateManner(Random rand, MorphologicalProfile morphology, Phonology phonology, double dropoff)
    {
        var counts = morphology.Fusion == Fusion.Isolating ? MannerCounts.Where(c => c.Value != MannerType.Affix).ToArray() : MannerCounts;
        return Sampling.Categorical(rand, counts) switch
        {
            MannerType.Bare => new MannerBare(),
            MannerType.Affix => new MannerAffix(new Affix(MarkerForm(rand, true, phonology, dropoff), [Derivation.Adverbial]),
                rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix),
            _ => new MannerWord(new Particle(MarkerForm(rand, false, phonology, dropoff), [Derivation.Adverbial]), rand.NextDouble() < MannerWordBeforeChance),
        };
    }

    private enum DurationType { AsObject, AsTime, Word }

    //how long something goes on is often marked like an object, as Latin duos annos and Russian vsju
    //noč' 'all night' are accusative, and Mandarin's is a bare phrase after the verb; often with a
    //word of its own, as English for, Spanish durante and Turkish boyunca; and sometimes like a time.
    //WALS doesn't count these, so the odds are guesses
    private static readonly (DurationType Value, int Weight)[] DurationCounts = [(DurationType.AsObject, 40), (DurationType.Word, 35), (DurationType.AsTime, 25)];

    /// <summary>
    /// How a language marks a duration: like another role, or with a word of its own, a one-syllable
    /// adposition kept apart from the others.
    /// </summary>
    public static (Role? Like, Lexeme? Word) GenerateDuration(Random rand, IReadOnlyDictionary<Role, Lexeme> adpositions, Phonology phonology, double dropoff)
    {
        switch (Sampling.Categorical(rand, DurationCounts))
        {
            case DurationType.AsObject:
                return (Role.DirectObject, null);
            case DurationType.AsTime:
                return (Role.Time, null);
            default:
                var forms = new DistinctForms(draws: 11);
                foreach (var word in adpositions.Values.Distinct())
                {
                    forms.Take(word.Root.Form);
                }
                return (null, new Lexeme(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1)), "for"), PartOfSpeech.Adposition));
        }
    }
}
