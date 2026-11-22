namespace LanguageGenerator;

//articles
public static partial class Generator
{
    /*
      WALS 37A, of 620 languages: 216 with a definite word distinct from the demonstrative, 69 that
      use a demonstrative and 92 with an affix; and 45 with only an indefinite article and 198 with
      neither, which are both None. The demonstrative follows the word, so a seed that drew a word
      before there were demonstratives draws a word or a demonstrative now, and the rest draw what
      they did
     */
    private static readonly (DefiniteArticle Value, int Languages)[] DefiniteCounts =
        [(DefiniteArticle.Word, 216), (DefiniteArticle.Demonstrative, 69), (DefiniteArticle.Affix, 92), (DefiniteArticle.None, 243)];

    /*
      WALS 38A, of 534 languages: 102 with an indefinite word distinct from 'one', 112 that use 'one'
      and 24 with an affix, 98 with only a definite article and 198 with neither. Given a definite
      article, 193 of the 291 with one have an indefinite article too, and without, 45 of 243, the
      latter from 37A since 38A doesn't split them
     */
    private static readonly (IndefiniteArticle Value, int Languages)[] IndefiniteCounts =
        [(IndefiniteArticle.Word, 102), (IndefiniteArticle.One, 112), (IndefiniteArticle.Affix, 24)];
    private const double IndefiniteGivenDefinite = 193.0 / 291;
    private const double IndefiniteWithoutDefinite = 45.0 / 243;

    /*
      WALS 88A, of the 1,104 languages with a dominant order of demonstrative and noun, 542 put the
      demonstrative first. An article word goes where a demonstrative would, which WALS doesn't count
      for articles themselves, so that's a guess, drawn apart from the language's own demonstrative,
      and a language's two article words go on one side. An article that's the demonstrative goes
      where the demonstrative does
     */
    private static readonly (bool Value, int Languages)[] ArticleBeforeNounCounts = [(true, 542), (false, 562)];

    /*
      how often a definite article stays beside a demonstrative, as in Greek aftó to spíti 'this the
      house', Hungarian ez a ház and Romanian omul acesta 'man-the this', where English drops it.
      Nobody counts it, so it's a guess
     */
    private const double ArticleWithDemonstrativeChance = 0.3;

    /*
      an isolating language has no affixes to make articles of, so it uses a word where an affix would
      go. An affix goes on the side the language puts its others. The indefinite article that's the
      word for 'one' stands where numerals do, and agrees in nothing. Other articles agree with their
      noun's gender in most languages that have gender, as in the Romance and Germanic languages and
      Greek, though Arabic al- doesn't, and a definite article agrees in number in some, as French le
      and les do but English the doesn't. WALS doesn't count either, so the odds are guesses. An
      indefinite article is for one thing only, so it never agrees in number. A definite article
      that's the demonstrative has the distal demonstrative's forms and place, glossed DEF, and
      agrees as the article does: where the demonstrative doesn't agree, it has one form for all
     */
    public static Articles GenerateArticles(Random rand, MorphologicalProfile morphology, GenderSystem genders, NumeralSystem numerals, Demonstratives demonstratives, Phonology phonology)
    {
        var definiteType = Sampling.Categorical(rand, DefiniteCounts);
        var hasIndefinite = rand.NextDouble() < (definiteType == DefiniteArticle.None ? IndefiniteWithoutDefinite : IndefiniteGivenDefinite);
        var indefiniteType = hasIndefinite ? Sampling.Categorical(rand, IndefiniteCounts) : IndefiniteArticle.None;
        var wordFirst = Sampling.Categorical(rand, ArticleBeforeNounCounts);
        var isolating = morphology.Fusion == Fusion.Isolating;
        var agreesInGender = genders.Values.Count > 0 && rand.NextDouble() < 0.8;
        var agreesInNumber = rand.NextDouble() < 0.5;

        var forms = new DistinctForms();
        foreach (var word in numerals.Words.Values)
        {
            forms.Take(word.Root.Form);
        }
        foreach (var form in demonstratives.Forms.Values)
        {
            forms.Take(form.Form);
        }
        Gender?[] GenderValues() => agreesInGender ? [.. genders.Values] : [null];
        Number?[] NumberValues(Definiteness meaning) => agreesInNumber && meaning == Definiteness.Definite ? [Number.Singular, Number.Plural] : [null];
        Article Marker(bool affix, Definiteness meaning)
        {
            var position = affix && !isolating
                ? (rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix)
                : (wordFirst ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle);
            var isAffix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
            var cells = new Dictionary<(Gender?, Number?), Morpheme>();
            foreach (var gender in GenderValues())
            {
                foreach (var number in NumberValues(meaning))
                {
                    var form = forms.Draw(() => MarkerForm(rand, isAffix, phonology));
                    List<Grammeme> grammemes = [meaning, .. new Grammeme?[] { gender, number }.OfType<Grammeme>()];
                    cells[(gender, number)] = isAffix ? new Affix(form, grammemes) : new Particle(form, grammemes);
                }
            }
            return new Article(position, cells);
        }
        Article FromDemonstrative()
        {
            var distal = demonstratives.Distances[^1];
            var cells = new Dictionary<(Gender?, Number?), Morpheme>();
            foreach (var gender in GenderValues())
            {
                foreach (var number in NumberValues(Definiteness.Definite))
                {
                    var form = demonstratives.For(distal, gender ?? genders.Values.FirstOrDefault(), number ?? Number.Singular).Form;
                    List<Grammeme> grammemes = [Definiteness.Definite, .. new Grammeme?[] { gender, number }.OfType<Grammeme>()];
                    cells[(gender, number)] = demonstratives.IsAffix ? new Affix(form, grammemes) : new Particle(form, grammemes);
                }
            }
            return new Article(demonstratives.Position, cells);
        }

        var definite = definiteType switch
        {
            DefiniteArticle.Word => Marker(false, Definiteness.Definite),
            DefiniteArticle.Demonstrative => FromDemonstrative(),
            DefiniteArticle.Affix => Marker(true, Definiteness.Definite),
            _ => null,
        };
        var indefinite = indefiniteType switch
        {
            IndefiniteArticle.Word => Marker(false, Definiteness.Indefinite),
            IndefiniteArticle.Affix => Marker(true, Definiteness.Indefinite),
            IndefiniteArticle.One => new Article(numerals.BeforeNoun ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle,
                new Dictionary<(Gender?, Number?), Morpheme> { [(null, null)] = numerals.Words[1].Root }),
            _ => null,
        };
        //drawn last, so the forms above are what the seed drew before there were demonstratives
        var withDemonstrative = rand.NextDouble() < ArticleWithDemonstrativeChance && definiteType is DefiniteArticle.Word or DefiniteArticle.Affix;
        return new Articles(definite, indefinite, definiteType == DefiniteArticle.Demonstrative, withDemonstrative);
    }
}
