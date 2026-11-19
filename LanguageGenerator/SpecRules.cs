namespace LanguageGenerator;

/// <summary>
/// A rule about what one setting implies for another, or for what an earlier stage draws: a coda
/// restriction implies a template with a coda, and a dative case implies a recipient heading its
/// region. Each holds of every language the generator makes, so applying one before drawing never
/// changes which languages meet a spec: it only keeps a value written in from contradicting what was
/// drawn before it, refuses a spec that contradicts itself, and says what a frontend should grey out.
/// </summary>
public abstract record SpecRule
{
    /// <summary>
    /// Two choice settings whose values have to go together: each is narrowed to the values that go
    /// with some value the spec allows for the other, a free setting allowing all.
    /// </summary>
    public static SpecRule Together<TStageA, TA, TStageB, TB>(Setting<TStageA, TA> first, Setting<TStageB, TB> second, Func<TA, TB, bool> together, string says)
        where TA : notnull where TB : notnull => new Narrowing(says, spec =>
        {
            var firsts = (spec.Allowed(first) ?? first.AllValues!).Cast<TA>().ToList();
            var seconds = (spec.Allowed(second) ?? second.AllValues!).Cast<TB>().ToList();
            List<TA> keptFirsts = [.. firsts.Where(a => seconds.Any(b => together(a, b)))];
            List<TB> keptSeconds = [.. seconds.Where(b => firsts.Any(a => together(a, b)))];
            if (keptFirsts.Count == 0 || keptSeconds.Count == 0)
            {
                return null;
            }
            var narrowed = keptFirsts.Count < firsts.Count ? spec.With(first, [.. keptFirsts.Cast<object>()]) : spec;
            return keptSeconds.Count < seconds.Count ? narrowed.With(second, [.. keptSeconds.Cast<object>()]) : narrowed;
        });

    /// <summary>The conditions a spec puts on what a stage draws.</summary>
    public static SpecRule On<T>(Stage<T> stage, Func<LanguageSpec, IEnumerable<Condition<T>>> conditions) =>
        new Conditions(spec => conditions(spec));

    /// <summary>
    /// A rule that narrows the values settings can take. Narrow gives the spec narrowed, the same
    /// spec if there's nothing to narrow, or null if a setting is left no value at all.
    /// </summary>
    public sealed record Narrowing(string Says, Func<LanguageSpec, LanguageSpec?> Narrow) : SpecRule;

    /// <summary>A rule that puts conditions on what stages draw.</summary>
    public sealed record Conditions(Func<LanguageSpec, IEnumerable<ICondition>> Implied) : SpecRule;
}

/// <summary>
/// What a spec comes to once its rules are applied: the spec with each setting narrowed to the values
/// the others leave it, the conditions they put on what stages draw, and what can't be met at all.
/// </summary>
public sealed class Implications(LanguageSpec spec, IReadOnlyList<ICondition> conditions, IReadOnlyList<string> problems)
{
    public LanguageSpec Spec { get; } = spec;
    public IReadOnlyList<ICondition> Conditions { get; } = conditions;
    public IReadOnlyList<string> Problems { get; } = problems;

    public IReadOnlyList<Condition<T>> For<T>(Stage<T> stage) => [.. Conditions.OfType<Condition<T>>().Where(c => c.Stage == stage)];
}

