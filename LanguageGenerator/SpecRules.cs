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
        .. InventoryRules(Settings.VowelInventory),
        .. InventoryRules(Settings.ConsonantInventory),
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
        //a configuration can only be marked in a way some arrangement of the map's edges allows,
        //given the spec's other markers and edges: against, which reaches at only through on, can't
        //be said like at where on has a marker of its own. Narrowing it to what's left lets the rules
        //for spatial cases see a case the spec only implies
        new SpecRule.Narrowing("No arrangement of the configuration map's edges fits the markers the spec fixes.", NarrowMarkersToTheMap),
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
        .. Settings.ModalMarkers.Select(marker => SpecRule.Together(Settings.Fusion, marker,
            (fusion, kind) => fusion != Fusion.Isolating || kind != ModalKind.Affix,
            $"{marker.Name} can only be an affix where words take affixes, so fusion can't be Isolating.")),
        //modalities joined by an edge share a marker, so an edge fixed joined narrows them to the
        //kinds they can share, and two that can't share one can't be joined
        .. ModalityMap.Edges.Select(e => (e.First, e.Second)).Select(edge => new SpecRule.Narrowing(
            $"Modalities joined by an edge share a marker, so {edge.First.Name.ToLowerInvariant()} and {edge.Second.Name.ToLowerInvariant()} can't be joined when they're marked differently.",
            spec =>
            {
                var (first, second, joined) = (Settings.ModalOf(edge.First), Settings.ModalOf(edge.Second), Settings.ModalityEdge(edge.First, edge.Second));
                var firsts = spec.Allowed(first) ?? first.AllValues!;
                var seconds = spec.Allowed(second) ?? second.AllValues!;
                List<object> shared = [.. firsts.Intersect(seconds)];
                if (spec.Allowed(joined) is [true])
                {
                    if (shared.Count == 0)
                    {
                        return null;
                    }
                    var narrowed = shared.Count < firsts.Count ? spec.With(first, shared) : spec;
                    return shared.Count < seconds.Count ? narrowed.With(second, shared) : narrowed;
                }
                return shared.Count == 0 && spec.Allowed(joined) is null or [_, _] ? spec.With(joined, [false]) : spec;
            })),
        //only an affix on the possessed noun has a side
        SpecRule.Together(Settings.PossessiveMarking, Settings.PossessiveAffixes,
            (marking, position) => position == ExponentPosition.Suffix || marking is PossessiveMarking.Head or PossessiveMarking.Double,
            "Possessive affixes can only be prefixes where possession is marked on the possessed noun: Head or Double."),

        //a configuration's marker belongs to its region, and a region with At in it has none
        SpecRule.On(Settings.ConfigurationRegionStage, ConfigurationConditions),

        //a change that works on codas needs syllables that have them, and devoicing or weakening a
        //coda needs one that can be an obstruent
        .. new[] { Settings.FinalDevoicing, Settings.CodaDevoicing, Settings.CodaWeakening, Settings.CompensatoryLengthening, Settings.ClosedSyllableLaxing, Settings.Syncope }
            .Select(change => SpecRule.Together(Settings.SyllableTemplate, change, (template, on) => !on || HasCodas(template),
                $"{change.Name} needs a syllable template with a coda: CVF, CLVF, SCVF or SCLVF.")),
        .. new[] { Settings.CodaDevoicing, Settings.CodaWeakening }.Select(change => SpecRule.Together(Settings.CodaRestriction, change,
            (codas, on) => !on || codas == CodaRestriction.Any, $"{change.Name} needs codas that can be any consonant, so coda-restriction must be Any.")),
        //syllable contact only matters across a coda, and a language repairs only the contacts it restricts
        SpecRule.Together(Settings.SyllableTemplate, Settings.SyllableContact, (template, contact) => contact == SyllableContact.Any || HasCodas(template),
            "A syllable contact restriction needs a syllable template with a coda: CVF, CLVF, SCVF or SCLVF."),
        SpecRule.Together(Settings.SyllableContact, Settings.ContactRepair, (contact, repair) => repair == ContactRepair.None || contact != SyllableContact.Any,
            "A contact repair needs a syllable contact restriction, so syllable-contact can't be Any."),
        //and a change needs the sounds it works with
        .. Needs(Settings.CompensatoryLengthening, on => on ? HasLong : null, Settings.VowelInventory, "compensatory-lengthening needs long vowels."),
        .. Needs(Settings.OpenSyllableLengthening, on => on ? HasLong : null, Settings.VowelInventory, "open-syllable-lengthening needs long vowels."),
        .. Needs(Settings.UnstressedShortening, on => on ? HasLong : null, Settings.VowelInventory, "unstressed-shortening needs long vowels."),
        .. Needs(Settings.UnstressedVowels, kind => kind switch
            {
                UnstressedVowels.Reduction => vowels => vowels.Contains(Vowel.ə),
                UnstressedVowels.Raising => vowels => vowels.Any(v => v.Height is VowelHeight.CloseMid or VowelHeight.OpenMid) && vowels.Contains(Vowel.i) && vowels.Contains(Vowel.u),
                _ => null,
            },
            Settings.VowelInventory, "Unstressed reduction needs ə, and unstressed raising a mid vowel besides i and u."),
        .. Needs(Settings.VowelHarmony, kind => SoundChange.Harmonies.Single(h => h.Kind == kind).Change is { } harmony ? harmony.CanApplyTo : null,
            Settings.VowelInventory, "Vowel harmony needs a pair of vowels it can match: front-back harmony a front vowel and its back partner, like i and u, and tongue-root harmony e and ɛ or o and ɔ."),
        .. Needs(Settings.GlideFormation, on => on ? HasGlide : null, Settings.ConsonantInventory, "glide-formation needs j or w."),
        .. Needs(Settings.HiatusResolution, kind => kind switch
            {
                HiatusResolution.GlideInsertion => HasGlide,
                HiatusResolution.GlottalInsertion => consonants => consonants.Contains(Consonant.ʔ) || consonants.Contains(Consonant.h),
                _ => null,
            },
            Settings.ConsonantInventory, "Glide insertion needs j or w, and glottal insertion ʔ or h."),
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

    //each value a marker allows is tried with every arrangement of the map's edges, assuming the
    //best of the stages before them, as a spec is checked. A spec fixing nothing about the map has
    //nothing to narrow, which keeps checking it quick. It never refuses a spec itself.
    //
    //Trying markers one at a time finds every case the map forces, not just those it forces on one
    //marker: configurations joined by edges the spec fixes share a marker, so a value that fits one
    //of them fits them all, and an edge left free can always be left unjoined, keeping a group in a
    //region of its own. A group said like at reaches it only through groups that are said like at
    //too, which any value that fits has to allow already. So where every marker that allows a case
    //has some other value that fits, those values all fit together, and no case is needed. Random
    //specs bear this out. A map with edges that join groups some other way would need checking anew
    private static LanguageSpec? NarrowMarkersToTheMap(LanguageSpec spec)
    {
        if (!Settings.ConfigurationMarkers.Concat<ISetting>(Settings.ConfigurationEdges).Any(s => spec.Allowed(s) is not null))
        {
            return spec;
        }
        static bool Fits(LanguageSpec spec) => Settings.ConfigurationRegionStage.Unmet(spec, [.. ConfigurationConditions(spec)]) is null;
        var narrowed = spec;
        foreach (var marker in Settings.ConfigurationMarkers)
        {
            var allowed = narrowed.Allowed(marker) ?? marker.AllValues!;
            List<object> possible = [.. allowed.Where(value => Fits(narrowed.With(marker, [value])))];
            //where no value fits, the conditions on the map refuse the spec, saying why
            if (possible.Count > 0 && possible.Count < allowed.Count)
            {
                narrowed = narrowed.With(marker, possible);
            }
        }
        return narrowed;
    }

    private static bool HasCodas(SyllableTemplate template) => template.Slots.Contains(Slot.Final);

    private static bool HasLong(IReadOnlyCollection<Vowel> vowels) => vowels.Any(v => v.Long);

    private static bool HasGlide(IReadOnlyCollection<Consonant> consonants) => consonants.Contains(Consonant.j) || consonants.Contains(Consonant.w);

    //values of a setting that need certain sounds, given as what each needs, or null for one that
    //needs none. What a value needs only gets likelier to hold as sounds are added, so an inventory
    //can have it exactly when every sound the spec doesn't forbid, all together, has it. Where the
    //inventory is fixed exactly, or sounds forbidden, a value no inventory the spec allows can have
    //is narrowed away, and if every value left needs sounds, exact inventories are narrowed to those
    //that have them. Where the setting allows only values that need sounds, the inventory is drawn
    //again until it has the sounds of one of them, and failing that, has the commonest sounds the
    //spec allows added until it does
    private static IEnumerable<SpecRule> Needs<T, TValue, TPhoneme>(Setting<T, TValue> setting, Func<TValue, Func<IReadOnlyCollection<TPhoneme>, bool>?> needs,
        InventorySettings<TPhoneme> inventory, string says)
        where TValue : notnull where TPhoneme : Phoneme
    {
        bool Allows(object value, IReadOnlyList<TPhoneme> phonemes) => needs((TValue)value) is not { } has || has(phonemes);
        yield return new SpecRule.Narrowing(says, spec =>
        {
            var exact = spec.Allowed(inventory.Exact);
            if (exact is null && inventory.Sounds.All(s => spec.Allowed(s.Has) is not [false]))
            {
                return spec;
            }
            IReadOnlyList<object> inventories = exact ?? [inventory.Allowed(spec)];
            var values = spec.Allowed(setting) ?? setting.AllValues!;
            List<object> kept = [.. values.Where(v => inventories.Any(i => Allows(v, (IReadOnlyList<TPhoneme>)i)))];
            if (kept.Count == 0)
            {
                return null;
            }
            var narrowed = kept.Count < values.Count ? spec.With(setting, kept) : spec;
            if (exact is not null && kept.All(v => needs((TValue)v) is not null))
            {
                List<object> having = [.. exact.Where(i => kept.Any(v => Allows(v, (IReadOnlyList<TPhoneme>)i)))];
                narrowed = having.Count < exact.Count ? narrowed.With(inventory.Exact, having) : narrowed;
            }
            return narrowed;
        });
        yield return SpecRule.On(inventory.Stage, spec => spec.Allowed(setting) is { } values && values.All(v => needs((TValue)v) is not null)
            ? [new Condition<IReadOnlyList<TPhoneme>>(inventory.Stage, says, (phonemes, _) => values.Any(v => Allows(v, phonemes)),
                phonemes => inventory.Allowed(spec).Aggregate(phonemes, (added, sound) => values.Any(v => Allows(v, added)) ? added : SettingWrites.WithSound(added, sound, true)))]
            : []);
    }

    //an inventory needs a sound, and an exact one has to keep to the sounds the spec requires and
    //forbids, which it then says all there is to say about. Where some sounds are forbidden and none
    //required, a draw with the forbidden ones taken out could be left with none, so the commonest
    //sound allowed is put back
    private static IEnumerable<SpecRule> InventoryRules<T>(InventorySettings<T> inventory) where T : Phoneme
    {
        yield return new SpecRule.Narrowing($"An inventory needs at least one sound, so not every {inventory.Noun} can be forbidden.",
            spec => inventory.Sounds.All(s => spec.Allowed(s.Has) is [false]) ? null : spec);
        yield return new SpecRule.Narrowing($"An exact list of {inventory.Noun}s can't lack a sound the spec requires or have one it forbids.", spec =>
        {
            var fixedSounds = inventory.Sounds.Where(s => spec.Allowed(s.Has) is [bool]).ToList();
            if (spec.Allowed(inventory.Exact) is not { } exact || fixedSounds.Count == 0)
            {
                return spec;
            }
            List<object> fitting = [.. exact.Where(i => fixedSounds.All(s => ((IReadOnlyList<T>)i).Contains(s.Sound) == (bool)spec.Allowed(s.Has)![0]))];
            if (fitting.Count == 0)
            {
                return null;
            }
            return fixedSounds.Aggregate(fitting.Count < exact.Count ? spec.With(inventory.Exact, fitting) : spec, (narrowed, s) => narrowed.Unfix(s.Has));
        });
        yield return SpecRule.On(inventory.Stage, spec => spec.Allowed(inventory.Exact) is null && inventory.Sounds.Any(s => spec.Allowed(s.Has) is [false])
            && !inventory.Sounds.Any(s => spec.Allowed(s.Has) is [true])
            ? [new Condition<IReadOnlyList<T>>(inventory.Stage, $"An inventory needs at least one sound.", (sounds, _) => sounds.Count > 0,
                sounds => [inventory.Allowed(spec)[0]])]
            : []);
    }

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
