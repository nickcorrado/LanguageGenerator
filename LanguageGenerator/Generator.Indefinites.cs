namespace LanguageGenerator;

//quantifiers and indefinite pronouns
public static partial class Generator
{
    /*
      WALS 46A, Haspelmath's indefinite pronouns, of 326 languages: made from question words in 194,
      from generic nouns in 85, special in 22 and mixed in 23. The 2 that say them with an
      existential construction, 'there is who...', aren't made here
     */
    private static readonly (IndefinitePronouns Value, int Languages)[] IndefinitePronounCounts =
    [
        (IndefinitePronouns.InterrogativeBased, 194), (IndefinitePronouns.GenericNounBased, 85),
        (IndefinitePronouns.Special, 22), (IndefinitePronouns.Mixed, 23),
    ];

    /*
      WALS 115A, Haspelmath's negative indefinites and predicate negation, of 206 languages: with the
      verb negated too in 170, never in 11, and mixed in 13, which are made as Spanish is, negated
      where the indefinite follows the verb. The 12 that say 'nobody came' as 'there is no one who
      came' aren't made here
     */
    private static readonly (NegativeIndefiniteNegation Value, int Languages)[] NegativeIndefiniteCounts =
        [(NegativeIndefiniteNegation.Always, 170), (NegativeIndefiniteNegation.Never, 11), (NegativeIndefiniteNegation.AfterVerb, 13)];

    /*
      nobody counts where quantifiers go, but they mostly go as numerals do, as Greenberg's and Dryer's
      surveys find, so this is a guess. Nor does anyone count whether a marker making a question word
      indefinite is an affix, as Russian -to is, or a word, as Japanese ka is, or which side it goes
      on, so these are guesses too, an affix going on the side the language puts its others and a
      word mostly after, as Haspelmath's examples mostly have it
     */
    private const double QuantifierWithNumeralChance = 0.8;
    private const double IndefiniteAffixChance = 0.5;
    private const double IndefiniteWordAfterChance = 0.7;

    //what each indefinite pronoun of its own is glossed, by its series and kind
    private static string IndefiniteGloss(Quantifier series, Ontology kind) => (series, kind) switch
    {
        (Quantifier.Some, Ontology.Person) => "someone",
        (Quantifier.Some, Ontology.Thing) => "something",
        (Quantifier.Some, Ontology.Place) => "somewhere",
        (Quantifier.Some, Ontology.Time) => "sometime",
        (Quantifier.No, Ontology.Person) => "nobody",
        (Quantifier.No, Ontology.Thing) => "nothing",
        (Quantifier.No, Ontology.Place) => "nowhere",
        (Quantifier.No, Ontology.Time) => "never",
        (Quantifier.Every, Ontology.Person) => "everyone",
        (Quantifier.Every, Ontology.Thing) => "everything",
        (Quantifier.Every, Ontology.Place) => "everywhere",
        (Quantifier.Every, Ontology.Time) => "always",
        _ => throw new ArgumentOutOfRangeException(nameof(series), (series, kind), "No indefinite pronoun has this series and kind."),
    };

    /*
      what the indefinite pronouns are made from, for persons and things, a mixed language making one
      from question words and the other from nouns, either way around; where they're made from question
      words, a marker for each series, on one side, an isolating language's always words; where
      they're words of their own, one for each; a word for each quantifier, 'many' being the numerals' word for it where they have one; where they go;
      and whether a negative indefinite takes the verb's negation too
     */
    public static Indefinites GenerateIndefinites(Random rand, MorphologicalProfile morphology, NumeralSystem numerals, Phonology phonology)
    {
        var type = Sampling.Categorical(rand, IndefinitePronounCounts);
        var personsFromNoun = type == IndefinitePronouns.Mixed && rand.NextDouble() < 0.5;

        //the numerals' words are taken, 'many' among them, since a quantifier goes where they do
        var forms = new DistinctForms();
        foreach (var word in new[] { numerals.Many }.OfType<Lexeme>().Concat(numerals.Words.Values))
        {
            forms.Take(word.Root.Form);
        }
        //persons and things made from question words share their series' markers
        InterrogativeIndefinites? interrogative = null;
        InterrogativeIndefinites Interrogative() => interrogative ??= InterrogativeMarkers(rand, morphology, phonology, forms);
        SpecialIndefinites Special(params Ontology[] kinds) => new((
            from series in Indefinites.Series
            from kind in kinds
            select (series, kind)).ToDictionary(k => k, k =>
                new Lexeme(CoinRoot(rand, phonology, forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 2)), IndefiniteGloss(k.series, k.kind)), PartOfSpeech.Noun)));
        IndefiniteSource Source(bool fromNoun, params Ontology[] kinds) => type switch
        {
            IndefinitePronouns.InterrogativeBased => Interrogative(),
            IndefinitePronouns.GenericNounBased => new GenericNounIndefinites(),
            IndefinitePronouns.Special => Special(kinds),
            IndefinitePronouns.Mixed => fromNoun ? new GenericNounIndefinites() : Interrogative(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
        var persons = Source(personsFromNoun, Ontology.Person);
        var things = Source(!personsFromNoun, Ontology.Thing, Ontology.Place, Ontology.Time);

        var quantifiers = Enum.GetValues<Quantifier>().ToDictionary(q => q, q => q == Quantifier.Many && numerals.Many is { } many ? many
            : new Lexeme(CoinRoot(rand, phonology, forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 2)), q.ToString().ToLowerInvariant()), PartOfSpeech.Adjective));
        var beforeNoun = rand.NextDouble() < QuantifierWithNumeralChance ? numerals.BeforeNoun : !numerals.BeforeNoun;
        var negation = Sampling.Categorical(rand, NegativeIndefiniteCounts);
        return new Indefinites(persons, things, quantifiers, beforeNoun, negation);
    }

    //a marker for each series, an affix or a word as the language goes, all on one side of the question word
    private static InterrogativeIndefinites InterrogativeMarkers(Random rand, MorphologicalProfile morphology, Phonology phonology, DistinctForms forms)
    {
        var isAffix = morphology.Fusion != Fusion.Isolating && rand.NextDouble() < IndefiniteAffixChance;
        var side = isAffix
            ? (rand.NextDouble() < PrefixChance(morphology) ? Side.Before : Side.After)
            : (rand.NextDouble() < IndefiniteWordAfterChance ? Side.After : Side.Before);
        return new(Indefinites.Series.ToDictionary(s => s, s =>
        {
            var form = forms.Draw(() => MarkerForm(rand, isAffix, phonology));
            Grammeme meaning = s switch
            {
                Quantifier.Some => Quantification.Existential,
                Quantifier.No => Polarity.Negative,
                _ => Quantification.Universal,
            };
            return isAffix ? (Exponent)new Bound(CoinAffix(rand, phonology, form, [meaning]), side) : new Free(new Particle(form, [meaning]), side);
        }));
    }
}
