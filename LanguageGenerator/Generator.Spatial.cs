namespace LanguageGenerator;

//configurations: in, on, under and the rest, apart from the path a role says
public static partial class Generator
{
    /*
      how a language marks its configurations, as a preference that most of them follow. Guesses: WALS
      doesn't count these, but Svorou finds relational nouns the commonest source of spatial words,
      and adpositions what they become, and only a minority of languages have spatial cases beyond
      a locative, mostly in Uralic and the Caucasus
     */
    private static readonly (ConfigurationStrategy Value, int Weight)[] ConfigurationStrategyWeights =
    [
        (ConfigurationStrategy.Adposition, 45),
        (ConfigurationStrategy.RelationalNoun, 40),
        (ConfigurationStrategy.Case, 15),
    ];

    /// <summary>Which edges of the configuration map a language joins, each with its own chance.</summary>
    public static ConfigurationRegions SelectConfigurationRegions(Random rand) => new(SemanticMap.Joined(rand, ConfigurationMap.Edges));

    /// <summary>
    /// How a language marks each region of its configuration map that isn't said like At: an
    /// adposition, a relational noun or a localization in the noun. No word is coined yet.
    /// </summary>
    /*
      a localization needs a noun that inflects, and a location marked by case for it to go with, as
      Finnish talo-ssa 'house-IN.LOC' has one. Most regions follow the language's preference, and the
      rest are drawn again, so a language can mix them, as Finnish has cases for in and on and
      postpositions for under and behind; in and on are likelier cases than the rest, as there
     */
    public static ConfigurationPlan PlanConfigurations(Random rand, ConfigurationRegions map, MorphologicalProfile morphology, MorphosyntacticAlignment alignment,
        IReadOnlyList<RoleRegion> roleRegions, IReadOnlyList<Case> cases)
    {
        var casesAllowed = SpatialCasesAllowed(morphology, roleRegions, cases);
        var allowed = ConfigurationStrategyWeights.Where(w => casesAllowed || w.Value != ConfigurationStrategy.Case).ToArray();
        var preferred = Sampling.Categorical(rand, allowed);

        var nouns = new HashSet<string>();
        var markers = new Dictionary<Configuration, PlannedMarker>();
        foreach (var region in map.Regions.Where(r => !r.Contains(Configuration.At)))
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
            PlannedMarker marker;
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
                    marker = new PlannedAdposition(GovernedCase(rand, alignment, cases));
                    break;
            }
            foreach (var configuration in region.Configurations)
            {
                markers[configuration] = marker;
            }
        }
        return new ConfigurationPlan(map, preferred, markers, casesAllowed);
    }

    /// <summary>
    /// Whether a language can mark configurations by case: it needs nouns that inflect, and a case
    /// on the location's region for the localization to go with.
    /// </summary>
    public static bool SpatialCasesAllowed(MorphologicalProfile morphology, IReadOnlyList<RoleRegion> roleRegions, IReadOnlyList<Case> cases) =>
        morphology.Fusion != Fusion.Isolating && roleRegions.First(r => r.Contains(Role.Location)).Case is { } locative && cases.Contains(locative);

    /// <summary>A one-syllable word for each adposition a language's plan for its configurations calls for.</summary>
    public static Configurations CoinConfigurations(Random rand, ConfigurationPlan plan, WordOrder wordOrder, Phonology phonology)
    {
        var type = wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial ? AdpositionType.Preposition : AdpositionType.Postposition;
        var forms = new DistinctForms(draws: 11);
        var markers = new Dictionary<Configuration, ConfigurationMarker>();
        foreach (var region in plan.Map.Regions.Where(r => plan.Markers.ContainsKey(r.Head)))
        {
            ConfigurationMarker marker = plan.Markers[region.Head].Value switch
            {
                PlannedAdposition a => new SpatialAdposition(new Adposition(new Lexeme(new Root(
                    forms.Draw(() => GenerateForm(rand, phonology, 0, maxSyllables: 1)), ConfigurationMap.GlossFor(region.Head)), PartOfSpeech.Adposition), type), a.Governs),
                RelationalNoun n => n,
                SpatialCase c => c,
            };
            foreach (var configuration in region.Configurations)
            {
                markers[configuration] = marker;
            }
        }
        return new Configurations(plan, markers);
    }
}
