namespace LanguageGenerator;

//modals
public static partial class Generator
{
    /*
      WALS 74A: situational possibility by an affix on the verb in 63 languages, a verb in 158, and
      something else in 13, mostly particles but also adverbs, adjectives and nouns, all of which are
      made a particle here. WALS doesn't count necessity, so it's drawn the same way
     */
    private static readonly (ModalKind Value, int Languages)[] ModalCounts =
        [(ModalKind.Affix, 63), (ModalKind.Verb, 158), (ModalKind.Particle, 13)];

    /*
      WALS 75A: epistemic possibility by a verb in 65 languages, an affix or clitic on the verb in
      84, and something else in 91, mostly adverbs and particles like English maybe, made a particle
      here. WALS doesn't count epistemic necessity, so it's drawn the same way. These are for an
      epistemic modal with a marker of its own; one sharing a situational modal's has that one's kind
     */
    private static readonly (ModalKind Value, int Languages)[] EpistemicCounts =
        [(ModalKind.Affix, 84), (ModalKind.Verb, 65), (ModalKind.Particle, 91)];

    /*
      WALS 76A: of 207 languages, 36 use their situational markers for both epistemic possibility
      and necessity, as English may and must do, 66 for one of them, as Hungarian lehet is 'can' and
      'may be' but its 'must' isn't epistemic, and 105 for neither, as Evenki's epistemic suffixes
      are its own. WALS doesn't say which of the two the 66 share, so they're split evenly
     */
    private static readonly ((bool Possibility, bool Necessity) Value, int Languages)[] EpistemicOverlapCounts =
        [((true, true), 36), ((true, false), 33), ((false, true), 33), ((false, false), 105)];

    /*
      a marker for each region of the modality map, drawn on its own, since languages often mark
      possibility and necessity differently, as Japanese has the suffix -e- for 'can' and a
      construction for 'must'. A modal verb is short, as frequent verbs are, and kept from sounding
      like the copulas or the other modals. A modal verb or particle goes before the verb in a VO
      language and after it in an OV one, as auxiliaries do per Dryer; an affix goes on the side the
      language puts its affixes, and an isolating language uses a particle in its place
     */
    public static Modals GenerateModals(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Predication predication, Phonology phonology)
    {
        var before = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial;
        var forms = new DistinctForms();
        forms.Take(predication.Copula.Root.Form);
        if (predication.LocationalCopula is { } locational)
        {
            forms.Take(locational.Root.Form);
        }

        var markers = new Dictionary<Modality, ModalMarking>();
        //the situational edges each on their own, and the epistemic ones together, as WALS 76A counts them
        var joined = SemanticMap.Joined(rand, ModalityMap.Edges.Where(e => !e.Second.Epistemic));
        var (possibility, necessity) = Sampling.Categorical(rand, EpistemicOverlapCounts);
        if (possibility)
        {
            joined.Add((Modality.Permission, Modality.Dubitative));
        }
        if (necessity)
        {
            joined.Add((Modality.Obligation, Modality.Presumptive));
        }
        foreach (var region in SemanticMap.Regions(ModalityMap.Modalities, joined))
        {
            var kind = Sampling.Categorical(rand, region.All(m => m.Epistemic) ? EpistemicCounts : ModalCounts);
            if (kind == ModalKind.Affix && morphology.Fusion == Fusion.Isolating)
            {
                kind = ModalKind.Particle;
            }
            var prefix = rand.NextDouble() < PrefixChance(morphology);
            var form = forms.Draw(() => kind switch
            {
                ModalKind.Affix => MarkerForm(rand, true, phonology),
                ModalKind.Verb => GenerateForm(rand, phonology, 0, maxSyllables: 1),
                _ => MarkerForm(rand, false, phonology),
            });
            foreach (var modality in region)
            {
                markers[modality] = kind switch
                {
                    ModalKind.Affix => new ModalAffix(CoinAffix(rand, phonology, form, [modality]), prefix ? ExponentPosition.Prefix : ExponentPosition.Suffix),
                    ModalKind.Verb => new ModalVerb(new Lexeme(CoinRoot(rand, phonology, form, ModalityMap.VerbGloss(modality)), PartOfSpeech.Verb), before),
                    _ => new ModalParticle(new Particle(form, [modality]), before),
                };
            }
        }
        return new Modals(joined, markers);
    }
}
