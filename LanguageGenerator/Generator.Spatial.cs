namespace LanguageGenerator;

//configurations: in, on, under and the rest, apart from the path a role says
public static partial class Generator
{
    private enum ConfigurationStrategy
    {
        Adposition,
        RelationalNoun,
        Case,
    }

    //how a language marks its configurations, as a preference that most of them follow. Guesses: WALS
    //doesn't count these, but Svorou finds relational nouns the commonest source of spatial words,
    //and adpositions what they become, and only a minority of languages have spatial cases beyond
    //a locative, mostly in Uralic and the Caucasus
    private static readonly (ConfigurationStrategy Value, int Weight)[] ConfigurationStrategyWeights =
    [
        (ConfigurationStrategy.Adposition, 45),
        (ConfigurationStrategy.RelationalNoun, 40),
        (ConfigurationStrategy.Case, 15),
    ];

    /// <summary>
    /// Which configurations a language tells apart, from the edges of the configuration map it joins,
    /// and how it marks each region it's left with: an adposition, a relational noun or a localization
    /// in the noun.
    /// </summary>
    //a localization needs a noun that inflects, and a location marked by case for it to go with, as
    //Finnish talo-ssa 'house-IN.LOC' has one. Most regions follow the language's preference, and the
    //rest are drawn again, so a language can mix them, as Finnish has cases for in and on and
    //postpositions for under and behind; in and on are likelier cases than the rest, as there
    public static Configurations GenerateConfigurations(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, MorphosyntacticAlignment alignment,
        IReadOnlyList<RoleRegion> roleRegions, IReadOnlyList<Case> cases, Phonology phonology, double dropoff)
    {
        var regions = ConfigurationMap.Regions(SemanticMap.Joined(rand, ConfigurationMap.Edges));
        var locative = roleRegions.First(r => r.Contains(Role.Location)).Case;
        var casesAllowed = morphology.Fusion != Fusion.Isolating && locative is not null && cases.Contains(locative);
        var allowed = ConfigurationStrategyWeights.Where(w => casesAllowed || w.Value != ConfigurationStrategy.Case).ToArray();
        var preferred = Sampling.Categorical(rand, allowed);

        var type = wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial ? AdpositionType.Preposition : AdpositionType.Postposition;
        var forms = new DistinctForms(draws: 11);
        var nouns = new HashSet<string>();
        var markers = new Dictionary<Configuration, ConfigurationMarker>();
        foreach (var region in regions.Where(r => !r.Contains(Configuration.At)))
        {
            var inOrOn = region.Head is Configuration.In or Configuration.On;
            var strategy = Sampling.Categorical(rand, [.. allowed.Select(w => (w.Value, w.Weight
                * (w.Value == preferred ? 4 : 1)
                * (w.Value == ConfigurationStrategy.Case && !inOrOn ? 1 : 4)))]);
            var choices = ConfigurationMap.NounsFor(region.Head).Where(n => !nouns.Contains(n.Meaning)).ToArray();
            if (strategy == ConfigurationStrategy.RelationalNoun && choices.Length == 0)
            {
                strategy = ConfigurationStrategy.Adposition;
            }
            ConfigurationMarker marker;
            switch (strategy)
            {
                case ConfigurationStrategy.RelationalNoun:
                {
                    var meaning = Sampling.Categorical(rand, choices);
                    nouns.Add(meaning);
                    marker = new RelationalNoun(meaning);
                    break;
                }
                case ConfigurationStrategy.Case:
                    marker = new SpatialCase(Localization.For(region.Head));
                    break;
                default:
                {
                    var word = new Lexeme(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1)), ConfigurationMap.GlossFor(region.Head)), PartOfSpeech.Adposition);
                    marker = new SpatialAdposition(new Adposition(word, type), GovernedCase(rand, alignment, cases));
                    break;
                }
            }
            foreach (var configuration in region.Configurations)
            {
                markers[configuration] = marker;
            }
        }
        return new Configurations(regions, markers);
    }
}
