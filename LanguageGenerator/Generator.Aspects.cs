namespace LanguageGenerator;

//the perfect and the progressive
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
      an affix goes on the side the language puts its affixes, and an isolating language uses a
      particle in its place. A particle or an auxiliary goes before the verb in a VO language and
      after it in an OV one, as auxiliaries do per Dryer. The two markers' forms are kept apart, so
      that a perfect and a progressive that are both particles don't sound alike
     */
    public static Aspects GenerateAspects(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology)
    {
        var before = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial;
        var forms = new DistinctForms();
        AspectMarker Marker((AspectKind Value, int Languages)[] counts)
        {
            var kind = Sampling.Categorical(rand, counts);
            if (kind == AspectKind.Affix && morphology.Fusion == Fusion.Isolating)
            {
                kind = AspectKind.Particle;
            }
            var form = forms.Draw(() => MarkerForm(rand, kind == AspectKind.Affix, phonology));
            return new AspectMarker(kind, form, rand.NextDouble() < PrefixChance(morphology) ? ExponentPosition.Prefix : ExponentPosition.Suffix, before);
        }
        var perfect = Marker(PerfectCounts);
        return new Aspects(perfect, Marker(ProgressiveCounts));
    }
}
