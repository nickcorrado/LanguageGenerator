namespace LanguageGenerator;

//commands
public static partial class Generator
{
    //which numbers have an imperative of their own
    private enum ImperativeForms
    {
        SingularAndPlural,
        SingularOnly,
        PluralOnly,
        OneForBoth,
        None,
    }

    // https://wals.info/feature/70A
    private static readonly (ImperativeForms Value, int Languages)[] ImperativeCounts =
    [
        (ImperativeForms.SingularAndPlural, 292),
        (ImperativeForms.SingularOnly, 43),
        (ImperativeForms.PluralOnly, 2),
        (ImperativeForms.OneForBoth, 89),
        (ImperativeForms.None, 122),
    ];

    // https://wals.info/feature/71A: whether a prohibitive uses the imperative's verb form, and whether
    //its negator is one that declaratives don't use. Where it doesn't use the imperative, Spanish and
    //others use a subjunctive; with no such form here, it's the ordinary second person present
    private static readonly ((bool UsesImperative, bool OwnNegator) Value, int Languages)[] ProhibitiveCounts =
    [
        ((true, false), 113),
        ((true, true), 182),
        ((false, false), 55),
        ((false, true), 146),
    ];

    //WALS counts a bare stem as an imperative of its own, and says that many languages use one,
    //especially for the singular, but gives no figure, so the half is a guess. A plural is marked,
    //like the plural everywhere else here
    public static Commands GenerateCommands(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var position = rand.NextDouble() < morphology.ParticleChance
            ? (wordOrder.VerbPhraseDirectionality == HeadDirectionality.Initial ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle)
            : rand.NextDouble() < morphology.PrefixChance ? ExponentPosition.Prefix : ExponentPosition.Suffix;
        var affix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
        var forms = new DistinctForms();

        Morpheme Marker(List<Phoneme> form, params Grammeme[] values) => affix ? new Affix(form, values) : new Particle(form, values);
        Morpheme Marked(params Grammeme[] values) => Marker(forms.Draw(() => MarkerForm(rand, affix, phonology, dropoff)), values);
        Morpheme Unmarkable(params Grammeme[] values) => rand.NextDouble() < 0.5 ? Marker([], values) : Marked(values);

        var (singular, plural) = Sampling.Categorical(rand, ImperativeCounts) switch
        {
            ImperativeForms.SingularAndPlural => (Unmarkable(Mood.Imperative, Number.Singular), Marked(Mood.Imperative, Number.Plural)),
            ImperativeForms.SingularOnly => (Unmarkable(Mood.Imperative, Number.Singular), (Morpheme?)null),
            ImperativeForms.PluralOnly => ((Morpheme?)null, Marked(Mood.Imperative, Number.Plural)),
            ImperativeForms.OneForBoth => Both(Unmarkable(Mood.Imperative)),
            _ => ((Morpheme?)null, (Morpheme?)null),
        };

        var (usesImperative, ownNegator) = Sampling.Categorical(rand, ProhibitiveCounts);
        var prohibitive = ownNegator ? GenerateNegation(rand, morphology, phonology, dropoff, Mood.Prohibitive) : null;
        return new Commands(singular, plural, position, usesImperative, prohibitive);

        static (Morpheme?, Morpheme?) Both(Morpheme marker) => (marker, marker);
    }
}
