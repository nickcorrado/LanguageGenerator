namespace LanguageGenerator;

//complement clauses and quotations
public static partial class Generator
{
    /*
      WALS 124A, Haspelmath's 'want' complement subjects: left implicit in 144 languages, expressed
      overtly in 72, and 'want' a desiderative affix in 45 and a particle in 8. The 14 with both
      constructions are left out, since a language here has one
     */
    private enum WantComplement
    {
        Implicit,
        Overt,
        Affix,
        Particle,
    }

    private static readonly (WantComplement Value, int Languages)[] WantCounts =
        [(WantComplement.Implicit, 144), (WantComplement.Overt, 72), (WantComplement.Affix, 45), (WantComplement.Particle, 8)];

    /*
      where a complementizer goes, by the order of verb and object. WALS doesn't count complementizers,
      so these are guesses after Dryer, who finds a word at the start of the clause in nearly every VO
      language, and in OV languages a word at either end or a suffix, as Japanese to, Hindi ki and
      Korean -ko, with plenty of languages using none, as Mandarin uses none. An event's complement
      goes unmarked more often, as serial verbs and bare stems are, and is a suffix more often in OV
      languages, as Turkish git-mek is
     */
    private static (ComplementizerPosition Value, int Weight)[] ComplementizerOdds(ComplementKind kind, bool objectFirst) => (kind, objectFirst) switch
    {
        (ComplementKind.Proposition, false) => [(ComplementizerPosition.Initial, 60), (ComplementizerPosition.None, 30), (ComplementizerPosition.Final, 5), (ComplementizerPosition.Suffix, 5)],
        (ComplementKind.Proposition, true) => [(ComplementizerPosition.Initial, 20), (ComplementizerPosition.None, 25), (ComplementizerPosition.Final, 30), (ComplementizerPosition.Suffix, 25)],
        (ComplementKind.Event, false) => [(ComplementizerPosition.Initial, 45), (ComplementizerPosition.None, 45), (ComplementizerPosition.Final, 5), (ComplementizerPosition.Suffix, 5)],
        (ComplementKind.Event, true) => [(ComplementizerPosition.Initial, 10), (ComplementizerPosition.None, 40), (ComplementizerPosition.Final, 20), (ComplementizerPosition.Suffix, 30)],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /*
      Cristofaro finds the complements of verbs of saying, knowing and thinking deranked far less often
      than those of wanting, and never deranked where wanting's aren't, but WALS doesn't count them,
      so the odds of a proposition's are a guess. An event's follow WALS 124A: an implicit subject is
      deranked, always or where it's shared, an overt one balanced, as Greek thélo na páo 'I want
      that I go' is
     */
    private static readonly (Balance Value, int Weight)[] PropositionBalance = [(Balance.Balanced, 75), (Balance.Deranked, 15), (Balance.Either, 10)];
    private const double ImplicitAlwaysDeranked = 0.7;

    /*
      the role a deranked complement is marked like, if any: an object's case, as Turkish puts a
      nominalized clause in the accusative, or for an event a goal's marker, as English to go and
      Spanish a are, or nothing, as a bare infinitive or stem has. Guesses
     */
    private static (Role? Role, int Weight)[] MarkedAsOdds(ComplementKind kind) => kind switch
    {
        ComplementKind.Proposition => [(Role.DirectObject, 60), (null, 40)],
        ComplementKind.Event => [(Role.DirectObject, 25), (Role.Goal, 25), (null, 50)],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /*
      how often a complement that keeps its verb goes last in its clause. A heavy clause goes last in
      VO languages, where the object already follows the verb. In OV languages it goes last mostly
      where its complementizer starts it, as Hindi ki and Persian ke clauses follow the verb, Dryer
      and Hawkins find, since a word at the start of a clause before the verb would part the verb from
      the rest; where it's marked at its end it stays before the verb, as in Japanese. Guesses
     */
    private static double LastChance(bool objectFirst, ComplementizerPosition proposition) => !objectFirst ? 0.9 : proposition switch
    {
        ComplementizerPosition.Initial => 0.9,
        ComplementizerPosition.None => 0.4,
        _ => 0.1,
    };

    /*
      how quotations are marked. Güldemann finds quotative markers all over Africa and Asia, many of
      them from words for 'say' that became complementizers too, and Europe's languages mostly mark
      none, but nobody counts them, so the odds are guesses. A quotative word goes before the quote in
      a VO language, as Ewe bé does, and after it in an OV one, as Turkish diye does
     */
    private static (QuotativeKind Value, int Weight)[] QuotativeOdds(bool objectFirst) => objectFirst
        ? [(QuotativeKind.None, 30), (QuotativeKind.Complementizer, 30), (QuotativeKind.Word, 40)]
        : [(QuotativeKind.None, 50), (QuotativeKind.Complementizer, 25), (QuotativeKind.Word, 25)];
    private static double QuotativeBeforeChance(bool objectFirst) => objectFirst ? 0.15 : 0.8;

    //how often an event's complementizer is a proposition's too, as Romance que and Arabic ʔan go
    //with both where English has that and to. A guess
    private const double SharedComplementizerChance = 0.3;

    /*
      a complementizer for each kind of complement, coined even where it goes unsaid, and how a
      quotation and 'want' are marked. An isolating language puts a word at the clause's end where
      another would use a suffix, and a particle for 'want' where another would use an affix
     */
    public static Complementation GenerateComplementation(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology)
    {
        var objectFirst = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Final;
        var isolating = morphology.Fusion == Fusion.Isolating;
        var forms = new DistinctForms();
        List<Phoneme> Form(bool affix) =>
            forms.Draw(() => affix ? MarkerForm(rand, true, phonology) : [.. GenerateSyllable(rand, phonology.SyllableRule, phonology.Dropoff, needsOnset: false, codaCluster: false).Phonemes]);
        ComplementizerPosition Position(ComplementKind kind) => Sampling.Categorical(rand, ComplementizerOdds(kind, objectFirst)) is var position
            && position == ComplementizerPosition.Suffix && isolating ? ComplementizerPosition.Final : position;

        var want = Sampling.Categorical(rand, WantCounts);
        //'want' as an affix or particle says nothing of other events, which take one of the verb's types
        var wantVerb = want is WantComplement.Implicit or WantComplement.Overt ? want
            : Sampling.Categorical(rand, [(WantComplement.Implicit, 144), (WantComplement.Overt, 72)]);
        var eventBalance = wantVerb == WantComplement.Overt ? Balance.Balanced
            : rand.NextDouble() < ImplicitAlwaysDeranked ? Balance.Deranked : Balance.Either;

        var propositionPosition = Position(ComplementKind.Proposition);
        var propositionForm = Form(propositionPosition == ComplementizerPosition.Suffix);
        var propositionAccent = CoinAffixAccent(rand, phonology, propositionForm);
        var proposition = new ComplementMarking(ComplementizerMarker(propositionForm, propositionPosition, propositionAccent), propositionPosition,
            Sampling.Categorical(rand, PropositionBalance), Sampling.Categorical(rand, MarkedAsOdds(ComplementKind.Proposition)));
        var eventPosition = Position(ComplementKind.Event);
        var eventForm = rand.NextDouble() < SharedComplementizerChance && (eventPosition == ComplementizerPosition.Suffix) == (propositionPosition == ComplementizerPosition.Suffix)
            ? propositionForm
            : Form(eventPosition == ComplementizerPosition.Suffix);
        //a complementizer the two share is one morpheme, accent and all
        var eventAccent = ReferenceEquals(eventForm, propositionForm) ? propositionAccent : CoinAffixAccent(rand, phonology, eventForm);
        var @event = new ComplementMarking(ComplementizerMarker(eventForm, eventPosition, eventAccent), eventPosition, eventBalance, Sampling.Categorical(rand, MarkedAsOdds(ComplementKind.Event)));

        var quotativeKind = Sampling.Categorical(rand, QuotativeOdds(objectFirst));
        if (quotativeKind == QuotativeKind.Complementizer && propositionPosition == ComplementizerPosition.None)
        {
            quotativeKind = rand.NextDouble() < 0.5 ? QuotativeKind.Word : QuotativeKind.None;
        }
        var quotative = new Quotative(quotativeKind, new Particle(Form(false), [Linkage.Quotative]), rand.NextDouble() < QuotativeBeforeChance(objectFirst));

        var last = rand.NextDouble() < LastChance(objectFirst, propositionPosition);
        var before = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial;
        var wantKind = (want, isolating) switch
        {
            (WantComplement.Affix, false) => DesiderativeKind.Affix,
            (WantComplement.Affix or WantComplement.Particle, _) => DesiderativeKind.Particle,
            _ => DesiderativeKind.Verb,
        };
        var wantForm = Form(wantKind == DesiderativeKind.Affix);
        var desiderative = new Desiderative(wantKind, wantForm, CoinAffixAccent(rand, phonology, wantForm),
            rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix, before);
        return new Complementation(proposition, @event, quotative, last, desiderative);
    }

    //a complementizer's morpheme: an affix with this accent if it's a suffix, and otherwise a word, said or not
    public static Morpheme ComplementizerMarker(IReadOnlyList<Phoneme> form, ComplementizerPosition position, AffixAccent accent) => position == ComplementizerPosition.Suffix
        ? new Affix(form, [Linkage.Complementizer], accent)
        : new Particle(form, [Linkage.Complementizer]);
}
