namespace LanguageGenerator;

//how words are built: fusion, affixation, zero marking and paradigms
public static partial class Generator
{
    //case and number particles on nouns behave like adpositions, so they follow the same directionality
    public static Paradigm GenerateNounParadigm(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, MorphosyntacticAlignment alignment, IReadOnlyList<Case> cases, Phonology phonology, double dropoff) =>
        GenerateParadigm(rand, PartOfSpeech.Noun, SelectNounCategories(rand, alignment, cases), morphology, wordOrder.AdpositionalPhraseDirectionality, phonology, dropoff);

    public static Paradigm GenerateVerbParadigm(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff) =>
        GenerateParadigm(rand, PartOfSpeech.Verb, SelectVerbCategories(rand), morphology, wordOrder.VerbPhraseDirectionality, phonology, dropoff);

    //innermost first: number sits closer to the root than case, as it overwhelmingly does.
    //gender isn't here because a noun doesn't inflect for it; it has one, and adjectives and
    //verbs agree with it
    public static List<InflectionalCategory> SelectNounCategories(Random rand, MorphosyntacticAlignment alignment, IReadOnlyList<Case> cases)
    {
        var categories = new List<InflectionalCategory>();
        if (rand.NextDouble() < 0.85)
        {
            List<Grammeme> numbers = rand.NextDouble() < 0.1 ? [Number.Singular, Number.Dual, Number.Plural] : [Number.Singular, Number.Plural];
            categories.Add(new("Number", numbers));
        }
        //with no core cases, a noun still needs a plain form for everything the peripheral cases don't cover
        if (cases.Count > 0)
        {
            categories.Add(alignment == MorphosyntacticAlignment.None ? new("Case", [Case.Direct, .. cases]) : new("Case", [.. cases]));
        }
        return categories;
    }

    //innermost first: aspect, then tense, then subject agreement, the usual order outward from the root
    public static List<InflectionalCategory> SelectVerbCategories(Random rand)
    {
        var categories = new List<InflectionalCategory>();
        if (rand.NextDouble() < 0.3)
        {
            categories.Add(new("Aspect", [Aspect.Imperfective, Aspect.Perfective]));
        }
        if (rand.NextDouble() < 0.8)
        {
            List<Grammeme> tenses = rand.NextDouble() < 0.4 ? [Tense.Present, Tense.Past, Tense.Future] : [Tense.Present, Tense.Past];
            categories.Add(new("Tense", tenses));
        }
        if (rand.NextDouble() < 0.6)
        {
            categories.Add(new("Person", [Person.First, Person.Second, Person.Third]));
            categories.Add(new("Number", [Number.Singular, Number.Plural]));
        }
        return categories;
    }

    //whether a category shares its marker with the one just inside it
    private static bool FusesWithPrevious(InflectionalCategory inner, InflectionalCategory outer, Exponence exponence) => (inner.Values[0], outer.Values[0]) switch
    {
        //person and number agreement is fused in nearly every language that has it (1SG, 3PL)
        (Person, Number) => true,
        (Number, Case) => exponence.CaseWithNumber,
        (Tense or Aspect, Person) => exponence.TamWithAgreement,
        (Aspect, Tense) => exponence.TenseWithAspect,
        _ => false,
    };

