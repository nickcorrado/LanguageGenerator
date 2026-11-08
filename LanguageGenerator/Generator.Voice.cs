namespace LanguageGenerator;

//voice
public static partial class Generator
{
    //WALS 107A crossed with 98A: a passive in 37 of 52 nominative-accusative languages, but only 7 of
    //32 ergative ones, whose patients are already in the case an intransitive subject takes, and 42
    //of 97 with no core cases
    private static double PassiveChance(MorphosyntacticAlignment alignment) => alignment switch
    {
        MorphosyntacticAlignment.NominativeAccusative => 37.0 / 52,
        MorphosyntacticAlignment.ErgativeAbsolutive => 7.0 / 32,
        _ => 42.0 / 97,
    };

    //a passive is marked on the side the language puts its other affixes, and an isolating language,
    //with no affixes, uses a word on that side
    public static Passive? GeneratePassive(Random rand, MorphosyntacticAlignment alignment, MorphologicalProfile morphology, Phonology phonology, double dropoff)
    {
        if (rand.NextDouble() >= PassiveChance(alignment))
        {
            return null;
        }
        var before = rand.NextDouble() < PrefixChance(morphology);
        var position = morphology.Fusion == Fusion.Isolating
            ? (before ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle)
            : (before ? ExponentPosition.Prefix : ExponentPosition.Suffix);
        var affix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
        var form = MarkerForm(rand, affix, phonology, dropoff);
        return new Passive(affix ? new Affix(form, [Voice.Passive]) : new Particle(form, [Voice.Passive]), position);
    }
}
