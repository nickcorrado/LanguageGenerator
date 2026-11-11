namespace LanguageGenerator;

//modals
public static partial class Generator
{
    //WALS 74A: situational possibility by an affix on the verb in 63 languages, a verb in 158, and
    //something else in 13, mostly particles but also adverbs, adjectives and nouns, all of which are
    //made a particle here. WALS doesn't count necessity, so it's drawn the same way
    private enum ModalKind
    {
        Affix,
        Verb,
        Particle,
    }

    private static readonly (ModalKind Value, int Languages)[] ModalCounts =
        [(ModalKind.Affix, 63), (ModalKind.Verb, 158), (ModalKind.Particle, 13)];

    //a marker for each region of the modality map, drawn on its own, since languages often mark
    //possibility and necessity differently, as Japanese has the suffix -e- for 'can' and a
    //construction for 'must'. A modal verb is short, as frequent verbs are, and kept from sounding
    //like the copulas or the other modals. A modal verb or particle goes before the verb in a VO
    //language and after it in an OV one, as auxiliaries do per Dryer; an affix goes on the side the
    //language puts its affixes, and an isolating language uses a particle in its place
    public static Modals GenerateModals(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Predication predication, Phonology phonology, double dropoff)
    {
        var before = wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial;
        var forms = new DistinctForms();
        forms.Take(predication.Copula.Root.Form);
        if (predication.LocationalCopula is { } locational)
        {
            forms.Take(locational.Root.Form);
        }

        var markers = new Dictionary<Modality, ModalMarking>();
        foreach (var region in SemanticMap.Regions(ModalityMap.Modalities, SemanticMap.Joined(rand, ModalityMap.Edges)))
        {
            var kind = Sampling.Categorical(rand, ModalCounts);
            if (kind == ModalKind.Affix && morphology.Fusion == Fusion.Isolating)
            {
                kind = ModalKind.Particle;
            }
            var prefix = rand.NextDouble() < PrefixChance(morphology);
            var form = forms.Draw(() => kind switch
            {
                ModalKind.Affix => MarkerForm(rand, true, phonology, dropoff),
                ModalKind.Verb => GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1),
                _ => MarkerForm(rand, false, phonology, dropoff),
            });
            foreach (var modality in region)
            {
                markers[modality] = kind switch
                {
                    ModalKind.Affix => new ModalAffix(new Affix(form, [modality]), prefix ? ExponentPosition.Prefix : ExponentPosition.Suffix),
                    ModalKind.Verb => new ModalVerb(new Lexeme(new Root(form, ModalityMap.VerbGloss(modality)), PartOfSpeech.Verb), before),
                    _ => new ModalParticle(new Particle(form, [modality]), before),
                };
            }
        }
        return new Modals(markers);
    }
}