    //how often a new marker is a separate word rather than an affix
    private static double ParticleChance(MorphologicalProfile morphology) => morphology.Fusion switch
    {
        Fusion.Concatenative => 0,
        Fusion.Mixed => 0.5,
        Fusion.Isolating => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(morphology), morphology.Fusion, null),
    };

    //the middle of each 26A band, which WALS defines by the share of affixes that are prefixes
    private static double PrefixChance(MorphologicalProfile morphology) => morphology.Affixation switch
    {
        Affixation.StronglySuffixing => 0.1,
        Affixation.WeaklySuffixing => 0.3,
        Affixation.Equal => 0.5,
        Affixation.WeaklyPrefixing => 0.7,
        Affixation.StronglyPrefixing => 0.9,
        _ => throw new ArgumentOutOfRangeException(nameof(morphology), morphology.Affixation, null),
    };

    public static Paradigm GenerateParadigm(Random rand, PartOfSpeech partOfSpeech, IReadOnlyList<InflectionalCategory> categories, MorphologicalProfile morphology, HeadDirectionality directionality, Phonology phonology, double dropoff)
    {
        var particlePosition = directionality == HeadDirectionality.Initial ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle;

        var groups = new List<(List<InflectionalCategory> Categories, ExponentPosition Position)>();
        foreach (var category in categories)
        {
            if (groups.Count > 0 && FusesWithPrevious(groups[^1].Categories[^1], category, morphology.Exponence))
            {
                groups[^1].Categories.Add(category);
            }
            else if (rand.NextDouble() < ParticleChance(morphology))
            {
                groups.Add(([category], particlePosition));
            }
            else
            {
                groups.Add(([category], rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix));
            }
        }

        var slots = new List<ExponentSlot>();
        foreach (var (slotCategories, position) in groups)
        {
            slots.Add(new ExponentSlot
            {
                Categories = slotCategories,
                Position = position,
                Exponents = GenerateExponents(rand, slotCategories, position, morphology.ZeroMarking, phonology, dropoff),
            });
        }
        return new Paradigm { PartOfSpeech = partOfSpeech, Categories = categories, Slots = slots };
    }

    //one exponent per combination of values, with no form at all for combinations the language
    //leaves unmarked. Other forms are kept distinct within a slot where possible; if the phonology
    //keeps producing repeats, the syncretism is allowed
    private static List<(IReadOnlyList<Grammeme> Values, Morpheme Exponent)> GenerateExponents(Random rand, IReadOnlyList<InflectionalCategory> categories, ExponentPosition position, ZeroMarking zeroMarking, Phonology phonology, double dropoff)
    {
        var exponents = new List<(IReadOnlyList<Grammeme>, Morpheme)>();
        var forms = new DistinctForms();
        foreach (var values in Paradigm.Combinations(categories))
        {
            if (zeroMarking.IsZero(values))
            {
                exponents.Add((values, position is ExponentPosition.Prefix or ExponentPosition.Suffix ? new Affix([], values) : new Particle([], values)));
                continue;
            }
            //affixes are often a bare consonant (English -s, -t), which is what gives sandhi and cluster
            //repair work to do; particles are words of their own, so they're always a whole syllable
            var bare = position is ExponentPosition.Prefix or ExponentPosition.Suffix && rand.NextDouble() < 0.3;
            var form = forms.Draw(() => bare
                ? [phonology.Consonants[Sampling.TruncatedGeometric(rand, phonology.Consonants.Count, dropoff)]]
                : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff, needsOnset: false).Phonemes]);
            Morpheme exponent = position is ExponentPosition.Prefix or ExponentPosition.Suffix ? new Affix(form, values) : new Particle(form, values);
            exponents.Add((values, exponent));
        }
        return exponents;
    }

    //WALS won't classify languages as fusional, agglutinative or analytic, but it does measure the
    //properties underneath, each drawn here in proportion to WALS's language counts

    // https://wals.info/combinations/20A_83A: fusion by the order of object and verb. Isolating
    //morphology is far commoner in VO languages; the OV counts are small, but 26A's languages with
    //little affixation lean the same way (35 OV to 100 VO). Neither tone nor ablaut is modeled, so
    //values pairing one with another strategy count as that strategy, and the exclusively tonal
    //languages are left out, as are those with no dominant order
    private static readonly (Fusion Value, int Languages)[] ObjectVerbFusionCounts =
    [
        (Fusion.Concatenative, 70 + 1 + 0),
        (Fusion.Mixed, 5),
        (Fusion.Isolating, 3 + 0),
    ];
    private static readonly (Fusion Value, int Languages)[] VerbObjectFusionCounts =
    [
        (Fusion.Concatenative, 38 + 3 + 2),
        (Fusion.Mixed, 7),
        (Fusion.Isolating, 12 + 1),
    ];

    // https://wals.info/combinations/26A_83A: affixation by the order of object and verb, which makes
    //a big difference, since OV languages strongly prefer suffixes. Languages with little or no
    //affixation are left out, since fusion already decides that, and so are the 60 with no
    //dominant order, since every generated language has one
    private static readonly (Affixation Value, int Languages)[] ObjectVerbAffixationCounts =
    [
        (Affixation.StronglySuffixing, 269),
        (Affixation.WeaklySuffixing, 70),
        (Affixation.Equal, 49),
        (Affixation.WeaklyPrefixing, 23),
        (Affixation.StronglyPrefixing, 6),
    ];
    private static readonly (Affixation Value, int Languages)[] VerbObjectAffixationCounts =
    [
        (Affixation.StronglySuffixing, 93),
        (Affixation.WeaklySuffixing, 43),
        (Affixation.Equal, 78),
        (Affixation.WeaklyPrefixing, 61),
        (Affixation.StronglyPrefixing, 51),
    ];

    public static MorphologicalProfile SelectMorphologicalProfile(Random rand, ConstituentWordOrder order) => new(
        Sampling.Categorical(rand, WordOrder.ObjectPrecedesVerb(order) ? ObjectVerbFusionCounts : VerbObjectFusionCounts),
        new Exponence(
            // https://wals.info/chapter/21: 8 of the 87 languages with case mark it together with number
            CaseWithNumber: rand.NextDouble() < 8 / 87.0,
            //and 24 of the 156 with tense-aspect-mood inflection mark it together with agreement
            TamWithAgreement: rand.NextDouble() < 24 / 156.0,
            //a guess: WALS counts tense and aspect together as TAM, so it has no figure for this
            TenseWithAspect: rand.NextDouble() < 0.3),
        Sampling.Categorical(rand, WordOrder.ObjectPrecedesVerb(order) ? ObjectVerbAffixationCounts : VerbObjectAffixationCounts),
        new ZeroMarking(
            //guesses, but grounded in Greenberg's universals: the singular can be expressed only by
            //zero while the plural never is (35), and the only case that can be zero is the one for
            //the intransitive subject (38). WALS has no counts for either, or for the present tense
            Singular: rand.NextDouble() < 0.9,
            CoreCase: rand.NextDouble() < 0.8,
            Present: rand.NextDouble() < 0.6,
            ThirdPerson: Sampling.Categorical(rand, ThirdPersonZeroCounts)));

    // https://wals.info/feature/103A, among the 284 languages with person marking on verbs. The 21
    //with zero in only some third singular forms are left out, since a paradigm here has one form
    //per cell, and so is the 1 with zero only in the third person nonsingular
    private static readonly (ThirdPersonZero Value, int Languages)[] ThirdPersonZeroCounts =
    [
        (ThirdPersonZero.None, 181),
        (ThirdPersonZero.Singular, 45),
        (ThirdPersonZero.All, 36),
    ];
}
