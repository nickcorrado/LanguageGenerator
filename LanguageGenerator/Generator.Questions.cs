namespace LanguageGenerator;

//questions
public static partial class Generator
{
    private enum PolarKind
    {
        Particle,
        Affix,
        Inversion,
        Intonation,
    }

    // https://wals.info/feature/116A: a question particle in 585 languages, interrogative verb
    //morphology in 164, a mixture of the two in 15, which are made a particle here, interrogative
    //word order in 13, and intonation alone in 173. The 4 that mark a question by leaving out a
    //statement's morpheme and the 1 that doesn't tell them apart are made intonation
    private static readonly (PolarKind Value, int Languages)[] PolarCounts =
        [(PolarKind.Particle, 600), (PolarKind.Affix, 164), (PolarKind.Inversion, 13), (PolarKind.Intonation, 178)];

    // https://wals.info/combinations/83A_92A: where a question particle goes, in OV and VO languages.
    //The 8 in some other position and the 18 in either of two are left out
    private static readonly (QuestionParticlePosition Value, int Languages)[] OVParticleCounts =
        [(QuestionParticlePosition.Initial, 37), (QuestionParticlePosition.Final, 140), (QuestionParticlePosition.Second, 23)];
    private static readonly (QuestionParticlePosition Value, int Languages)[] VOParticleCounts =
        [(QuestionParticlePosition.Initial, 81), (QuestionParticlePosition.Final, 154), (QuestionParticlePosition.Second, 21)];

    // https://wals.info/combinations/81A_93A: how many languages of each order put question words
    //first, and how many leave them in place. The 23 that do either are left out
    private static readonly Dictionary<ConstituentWordOrder, (int Initial, int InPlace)> FrontingCounts = new()
    {
        [ConstituentWordOrder.SOV] = (58, 266),
        [ConstituentWordOrder.SVO] = (72, 218),
        [ConstituentWordOrder.VSO] = (46, 19),
        [ConstituentWordOrder.VOS] = (10, 3),
        [ConstituentWordOrder.OVS] = (4, 3),
        [ConstituentWordOrder.OSV] = (0, 2),
    };

    //an isolating language uses a particle where others would use an affix. Question words are
    //short, as frequent words are, and kept from sounding like each other
    public static Questions GenerateQuestions(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var kind = Sampling.Categorical(rand, PolarCounts);
        if (kind == PolarKind.Affix && morphology.Fusion == Fusion.Isolating)
        {
            kind = PolarKind.Particle;
        }
        PolarQuestionMarking polar = kind switch
        {
            PolarKind.Particle => new QuestionParticle(new Particle(MarkerForm(rand, false, phonology, dropoff), [Mood.Interrogative]),
                Sampling.Categorical(rand, wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial ? VOParticleCounts : OVParticleCounts)),
            PolarKind.Affix => new QuestionAffix(new Affix(MarkerForm(rand, true, phonology, dropoff), [Mood.Interrogative]),
                rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix),
            PolarKind.Inversion => new QuestionInversion(),
            _ => new QuestionIntonation(),
        };

        var (initial, inPlace) = FrontingCounts[wordOrder.ConstituentWordOrder];
        var fronts = rand.Next(initial + inPlace) < initial;

        var forms = new DistinctForms();
        Lexeme Word(string meaning) => new(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0.5, maxSyllables: 2)), meaning), PartOfSpeech.Noun);
        var words = Enum.GetValues<Interrogative>().ToDictionary(w => w, w => Word(w.ToString().ToLowerInvariant()));
        return new Questions(polar, fronts, words, Word("why"));
    }
}
