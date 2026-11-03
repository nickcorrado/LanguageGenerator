namespace LanguageGenerator;

//copulas, and how adjectives predicate and agree
public static partial class Generator
{
    //guesses, with no WALS chapter to go on. Languages whose adjectives are verbs, like Mandarin,
    //Japanese and Korean, don't make them agree with nouns the way Latin or Russian do
    private static readonly (AdjectiveAgreement Value, int Weight)[] AdjectiveAgreementWeights =
        [(AdjectiveAgreement.None, 40), (AdjectiveAgreement.Agreement, 40), (AdjectiveAgreement.Phrasal, 20)];
    private static readonly (AdjectiveAgreement Value, int Weight)[] VerbalAdjectiveAgreementWeights =
        [(AdjectiveAgreement.None, 65), (AdjectiveAgreement.Phrasal, 35)];

    public static AdjectiveAgreement SelectAdjectiveAgreement(Random rand, Predication predication) =>
        Sampling.Categorical(rand, predication.Adjectives == PredicativeAdjectives.Verbal ? VerbalAdjectiveAgreementWeights : AdjectiveAgreementWeights);

    //WALS 118A and 120A, drawn independently. The copula is one syllable, as frequent words are
    private static readonly (PredicativeAdjectives Value, int Languages)[] PredicativeAdjectiveCounts =
        [(PredicativeAdjectives.Verbal, 151), (PredicativeAdjectives.Nonverbal, 132)];
    private static readonly (bool Value, int Languages)[] ZeroCopulaCounts = [(false, 211), (true, 175)];

    public static Predication GeneratePredication(Random rand, Phonology phonology, double dropoff) => new(
        Sampling.Categorical(rand, PredicativeAdjectiveCounts),
        Sampling.Categorical(rand, ZeroCopulaCounts),
        new Lexeme(new Root(GenerateForm(rand, phonology.SyllableRule, dropoff, 0, maxSyllables: 1), "COP"), PartOfSpeech.Verb));

    //WALS 119A
    private static readonly (bool Value, int Languages)[] LocationalCopulaCounts = [(true, 269), (false, 117)];

    //a one-syllable locational copula of its own, redrawn a few times to keep it from sounding like
    //the copula, in WALS's share of languages
    public static Predication GenerateLocationalPredication(Random rand, Predication predication, Phonology phonology, double dropoff)
    {
        if (!Sampling.Categorical(rand, LocationalCopulaCounts))
        {
            return predication;
        }
        var forms = new DistinctForms(draws: 11);
        forms.Take(predication.Copula.Root.Form);
        var form = forms.Draw(() => GenerateForm(rand, phonology.SyllableRule, dropoff, 0, maxSyllables: 1));
        return predication with { LocationalCopula = new Lexeme(new Root(form, "be.at"), PartOfSpeech.Verb) };
    }
}