/// <summary>The rules a spec has to keep to, besides each setting's own values.</summary>
public static class SpecRules
{
    public static IReadOnlyList<SpecRule> All { get; } =
    [
        //a restriction is only ever drawn for a template with a coda, and means nothing without one
        SpecRule.Together(Settings.SyllableTemplate, Settings.CodaRestriction,
            (template, codas) => codas == CodaRestriction.Any || template.Slots.Contains(Slot.Final),
            "A coda restriction needs a syllable template with a coda: CVF, CLVF, SCVF or SCLVF."),
        //with the verb before its object, between them is just before the verb, which WALS has no
        //languages with obliques in either
        SpecRule.Together(Settings.ConstituentOrder, Settings.ObliquePosition,
            (order, position) => position != ObliquePosition.BetweenObjectAndVerb || WordOrder.ObjectPrecedesVerb(order),
            "Obliques can only go between the object and the verb where the object comes first: SOV, OVS or OSV."),

        //a case is named for the head of the region it marks, so a role whose region another heads
        //has none of its own: a recipient joined to the possessor is marked by the genitive
        .. RoleMap.Roles.Where(r => RoleMap.CaseFor(r) is not null).Select(role => SpecRule.On(Settings.RoleRegionStage, spec =>
            spec.Allowed(Settings.HasCase(RoleMap.CaseFor(role)!)) is [true]
                ? [new Condition<RoleRegions>(Settings.RoleRegionStage,
                    $"A {RoleMap.CaseFor(role)!.Name.ToLowerInvariant()} case needs the {Settings.Kebab(role)} to head its region, joined to no role above it on the role map.",
                    (regions, _) => regions.Regions.First(r => r.Contains(role)).Head == role, null)]
                : [])),

        //a localization goes with the case of the location's region, as Finnish -ssa is IN with the
        //locative, in a noun that inflects
        .. Settings.ConfigurationMarkers.Append(Settings.ConfigurationMarking).Select(marker => SpecRule.Together(Settings.Fusion, marker,
            (fusion, strategy) => strategy != ConfigurationStrategy.Case || fusion != Fusion.Isolating,
            $"{marker.Name} can only be Case where nouns inflect, so fusion can't be Isolating.")),
        SpecRule.On(Settings.RoleRegionStage, spec => NeedsSpatialCase(spec)
            ? [new Condition<RoleRegions>(Settings.RoleRegionStage, "A spatial case needs a case on the location's region, and the spec rules out the case it would have.",
                (regions, _) => spec.Allowed(Settings.HasCase(LocationCase(regions.Regions))) is not [false], null)]
            : []),
        SpecRule.On(Settings.CaseStage, spec => NeedsSpatialCase(spec)
            ? [new Condition<CaseSystem>(Settings.CaseStage, "A spatial case needs a case on the location's region.",
                (system, _) => system.Cases.Contains(LocationCase(system.Regions)),
                system => system with { Cases = CaseSystem.Ordered([.. system.Cases, LocationCase(system.Regions)]) })]
            : []),

        //an isolating language has no affixes, so it marks with words what another might with affixes
        SpecRule.Together(Settings.Fusion, Settings.Negation,
            (fusion, negation) => fusion != Fusion.Isolating || negation is not (NegationStrategy.Prefix or NegationStrategy.Suffix),
            "Negation can only be a prefix or suffix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.PolarQuestions,
            (fusion, polar) => fusion != Fusion.Isolating || polar is not (PolarQuestion.Prefix or PolarQuestion.Suffix),
            "Polar questions can only be marked by a prefix or suffix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.PossessiveMarking,
            (fusion, marking) => fusion != Fusion.Isolating || marking is not (PossessiveMarking.Head or PossessiveMarking.Double),
            "Possession can only be marked on the possessed noun where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.Comparative,
            (fusion, kind) => fusion != Fusion.Isolating || kind != MarkerKind.Affix,
            "A comparative can only be an affix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.Similative,
            (fusion, kind) => fusion != Fusion.Isolating || kind != MarkerKind.Affix,
            "A similative can only be an affix where words take affixes, so fusion can't be Isolating."),
        //only an affix on the possessed noun has a side
        SpecRule.Together(Settings.PossessiveMarking, Settings.PossessiveAffixes,
            (marking, position) => position == ExponentPosition.Suffix || marking is PossessiveMarking.Head or PossessiveMarking.Double,
            "Possessive affixes can only be prefixes where possession is marked on the possessed noun: Head or Double."),

        //a configuration's marker belongs to its region, and a region with At in it has none
        SpecRule.On(Settings.ConfigurationRegionStage, ConfigurationConditions),
    ];

