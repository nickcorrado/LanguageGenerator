namespace LanguageGenerator;

//demonstratives
public static partial class Generator
{
    /*
      WALS 41A, Diessel's distance contrasts in adnominal demonstratives, of 234 languages: none in 7,
      two in 127, three in 88, four in 8 and five or more in 4. A clause file says only three
      distances, so the 12 with four or more are three here. Diessel finds three-way systems split
      between those ordered by distance, as Spanish este, ese and aquel, and those ordered by person,
      as Japanese ko-, so- and a-, whose middle is near the hearer; both say the same three, since
      Spanish ese is near the hearer too
     */
    private static readonly (DistanceContrast Value, int Languages)[] DistanceCounts =
        [(DistanceContrast.None, 7), (DistanceContrast.TwoWay, 127), (DistanceContrast.ThreeWay, 100)];

    /*
      WALS 88A, Dryer's order of demonstrative and noun, of 1,225 languages: before the noun in 542,
      after it in 562, a prefix in 9 and a suffix in 28. The 17 with one on each side and the 67 with
      no dominant order aren't made here
     */
    private static readonly (ExponentPosition Value, int Languages)[] DemonstrativePositionCounts =
    [
        (ExponentPosition.PrecedingParticle, 542), (ExponentPosition.FollowingParticle, 562),
        (ExponentPosition.Prefix, 9), (ExponentPosition.Suffix, 28),
    ];

    /*
      WALS 42A, Diessel's pronominal and adnominal demonstratives, of 201 languages: identical in 143,
      differing in inflection in 21, which are one here since a demonstrative standing alone always
      inflects as a noun does, and differing in stem in 37
     */
    private const double OwnStemChance = 37.0 / 201;

    /*
      Diessel finds demonstrative adverbs like 'here' and 'there' mostly built on the same stems as
      the demonstratives, as Japanese koko and Turkish bura are, but nobody counts it, so this is a
      guess
     */
    private const double PlacesFromDemonstrativesChance = 0.7;

    /*
      a demonstrative agrees with its noun's gender in most languages with gender, as in the Romance
      and Germanic languages, and in its number in many, as English this and these do. WALS counts
      neither, so these are guesses, a little likelier than an article's, since a demonstrative is a
      fuller word
     */
    private const double DemonstrativeGenderChance = 0.85;
    private const double DemonstrativeNumberChance = 0.6;

    //what 'here', 'there' and 'yonder' of their own are glossed, by the distance each says
    private static string PlaceGloss(Distance distance, DistanceContrast contrast) =>
        distance == Distance.Proximal ? "here"
        : distance == Distance.Distal && contrast == DistanceContrast.ThreeWay ? "yonder"
        : "there";

    /*
      how many distances, where the demonstrative goes, what agrees, and the forms: a short word or
      affix for each distance, gender and number it agrees in, glossed with what it says; a stem of
      its own for each distance standing alone; and words of their own for each place, all coined
      whatever is drawn. Adnominal forms are a syllable even as affixes, so that one can stand alone
      as a noun. An isolating language uses a word where an affix would go, on the same side, and a
      demonstrative that's an affix can't stand alone, so its pronouns have stems of their own
     */
    public static Demonstratives GenerateDemonstratives(Random rand, MorphologicalProfile morphology, GenderSystem genders, Phonology phonology)
    {
        var contrast = Sampling.Categorical(rand, DistanceCounts);
        var position = Sampling.Categorical(rand, DemonstrativePositionCounts);
        if (morphology.Fusion == Fusion.Isolating)
        {
            position = position switch
            {
                ExponentPosition.Prefix => ExponentPosition.PrecedingParticle,
                ExponentPosition.Suffix => ExponentPosition.FollowingParticle,
                _ => position,
            };
        }
        var isAffix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
        var agreesInGender = genders.Values.Count > 0 && rand.NextDouble() < DemonstrativeGenderChance;
        var agreesInNumber = rand.NextDouble() < DemonstrativeNumberChance;
        var ownStem = isAffix || rand.NextDouble() < OwnStemChance;
        var fromDemonstratives = rand.NextDouble() < PlacesFromDemonstrativesChance;

        var distances = Demonstratives.DistancesOf(contrast);
        Gender?[] genderValues = agreesInGender ? [.. genders.Values] : [null];
        Number?[] numberValues = agreesInNumber ? [Number.Singular, Number.Plural] : [null];
        var forms = new DistinctForms();
        var cells = new Dictionary<(Distance, Gender?, Number?), Morpheme>();
        foreach (var distance in distances)
        {
            foreach (var gender in genderValues)
            {
                foreach (var number in numberValues)
                {
                    var form = forms.Draw(() => MarkerForm(rand, false, phonology));
                    List<Grammeme> grammemes = [distance, .. new Grammeme?[] { gender, number }.OfType<Grammeme>()];
                    cells[(distance, gender, number)] = isAffix ? CoinAffix(rand, phonology, form, grammemes) : new Particle(form, grammemes);
                }
            }
        }
        var adnominal = distances.ToDictionary(d => d, d => new Lexeme(CoinRoot(rand, phonology, cells[(d, genderValues[0], numberValues[0])].Form, d.Gloss), PartOfSpeech.Noun));
        var own = distances.ToDictionary(d => d, d => new Lexeme(CoinRoot(rand, phonology, forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 1)), d.Gloss), PartOfSpeech.Noun));
        var places = distances.ToDictionary(d => d, d => new Lexeme(CoinRoot(rand, phonology, forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 2)), PlaceGloss(d, contrast)), PartOfSpeech.Noun));
        return new Demonstratives(contrast, position, cells, new PronominalDemonstratives(ownStem, adnominal, own), new PlaceDemonstratives(fromDemonstratives, places));
    }
}
