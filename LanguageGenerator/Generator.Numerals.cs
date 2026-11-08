namespace LanguageGenerator;

//numerals
public static partial class Generator
{
    //WALS 131A, leaving out its 5 languages with other bases and 4 that count on body parts. Its
    //hybrid vigesimal-decimal languages (22) and pure vigesimal ones (20) are both vigesimal here,
    //since most of the pure ones build 11 to 19 on ten or five too
    private static readonly (NumeralBase Value, int Languages)[] NumeralBaseCounts =
        [(NumeralBase.Decimal, 125), (NumeralBase.Vigesimal, 42), (NumeralBase.Restricted, 20)];
    //WALS 89A, leaving out its 21 languages with no dominant order and 37 with both
    private static readonly (bool Value, int Languages)[] NumeralBeforeNounCounts = [(true, 479), (false, 607)];
    //WALS 53A, of 321 languages. Its 33 with no ordinals at all say them with cardinals, as do its 13
    //whose ordinals are the cardinals, and its 8 with both 'first' and 'one-th' count as building
    //every ordinal. Its 35 with systems too various for these types are left out
    private static readonly (OrdinalType Value, int Languages)[] OrdinalCounts =
    [
        (OrdinalType.Cardinal, 46),
        (OrdinalType.FirstThenCardinal, 7),
        (OrdinalType.Derived, 62),
        (OrdinalType.FirstThenDerived, 110),
        (OrdinalType.FirstSecondThenDerived, 61),
    ];

    //a short word for each number the base needs, each redrawn to keep it from sounding like the
    //others. A restricted system's limit, and the odds of a singular noun after a numeral and of
    //compound numbers putting units last, are guesses: WALS doesn't count them
    public static NumeralSystem GenerateNumerals(Random rand, MorphologicalProfile morphology, Phonology phonology, double dropoff)
    {
        var numeralBase = Sampling.Categorical(rand, NumeralBaseCounts);
        var beforeNoun = Sampling.Categorical(rand, NumeralBeforeNounCounts);
        var nounStaysSingular = rand.NextDouble() < 0.4;
        var largestFirst = rand.NextDouble() < 0.8;
        var limit = numeralBase == NumeralBase.Restricted ? rand.Next(3, 6) : NumeralSystem.Largest;

        var forms = new DistinctForms();
        Lexeme Word(string meaning) =>
            new(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0.15, maxSyllables: 2)), meaning), PartOfSpeech.Numeral);

        var numbers = numeralBase switch
        {
            NumeralBase.Decimal => [.. Enumerable.Range(1, 9), 10, 100],
            NumeralBase.Vigesimal => [.. Enumerable.Range(1, 9), 10, 20, 400],
            _ => Enumerable.Range(1, limit).ToList(),
        };
        var words = numbers.ToDictionary(n => n, n => Word(NumeralNames[n]));
        var many = numeralBase == NumeralBase.Restricted ? Word("many") : null;
        var ordinals = GenerateOrdinals(rand, morphology, phonology, dropoff, Word);
        return new NumeralSystem(numeralBase, limit, words, many, beforeNoun, nounStaysSingular, largestFirst, ordinals);
    }

    //words of their own for 'first' and 'second' where the type has them, kept apart from the
    //cardinals, and a marker that builds the rest. WALS doesn't count how ordinals are marked, so
    //the marker goes on the side the language puts its other affixes, and an isolating language
    //uses a word, as Mandarin puts dì before the numeral in dì sān 'third'
    private static Ordinals GenerateOrdinals(Random rand, MorphologicalProfile morphology, Phonology phonology, double dropoff, Func<string, Lexeme> word)
    {
        var type = Sampling.Categorical(rand, OrdinalCounts);
        var first = type is OrdinalType.Cardinal or OrdinalType.Derived ? null : word("first");
        var second = type == OrdinalType.FirstSecondThenDerived ? word("second") : null;
        if (type is OrdinalType.Cardinal or OrdinalType.FirstThenCardinal)
        {
            return new Ordinals(type, first, second, null);
        }

        var before = rand.NextDouble() < PrefixChance(morphology);
        var position = morphology.Fusion == Fusion.Isolating
            ? (before ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle)
            : (before ? ExponentPosition.Prefix : ExponentPosition.Suffix);
        var affix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
        var form = MarkerForm(rand, affix, phonology, dropoff);
        Morpheme marker = affix ? new Affix(form, [Ordinality.Ordinal]) : new Particle(form, [Ordinality.Ordinal]);
        return new Ordinals(type, first, second, (position, marker));
    }

    //each numeral word's gloss
    private static readonly Dictionary<int, string> NumeralNames = new()
    {
        [1] = "one", [2] = "two", [3] = "three", [4] = "four", [5] = "five", [6] = "six", [7] = "seven", [8] = "eight", [9] = "nine",
        [10] = "ten", [20] = "twenty", [100] = "hundred", [400] = "four.hundred",
    };
}