    /// <summary>
    /// The spec with its rules applied: each setting narrowed until no rule narrows any further, and
    /// the conditions the narrowed spec puts on what stages draw, with anything that can't be met.
    /// </summary>
    public static Implications Close(LanguageSpec spec)
    {
        var narrowed = spec;
        List<string> problems = [];
        for (var changed = true; changed && problems.Count == 0;)
        {
            changed = false;
            foreach (var rule in All.OfType<SpecRule.Narrowing>())
            {
                switch (rule.Narrow(narrowed))
                {
                    case null:
                        problems.Add(rule.Says);
                        break;
                    case var next when next != narrowed:
                        (narrowed, changed) = (next, true);
                        break;
                }
            }
        }
        if (problems.Count > 0)
        {
            return new Implications(narrowed, [], [.. problems.Distinct()]);
        }
        List<ICondition> conditions = [.. All.OfType<SpecRule.Conditions>().SelectMany(r => r.Implied(narrowed))];
        problems.AddRange(conditions.GroupBy(c => c.Stage).Select(g => g.Key.Unmet(narrowed, [.. g])).OfType<string>());
        return new Implications(narrowed, conditions, problems);
    }

    //whether the spec has some configuration marked by a localization, or prefers them
    private static bool NeedsSpatialCase(LanguageSpec spec) =>
        Settings.ConfigurationMarkers.Append(Settings.ConfigurationMarking).Any(s => spec.Allowed(s) is [ConfigurationStrategy.Case]);

    private static Case LocationCase(IReadOnlyList<RoleRegion> regions) => regions.First(r => r.Contains(Role.Location)).Case!;

    //a configuration can be said like at only in a region with at, have a marker of its own only in
    //a region without, and be marked by case only where the language can have spatial cases, which
    //is assumed when nothing's been drawn yet
    private static bool Possible(ConfigurationStrategy strategy, ConfigurationRegion region, Earlier earlier) => strategy switch
    {
        ConfigurationStrategy.At => region.Contains(Configuration.At),
        ConfigurationStrategy.Case => !region.Contains(Configuration.At) && (earlier.Of(Settings.MorphologyStage) is not { } morphology
            || earlier.Of(Settings.CaseStage) is not { } system || Generator.SpatialCasesAllowed(morphology, system.Regions, system.Cases)),
        _ => !region.Contains(Configuration.At),
    };

    private static IEnumerable<Condition<ConfigurationRegions>> ConfigurationConditions(LanguageSpec spec)
    {
        var stage = Settings.ConfigurationRegionStage;
        Dictionary<Configuration, IReadOnlyList<ConfigurationStrategy>> allowed = ConfigurationMap.Configurations.Skip(1)
            .Where(c => spec.Allowed(Settings.MarkerOf(c)) is not null)
            .ToDictionary(c => c, c => (IReadOnlyList<ConfigurationStrategy>)[.. spec.Allowed(Settings.MarkerOf(c))!.Cast<ConfigurationStrategy>()]);
        foreach (var (configuration, strategies) in allowed)
        {
            var name = Settings.Kebab(configuration);
            var says = strategies.All(s => s == ConfigurationStrategy.At) ? $"{name} is said like at, so it needs edges joining it to at."
                : !strategies.Contains(ConfigurationStrategy.At) ? $"{name} has a marker of its own, so no edges can join it to at."
                : $"{name} needs edges joining it to at, or a marker of its own that it can have.";
            yield return new Condition<ConfigurationRegions>(stage, says,
                (map, earlier) => strategies.Any(s => Possible(s, map.Regions.First(r => r.Contains(configuration)), earlier)), null);
        }
        //configurations in one region share its marker, so they need a strategy they can all have
        if (allowed.Count > 1)
        {
            yield return new Condition<ConfigurationRegions>(stage,
                "Configurations joined into one region share its marker, so no edges can join configurations fixed to markers that can't be the same.",
                (map, earlier) => map.Regions.All(region => Enum.GetValues<ConfigurationStrategy>()
                    .Where(s => region.Configurations.All(c => !allowed.TryGetValue(c, out var strategies) || strategies.Contains(s)))
                    .Any(s => Possible(s, region, earlier))), null);
        }
    }
}
