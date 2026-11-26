namespace LanguageGenerator;

//the perfect, the progressive, the habitual, and beginning and ceasing
public static partial class Generator
{
    /*
       https://wals.info/feature/68A: of 222 languages, 114 have no perfect, 7 make one from a
      possessive construction, as English 'have' does, 21 from 'finish' or 'already', and 80 some other
      way. WALS doesn't say how those 80 mark it, so the split of them between an affix, a particle
      and the copula, as French est venu has it, is a guess. The 21 are split between an auxiliary
      'finish' and a particle, as 'already' is one, and as Mandarin le, from 'finish', became one
     */
    private static readonly (AspectKind Value, int Languages)[] PerfectCounts =
    [
        (AspectKind.None, 114),
        (AspectKind.Have, 7),
        (AspectKind.Finish, 11),
        (AspectKind.Particle, 10 + 20),
        (AspectKind.Affix, 35),
        (AspectKind.Be, 25),
    ];

    /*
      WALS has no chapter on the progressive, so these are guesses after Bybee, Perkins and Pagliuca,
      who find progressives mostly periphrastic and mostly from a construction saying where someone
      is, 'be at doing', as Spanish estar and Mandarin zài are, then from verbs of posture, 'sit' or
      'stand', as Dutch zitten is. A language without one says it as an imperfective, as Russian does
     */
    private static readonly (AspectKind Value, int Languages)[] ProgressiveCounts =
    [
        (AspectKind.None, 45),
        (AspectKind.Be, 25),
        (AspectKind.Affix, 15),
        (AspectKind.Particle, 10),
        (AspectKind.Sit, 5),
    ];

    /*
      WALS has no chapter on the habitual either, so these are guesses after Bybee, Perkins and
      Pagliuca and Dahl, who find habituals more often inflectional than progressives, as Turkish
      -Ir and the Bantu habitual prefixes are, and some periphrastic with the copula, as Hindi jātā
      hai 'goes, habitually' and Irish bíonn are. Most languages have none, and say a habit as an
      imperfective, as WALS 65A finds theirs covering habituals
     */
    private static readonly (AspectKind Value, int Languages)[] HabitualCounts =
    [
        (AspectKind.None, 60),
        (AspectKind.Affix, 20),
        (AspectKind.Particle, 12),
        (AspectKind.Be, 8),
    ];

    /*
      nobody counts inceptives or cessatives on the verb, so these are guesses too: most languages
      say beginning and ceasing with verbs of their own, as English does, and some mark them on the
      verb, as Russian za-pel 'started singing' does with an inceptive. That a cessative on the verb
      is the rarer of the two is a guess as well
     */
    private static readonly (AspectKind Value, int Languages)[] InceptiveCounts =
    [
        (AspectKind.None, 80),
        (AspectKind.Affix, 12),
        (AspectKind.Particle, 8),
    ];

    private static readonly (AspectKind Value, int Languages)[] CessativeCounts =
    [
        (AspectKind.None, 88),
        (AspectKind.Affix, 7),
        (AspectKind.Particle, 5),
    ];

    /*
      an affix goes on the side the language puts its affixes, and an isolating language uses a
      particle in its place. A particle or an auxiliary goes before the verb in a VO language and
      after it in an OV one, as auxiliaries do per Dryer. The markers' forms are kept apart, so
      that a perfect and a progressive that are both particles don't sound alike
     */
    public static Aspects GenerateAspects(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology)
    {
        var before = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial;
        var forms = new DistinctForms();
        //the marking of an aspect or phase, glossed as it
        AspectMarking? Marker((AspectKind Value, int Languages)[] counts, Grammeme meaning)
        {
            var kind = Sampling.Categorical(rand, counts);
            if (kind == AspectKind.Affix && morphology.Fusion == Fusion.Isolating)
            {
                kind = AspectKind.Particle;
            }
            return kind switch
            {
                AspectKind.None => null,
                AspectKind.Affix => new Bound(CoinAffix(rand, phonology, forms.Draw(() => MarkerForm(rand, true, phonology)), [meaning]),
                    rand.NextDouble() < PrefixChance(morphology) ? Side.Before : Side.After),
                AspectKind.Particle => new Free(new Particle(forms.Draw(() => MarkerForm(rand, false, phonology)), [meaning]), before ? Side.Before : Side.After),
                AspectKind.Have or AspectKind.Be or AspectKind.Finish or AspectKind.Sit => AspectAuxiliary.Of(Aspects.VerbOf(kind)!.Value, before),
                _ => throw new ArgumentOutOfRangeException(nameof(counts), kind, null),
            };
        }
        var perfect = Marker(PerfectCounts, Aspect.Perfect);
        var progressive = Marker(ProgressiveCounts, Aspect.Progressive);
        var habitual = Marker(HabitualCounts, Aspect.Habitual);
        var inceptive = Marker(InceptiveCounts, Phase.Inceptive);
        return new Aspects(perfect, progressive, habitual, inceptive, Marker(CessativeCounts, Phase.Cessative));
    }
}
