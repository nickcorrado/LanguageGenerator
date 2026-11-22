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
      from question words and the other from nouns, either way around; the markers for question words,
      on one side, an isolating language's always words; a word of its own for each indefinite; a word
      for each quantifier, 'many' being the numerals' word for it where they have one; where they go;
      and whether a negative indefinite takes the verb's negation too
     */
    public static Indefinites GenerateIndefinites(Random rand, MorphologicalProfile morphology, NumeralSystem numerals, Phonology phonology)
    {
        var (personBase, thingBase) = Sampling.Categorical(rand, IndefinitePronounCounts) switch
        {
            IndefinitePronouns.InterrogativeBased => (IndefiniteBase.Interrogative, IndefiniteBase.Interrogative),
            IndefinitePronouns.GenericNounBased => (IndefiniteBase.GenericNoun, IndefiniteBase.GenericNoun),
            IndefinitePronouns.Special => (IndefiniteBase.Special, IndefiniteBase.Special),
            _ => rand.NextDouble() < 0.5 ? (IndefiniteBase.GenericNoun, IndefiniteBase.Interrogative) : (IndefiniteBase.Interrogative, IndefiniteBase.GenericNoun),
        };
        var position = morphology.Fusion != Fusion.Isolating && rand.NextDouble() < IndefiniteAffixChance
            ? (rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix)
            : (rand.NextDouble() < IndefiniteWordAfterChance ? ExponentPosition.FollowingParticle : ExponentPosition.PrecedingParticle);
        var isAffix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;

        //the numerals' words are taken, 'many' among them, since a quantifier goes where they do
        var forms = new DistinctForms();
        foreach (var word in new[] { numerals.Many }.OfType<Lexeme>().Concat(numerals.Words.Values))
        {
            forms.Take(word.Root.Form);
        }
        var markers = Indefinites.Series.ToDictionary(s => s, s =>
        {
            var form = forms.Draw(() => MarkerForm(rand, isAffix, phonology));
            Grammeme meaning = s switch
            {
                Quantifier.Some => Quantification.Existential,
                Quantifier.No => Polarity.Negative,
                _ => Quantification.Universal,
            };
            return isAffix ? (Morpheme)new Affix(form, [meaning]) : new Particle(form, [meaning]);
        });
        var own = new Dictionary<(Quantifier, Ontology), Lexeme>();
        void Own(Quantifier series, Ontology kind) =>
            own[(series, kind)] = new Lexeme(new Root(forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 2)), IndefiniteGloss(series, kind)), PartOfSpeech.Noun);
        foreach (var series in Indefinites.Series)
        {
            foreach (var kind in new[] { Ontology.Person, Ontology.Thing, Ontology.Place })
            {
                Own(series, kind);
            }
        }
        var quantifiers = Enum.GetValues<Quantifier>().ToDictionary(q => q, q => q == Quantifier.Many && numerals.Many is { } many ? many
            : new Lexeme(new Root(forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 2)), q.ToString().ToLowerInvariant()), PartOfSpeech.Adjective));
        var beforeNoun = rand.NextDouble() < QuantifierWithNumeralChance ? numerals.BeforeNoun : !numerals.BeforeNoun;
        var negation = Sampling.Categorical(rand, NegativeIndefiniteCounts);
        //times were added later, so they're drawn last, leaving what was drawn before as it was
        foreach (var series in Indefinites.Series)
        {
            Own(series, Ontology.Time);
        }
        return new Indefinites(personBase, thingBase, position, markers, own, quantifiers, beforeNoun, negation);
    }
}
