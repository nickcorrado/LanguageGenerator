namespace LanguageGenerator;

//relative clauses
public static partial class Generator
{
    /*
      WALS 96A, Dryer's order of relative clause and noun beside the order of object and verb: in OV
      languages the relative clause precedes the noun in 132 and follows it in 113, and in VO ones it
      precedes it in 5, all in mainland China and Taiwan, and follows it in 416. The other types of
      relative clause, internally headed, correlative and adjoined, aren't made here
     */
    private static double BeforeNounChance(bool objectFirst) => objectFirst ? 132.0 / (132 + 113) : 5.0 / (5 + 416);

    /*
      WALS 122A, Comrie and Kuteva's relativization on subjects: a gap in 125 languages, a relative
      pronoun in 12, nearly all in Europe, and a resumptive pronoun in 5. The 24 that keep the whole
      noun in the relative clause, as internally headed and correlative ones do, are left out
     */
    private static readonly (RelativeStrategy Value, int Languages)[] SubjectStrategies =
        [(RelativeStrategy.Gap, 125), (RelativeStrategy.Pronoun, 12), (RelativeStrategy.Resumptive, 5)];

    /*
      WALS 123A counts obliques: a gap in 55 languages, a relative pronoun in 13 and a resumptive
      pronoun in 20, leaving out the 14 that keep the whole noun and the 10 that can't relativize an
      oblique at all. It doesn't cross them with subjects, so they're drawn given the subject's to
      keep those totals and the Accessibility Hierarchy, which Keenan and Comrie find has languages
      resume the roles low on it before the high ones: a language that resumes its subjects resumes
      its obliques too, and one with relative pronouns for subjects nearly always has them for
      obliques, as Europe's languages do. The odds given a gap are what's left to meet WALS's totals
     */
    private static (RelativeStrategy Value, int Weight)[] ObliqueStrategies(RelativeStrategy subject) => subject switch
    {
        RelativeStrategy.Gap => [(RelativeStrategy.Gap, 70), (RelativeStrategy.Resumptive, 22), (RelativeStrategy.Pronoun, 8)],
        RelativeStrategy.Pronoun => [(RelativeStrategy.Pronoun, 90), (RelativeStrategy.Resumptive, 5), (RelativeStrategy.Gap, 5)],
        RelativeStrategy.Resumptive => [(RelativeStrategy.Resumptive, 1)],
        _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, null),
    };

    //WALS counts no direct objects, which lie between subjects and obliques on the hierarchy, so
    //an object is relativized as the subject is, or as obliques are, as a guess has it
    private const double ObjectLikeSubjectChance = 0.7;

    /*
      how a relative clause is marked. Nobody counts it, so these are guesses after Lehmann and
      Keenan: a relative clause before its noun is mostly a participle, as in Turkish and Korean, or
      marked by a word at its end, as Mandarin de is, and one after its noun mostly by a word at its
      start, as English that and Arabic illi are, or by nothing. A relative pronoun needs no other
      marker, so one is rarely there beside it
     */
    private static (RelativizerKind Value, int Weight)[] RelativizerOdds(bool beforeNoun, RelativeStrategy subject) => (beforeNoun, subject) switch
    {
        (_, RelativeStrategy.Pronoun) => [(RelativizerKind.None, 90), (RelativizerKind.Particle, 10)],
        (true, _) => [(RelativizerKind.Affix, 50), (RelativizerKind.Particle, 30), (RelativizerKind.None, 20)],
        (false, _) => [(RelativizerKind.Particle, 50), (RelativizerKind.None, 35), (RelativizerKind.Affix, 15)],
    };

    //how often a relative pronoun is the language's question words, as Heine and Kuteva find
    //question words so often becoming relative pronouns, rather than a word of its own. A guess
    private const double PronounFromQuestionWordsChance = 0.6;

    /*
      where relative clauses go, how each role a relative clause's noun fills is said, and what
      marks a relative clause, with a relativizer and a relative pronoun of its own coined whatever
      is drawn. An isolating language uses a word where another would use an affix
     */
    public static Relativization GenerateRelativization(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology)
    {
        var beforeNoun = rand.NextDouble() < BeforeNounChance(wordOrder.VerbPhraseDirectionality == HeadDirectionality.Final);
        var subject = Sampling.Categorical(rand, SubjectStrategies);
        var oblique = Sampling.Categorical(rand, ObliqueStrategies(subject));
        var @object = rand.NextDouble() < ObjectLikeSubjectChance ? subject : oblique;
        var kind = Sampling.Categorical(rand, RelativizerOdds(beforeNoun, subject));
        if (kind == RelativizerKind.Affix && morphology.Fusion == Fusion.Isolating)
        {
            kind = RelativizerKind.Particle;
        }
        var forms = new DistinctForms();
        var relativizer = new Relativizer(kind,
            forms.Draw(() => kind == RelativizerKind.Affix ? MarkerForm(rand, true, phonology) : [.. GenerateSyllable(rand, phonology.SyllableRule, phonology.Dropoff, needsOnset: false).Phonemes]),
            rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix);
        var fromQuestionWords = rand.NextDouble() < PronounFromQuestionWordsChance;
        var own = new Lexeme(CoinRoot(rand, phonology, forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 1)), "REL"), PartOfSpeech.Noun);
        return new Relativization(beforeNoun, subject, @object, oblique, relativizer, new RelativePronoun(fromQuestionWords, own));
    }
}
