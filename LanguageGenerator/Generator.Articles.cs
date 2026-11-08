namespace LanguageGenerator;

//articles
public static partial class Generator
{
    private enum DefiniteType { Word, Affix, None }
    private enum IndefiniteType { Word, One, Affix }

    //WALS 37A, of 620 languages: 216 with a definite word distinct from the demonstrative and 69 that
    //use a demonstrative, which are both a word here, since there are no demonstratives yet; 92 with
    //an affix; and 45 with only an indefinite article and 198 with neither, which are both None
    private static readonly (DefiniteType Value, int Languages)[] DefiniteCounts =
        [(DefiniteType.Word, 285), (DefiniteType.Affix, 92), (DefiniteType.None, 243)];

    //WALS 38A, of 534 languages: 102 with an indefinite word distinct from 'one', 112 that use 'one'
    //and 24 with an affix, 98 with only a definite article and 198 with neither. Given a definite
    //article, 193 of the 291 with one have an indefinite article too, and without, 45 of 243, the
    //latter from 37A since 38A doesn't split them
    private static readonly (IndefiniteType Value, int Languages)[] IndefiniteCounts =
        [(IndefiniteType.Word, 102), (IndefiniteType.One, 112), (IndefiniteType.Affix, 24)];
    private const double IndefiniteGivenDefinite = 193.0 / 291;
    private const double IndefiniteWithoutDefinite = 45.0 / 243;

    //WALS 88A, of the 1,104 languages with a dominant order of demonstrative and noun, 542 put the
    //demonstrative first. An article word goes where a demonstrative would, which WALS doesn't count
    //for articles themselves, so that's a guess, and a language's two article words go on one side
    private static readonly (bool Value, int Languages)[] ArticleBeforeNounCounts = [(true, 542), (false, 562)];

    //an isolating language has no affixes to make articles of, so it uses a word where an affix would
    //go. An affix goes on the side the language puts its others. The indefinite article that's the
    //word for 'one' stands where numerals do, and agrees in nothing. Other articles agree with their
    //noun's gender in most languages that have gender, as in the Romance and Germanic languages and
    //Greek, though Arabic al- doesn't, and a definite article agrees in number in some, as French le
    //and les do but English the doesn't. WALS doesn't count either, so the odds are guesses. An
    //indefinite article is for one thing only, so it never agrees in number
    public static Articles GenerateArticles(Random rand, MorphologicalProfile morphology, GenderSystem genders, NumeralSystem numerals, Phonology phonology, double dropoff)
    {
        var definiteType = Sampling.Categorical(rand, DefiniteCounts);
        var hasIndefinite = rand.NextDouble() < (definiteType == DefiniteType.None ? IndefiniteWithoutDefinite : IndefiniteGivenDefinite);
        IndefiniteType? indefiniteType = hasIndefinite ? Sampling.Categorical(rand, IndefiniteCounts) : null;
        var wordFirst = Sampling.Categorical(rand, ArticleBeforeNounCounts);
        var isolating = morphology.Fusion == Fusion.Isolating;
        var agreesInGender = genders.Values.Count > 0 && rand.NextDouble() < 0.8;
        var agreesInNumber = rand.NextDouble() < 0.5;

        var forms = new DistinctForms();
        foreach (var word in numerals.Words.Values)
        {
            forms.Take(word.Root.Form);
        }
        Article Marker(bool affix, Definiteness meaning)
        {
            var position = affix && !isolating
                ? (rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix)
                : (wordFirst ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle);
            var isAffix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
            Gender?[] genderValues = agreesInGender ? [.. genders.Values] : [null];
            Number?[] numberValues = agreesInNumber && meaning == Definiteness.Definite ? [Number.Singular, Number.Plural] : [null];
            var cells = new Dictionary<(Gender?, Number?), Morpheme>();
            foreach (var gender in genderValues)
            {
                foreach (var number in numberValues)
                {
                    var form = forms.Draw(() => MarkerForm(rand, isAffix, phonology, dropoff));
                    List<Grammeme> grammemes = [meaning, .. new Grammeme?[] { gender, number }.OfType<Grammeme>()];
                    cells[(gender, number)] = isAffix ? new Affix(form, grammemes) : new Particle(form, grammemes);
                }
            }
            return new Article(position, cells);
        }

        var definite = definiteType switch
        {
            DefiniteType.Word => Marker(false, Definiteness.Definite),
            DefiniteType.Affix => Marker(true, Definiteness.Definite),
            _ => null,
        };
        var indefinite = indefiniteType switch
        {
            IndefiniteType.Word => Marker(false, Definiteness.Indefinite),
            IndefiniteType.Affix => Marker(true, Definiteness.Indefinite),
            IndefiniteType.One => new Article(numerals.BeforeNoun ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle,
                new Dictionary<(Gender?, Number?), Morpheme> { [(null, null)] = numerals.Words[1].Root }),
            _ => null,
        };
        return new Articles(definite, indefinite);
    }
}
