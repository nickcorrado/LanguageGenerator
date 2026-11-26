namespace LanguageGenerator;

//the perfect, the progressive, the habitual, and beginning and ceasing
public static partial class Generator
{
    //what an aspect's marker is drawn from: the language's profile and word order, and the forms its other markers took
    private sealed record AspectCoining(Random Rand, Phonology Phonology, MorphologicalProfile Morphology, DistinctForms Forms, bool BeforeVerb);

    //one way an aspect can be marked, as the odds below name it, coining its marker, glossed as the aspect
    private delegate AspectMarking? AspectMarker(AspectCoining coining, Grammeme meaning);

    private static AspectMarking? Unmarked(AspectCoining coining, Grammeme meaning) => null;

    //an isolating language uses a particle in an affix's place
    private static AspectMarking? AspectAffix(AspectCoining c, Grammeme meaning) => c.Morphology.Fusion == Fusion.Isolating
        ? AspectParticle(c, meaning)
        : new Bound(CoinAffix(c.Rand, c.Phonology, c.Forms.Draw(() => MarkerForm(c.Rand, true, c.Phonology)), [meaning]), c.Rand.NextDouble() < PrefixChance(c.Morphology) ? Side.Before : Side.After);

    private static AspectMarking? AspectParticle(AspectCoining c, Grammeme meaning) =>
        new Free(new Particle(c.Forms.Draw(() => MarkerForm(c.Rand, false, c.Phonology)), [meaning]), c.BeforeVerb ? Side.Before : Side.After);

    private static AspectMarker AspectAuxiliaryOf(AspectVerb verb) => (c, _) => AspectAuxiliary.Of(verb, c.BeforeVerb);

    /*
       https://wals.info/feature/68A: of 222 languages, 114 have no perfect, 7 make one from a
      possessive construction, as English 'have' does, 21 from 'finish' or 'already', and 80 some other
      way. WALS doesn't say how those 80 mark it, so the split of them between an affix, a particle
      and the copula, as French est venu has it, is a guess. The 21 are split between an auxiliary
      'finish' and a particle, as 'already' is one, and as Mandarin le, from 'finish', became one
     */
    private static readonly (AspectMarker Value, int Languages)[] PerfectCounts =
    [
        (Unmarked, 114),
        (AspectAuxiliaryOf(AspectVerb.Have), 7),
        (AspectAuxiliaryOf(AspectVerb.Finish), 11),
        (AspectParticle, 10 + 20),
        (AspectAffix, 35),
        (AspectAuxiliaryOf(AspectVerb.Be), 25),
    ];

    /*
      WALS has no chapter on the progressive, so these are guesses after Bybee, Perkins and Pagliuca,
      who find progressives mostly periphrastic and mostly from a construction saying where someone
      is, 'be at doing', as Spanish estar and Mandarin zài are, then from verbs of posture, 'sit' or
      'stand', as Dutch zitten is. A language without one says it as an imperfective, as Russian does
     */
    private static readonly (AspectMarker Value, int Languages)[] ProgressiveCounts =
    [
        (Unmarked, 45),
        (AspectAuxiliaryOf(AspectVerb.Be), 25),
        (AspectAffix, 15),
        (AspectParticle, 10),
        (AspectAuxiliaryOf(AspectVerb.Sit), 5),
    ];

    /*
      WALS has no chapter on the habitual either, so these are guesses after Bybee, Perkins and
      Pagliuca and Dahl, who find habituals more often inflectional than progressives, as Turkish
      -Ir and the Bantu habitual prefixes are, and some periphrastic with the copula, as Hindi jātā
      hai 'goes, habitually' and Irish bíonn are. Most languages have none, and say a habit as an
      imperfective, as WALS 65A finds theirs covering habituals
     */
    private static readonly (AspectMarker Value, int Languages)[] HabitualCounts =
    [
        (Unmarked, 60),
        (AspectAffix, 20),
        (AspectParticle, 12),
        (AspectAuxiliaryOf(AspectVerb.Be), 8),
    ];

    /*
      nobody counts inceptives or cessatives on the verb, so these are guesses too: most languages
      say beginning and ceasing with verbs of their own, as English does, and some mark them on the
      verb, as Russian za-pel 'started singing' does with an inceptive. That a cessative on the verb
      is the rarer of the two is a guess as well
     */
    private static readonly (AspectMarker Value, int Languages)[] InceptiveCounts =
    [
        (Unmarked, 80),
        (AspectAffix, 12),
        (AspectParticle, 8),
    ];

    private static readonly (AspectMarker Value, int Languages)[] CessativeCounts =
    [
        (Unmarked, 88),
        (AspectAffix, 7),
        (AspectParticle, 5),
    ];

    /*
      an affix goes on the side the language puts its affixes, and an isolating language uses a
      particle in its place. A particle or an auxiliary goes before the verb in a VO language and
      after it in an OV one, as auxiliaries do per Dryer. The markers' forms are kept apart, so
      that a perfect and a progressive that are both particles don't sound alike
     */
    public static Aspects GenerateAspects(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology)
    {
        var coining = new AspectCoining(rand, phonology, morphology, new DistinctForms(), wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial);
        AspectMarking? Marker((AspectMarker Value, int Languages)[] counts, Grammeme meaning) => Sampling.Categorical(rand, counts)(coining, meaning);
        var perfect = Marker(PerfectCounts, Aspect.Perfect);
        var progressive = Marker(ProgressiveCounts, Aspect.Progressive);
        var habitual = Marker(HabitualCounts, Aspect.Habitual);
        var inceptive = Marker(InceptiveCounts, Phase.Inceptive);
        return new Aspects(perfect, progressive, habitual, inceptive, Marker(CessativeCounts, Phase.Cessative));
    }
}
