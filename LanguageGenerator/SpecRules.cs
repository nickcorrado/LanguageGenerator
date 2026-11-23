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
    //what sound changes need of the inventories, which come before the rules, since they use them
    private static readonly IReadOnlyList<SoundNeed<Vowel>> VowelNeeds =
    [
        SoundNeed<Vowel>.Of(Settings.CompensatoryLengthening, on => on ? HasLong : null, "compensatory-lengthening needs long vowels."),
        SoundNeed<Vowel>.Of(Settings.OpenSyllableLengthening, on => on ? HasLong : null, "open-syllable-lengthening needs long vowels."),
        SoundNeed<Vowel>.Of(Settings.UnstressedShortening, on => on ? HasLong : null, "unstressed-shortening needs long vowels."),
        SoundNeed<Vowel>.Of(Settings.UnstressedVowels, kind => kind switch
            {
                UnstressedVowels.Reduction => vowels => vowels.Contains(Vowel.ə),
                UnstressedVowels.Raising => vowels => vowels.Any(v => v.Height is VowelHeight.CloseMid or VowelHeight.OpenMid) && vowels.Contains(Vowel.i) && vowels.Contains(Vowel.u),
                _ => null,
            },
            "Unstressed reduction needs ə, and unstressed raising a mid vowel besides i and u."),
        SoundNeed<Vowel>.Of(Settings.VowelHarmony, kind => SoundChange.Harmonies.Single(h => h.Kind == kind).Change is { } harmony ? harmony.CanApplyTo : null,
            "Vowel harmony needs a pair of vowels it can match: front-back harmony a front vowel and its back partner, like i and u, and tongue-root harmony e and ɛ or o and ɔ."),
    ];

    private static readonly IReadOnlyList<SoundNeed<Consonant>> ConsonantNeeds =
    [
        SoundNeed<Consonant>.Of(Settings.GlideFormation, on => on ? HasGlide : null, "glide-formation needs j or w."),
        SoundNeed<Consonant>.Of(Settings.VernersLaw, on => on ? SoundChange.HasFricativePair : null,
            "verners-law needs a voiceless fricative and its voiced counterpart, like s and z or f and v."),
        SoundNeed<Consonant>.Of(Settings.HiatusResolution, kind => kind switch
            {
                HiatusResolution.GlideInsertion => HasGlide,
                HiatusResolution.GlottalInsertion => consonants => consonants.Contains(Consonant.ʔ) || consonants.Contains(Consonant.h),
                _ => null,
            },
            "Glide insertion needs j or w, and glottal insertion ʔ or h."),
    ];

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
        /*
          a configuration can only be marked in a way some arrangement of the map's edges allows,
          given the spec's other markers and edges: against, which reaches at only through on, can't
          be said like at where on has a marker of its own. Narrowing it to what's left lets the rules
          for spatial cases see a case the spec only implies
         */
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
        .. Settings.AspectMarkers.Select(marker => SpecRule.Together(Settings.Fusion, marker,
            (fusion, kind) => fusion != Fusion.Isolating || kind != AspectKind.Affix,
            $"{(marker.Name.StartsWith('i') ? "An" : "A")} {marker.Name} can only be an affix where words take affixes, so fusion can't be Isolating.")),
        SpecRule.Together(Settings.Fusion, Settings.DefiniteArticle,
            (fusion, kind) => fusion != Fusion.Isolating || kind != DefiniteArticle.Affix,
            "A definite article can only be an affix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.IndefiniteArticle,
            (fusion, kind) => fusion != Fusion.Isolating || kind != IndefiniteArticle.Affix,
            "An indefinite article can only be an affix where words take affixes, so fusion can't be Isolating."),
        //an article agreeing in gender needs genders to agree in, and an article of its own: 'one'
        //agrees in nothing
        SpecRule.Together(Settings.Genders, Settings.ArticleGenderAgreement, (genders, agrees) => !agrees || genders != NounGenders.None,
            "An article can only agree in gender where nouns have genders, so genders can't be None."),
        new SpecRule.Narrowing("An article can only agree in gender where there's one that can: a definite article, or an indefinite one that isn't the word for 'one'.", spec =>
        {
            var definites = (spec.Allowed(Settings.DefiniteArticle) ?? Settings.DefiniteArticle.AllValues!).Cast<DefiniteArticle>().ToList();
            var indefinites = (spec.Allowed(Settings.IndefiniteArticle) ?? Settings.IndefiniteArticle.AllValues!).Cast<IndefiniteArticle>().ToList();
            var agreeing = (spec.Allowed(Settings.ArticleGenderAgreement) ?? Settings.ArticleGenderAgreement.AllValues!).Cast<bool>().ToList();
            var noDefinite = definites is [DefiniteArticle.None];
            var noIndefinite = indefinites.All(i => i is IndefiniteArticle.None or IndefiniteArticle.One);
            //each narrowing returns the same spec once there's nothing left to narrow, so closing reaches a fixpoint
            List<object> agreeingIndefinites = [.. indefinites.Where(i => i is IndefiniteArticle.Word or IndefiniteArticle.Affix).Cast<object>()];
            List<object> someDefinite = [.. definites.Where(d => d != DefiniteArticle.None).Cast<object>()];
            if (!agreeing.Contains(false))
            {
                return noDefinite && noIndefinite ? null
                    : noDefinite && agreeingIndefinites.Count < indefinites.Count ? spec.With(Settings.IndefiniteArticle, agreeingIndefinites)
                    : noIndefinite && someDefinite.Count < definites.Count ? spec.With(Settings.DefiniteArticle, someDefinite)
                    : spec;
            }
            return noDefinite && noIndefinite && agreeing.Count > 1 ? spec.With(Settings.ArticleGenderAgreement, [false]) : spec;
        }),
        SpecRule.Together(Settings.Fusion, Settings.DemonstrativePosition,
            (fusion, position) => fusion != Fusion.Isolating || position is not (ExponentPosition.Prefix or ExponentPosition.Suffix),
            "A demonstrative can only be an affix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Genders, Settings.DemonstrativeGenderAgreement, (genders, agrees) => !agrees || genders != NounGenders.None,
            "A demonstrative can only agree in gender where nouns have genders, so genders can't be None."),
        //an affix can't stand alone for a noun
        SpecRule.Together(Settings.DemonstrativePosition, Settings.DemonstrativePronounsOwnStem,
            (position, own) => own || position is not (ExponentPosition.Prefix or ExponentPosition.Suffix),
            "A demonstrative that's an affix can't stand alone, so demonstrative-pronouns-own-stem can't be false."),
        //an article that's the demonstrative can't go beside it again
        SpecRule.Together(Settings.DefiniteArticle, Settings.ArticleWithDemonstrative,
            (kind, with) => !with || kind is DefiniteArticle.Word or DefiniteArticle.Affix,
            "Only a definite article of its own, a word or an affix, can stay beside a demonstrative."),
        //and with 'with' joins only nouns, never clauses
        SpecRule.Together(Settings.AndIsWith, Settings.ClauseAnd, (with, clauses) => !with || clauses != ClauseAnd.SameAsNouns,
            "Clauses can only be joined like nouns where 'and' between nouns isn't 'with', which doesn't join clauses."),
        .. Settings.SubordinatorPositions.Select(position => SpecRule.Together(Settings.Fusion, position,
            (fusion, at) => fusion != Fusion.Isolating || at != ExponentPosition.Suffix,
            $"{position.Name} can only be a suffix where words take affixes, so fusion can't be Isolating.")),
        SpecRule.Together(Settings.Fusion, Settings.PropositionComplementizer,
            (fusion, at) => fusion != Fusion.Isolating || at != ComplementizerPosition.Suffix,
            "proposition-complementizer can only be a suffix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.EventComplementizer,
            (fusion, at) => fusion != Fusion.Isolating || at != ComplementizerPosition.Suffix,
            "event-complementizer can only be a suffix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.Desiderative,
            (fusion, kind) => fusion != Fusion.Isolating || kind != DesiderativeKind.Affix,
            "'Want' can only be an affix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.Fusion, Settings.Relativizer,
            (fusion, kind) => fusion != Fusion.Isolating || kind != RelativizerKind.Affix,
            "A relativizer can only be an affix where words take affixes, so fusion can't be Isolating."),
        SpecRule.Together(Settings.PropositionComplementizer, Settings.Quotative,
            (position, quotative) => quotative != QuotativeKind.Complementizer || position != ComplementizerPosition.None,
            "A quotation can only be marked with the complementizer where a proposition has one, so proposition-complementizer can't be None."),
        //subordinators joined by an edge share a marker, and so its position
        .. SubordinatorMap.Edges.Select(e => (e.First, e.Second)).Select(edge => new SpecRule.Narrowing(
            $"Subordinators joined by an edge share a marker, so {edge.First.ToString().ToLowerInvariant()} and {edge.Second.ToString().ToLowerInvariant()} can't be joined when they're marked in different places.",
            spec => SharedMarker(spec, Settings.SubordinatorOf(edge.First), Settings.SubordinatorOf(edge.Second), Settings.SubordinatorEdge(edge.First, edge.Second)))),
        /*
          and where an edge is left free, a draw that joins subordinators the spec allows different
          positions has to give their region one they all allow, or writing each position in turn
          would move the whole region to the last one written
         */
        SpecRule.On(Settings.SubordinationStage, spec => SubordinatorMap.Subordinators.Any(s => spec.Allowed(Settings.SubordinatorOf(s)) is not null)
            ? [new Condition<Subordination>(Settings.SubordinationStage, "Subordinators that share a marker need a position the spec allows each of them.",
                (subordination, _) => SubordinatorMap.Subordinators.All(s => spec.Allowed(Settings.SubordinatorOf(s)) is not { } allowed || allowed.Contains(subordination.Markers[s].Position)),
                subordination => WithSharedPositions(spec, subordination))]
            : []),
        //modalities joined by an edge share a marker, so an edge fixed joined narrows them to the
        //kinds they can share, and two that can't share one can't be joined
        .. ModalityMap.Edges.Select(e => (e.First, e.Second)).Select(edge => new SpecRule.Narrowing(
            $"Modalities joined by an edge share a marker, so {edge.First.Name.ToLowerInvariant()} and {edge.Second.Name.ToLowerInvariant()} can't be joined when they're marked differently.",
            spec => SharedMarker(spec, Settings.ModalOf(edge.First), Settings.ModalOf(edge.Second), Settings.ModalityEdge(edge.First, edge.Second)))),
        //only an affix on the possessed noun has a side
        SpecRule.Together(Settings.PossessiveMarking, Settings.PossessiveAffixes,
            (marking, position) => position == ExponentPosition.Suffix || marking is PossessiveMarking.Head or PossessiveMarking.Double,
            "Possessive affixes can only be prefixes where possession is marked on the possessed noun: Head or Double."),

        //a configuration's marker belongs to its region, and a region with At in it has none
        SpecRule.On(Settings.ConfigurationRegionStage, ConfigurationConditions),

        //a change that works on codas needs syllables that have them, and devoicing or weakening a
        //coda needs one that can be an obstruent
        .. new[] { Settings.FinalDevoicing, Settings.CodaDevoicing, Settings.CodaWeakening, Settings.CompensatoryLengthening, Settings.ClosedSyllableLaxing, Settings.Syncope, Settings.PretonicSyncope,
            Settings.Apocope }
            .Select(change => SpecRule.Together(Settings.SyllableTemplate, change, (template, on) => !on || HasCodas(template),
                $"{change.Name} needs a syllable template with a coda: CVF, CLVF, SCVF or SCLVF.")),
        .. new[] { Settings.CodaDevoicing, Settings.CodaWeakening }.Select(change => SpecRule.Together(Settings.CodaRestriction, change,
            (codas, on) => !on || codas == CodaRestriction.Any, $"{change.Name} needs codas that can be any consonant, so coda-restriction must be Any.")),
        //only a stored accent retracts, so retraction needs accents to store
        SpecRule.Together(Settings.FreeStress, Settings.AccentRetraction, (free, on) => !on || free,
            "accent-retraction needs free stress, since only an accent a root stores moves."),
        //syllable contact only matters across a coda, and a language repairs only the contacts it restricts
        SpecRule.Together(Settings.SyllableTemplate, Settings.SyllableContact, (template, contact) => contact == SyllableContact.Any || HasCodas(template),
            "A syllable contact restriction needs a syllable template with a coda: CVF, CLVF, SCVF or SCLVF."),
        SpecRule.Together(Settings.SyllableContact, Settings.ContactRepair, (contact, repair) => repair == ContactRepair.None || contact != SyllableContact.Any,
            "A contact repair needs a syllable contact restriction, so syllable-contact can't be Any."),
        //and a change needs the sounds it works with
        .. VowelNeeds.SelectMany(need => NeedRules(need, Settings.VowelInventory)),
        .. ConsonantNeeds.SelectMany(need => NeedRules(need, Settings.ConsonantInventory)),

        //how many sounds an inventory has, last, so that a count written in keeps every sound the
        //spec requires and needs
        .. CountRules(Settings.VowelInventory, VowelNeeds),
        .. CountRules(Settings.ConsonantInventory, ConsonantNeeds),
    ];

    /// <summary>
    /// The spec with its rules applied: each setting narrowed until no rule narrows any further, and
    /// the conditions the narrowed spec puts on what stages draw, with anything that can't be met.
    /// </summary>
    public static Implications Close(LanguageSpec spec)
    {
        var narrowed = spec;
        List<string> problems = [];
        /*
          a rule returns the spec it was given when it has nothing to narrow. One that returns a new
          spec every time would keep this going forever, so a few hundred passes, far more than any
          spec needs, means a rule is at fault
         */
        SpecRule.Narrowing? lastChanged = null;
        for (var (changed, pass) = (true, 0); changed && problems.Count == 0; pass++)
        {
            if (pass == 500)
            {
                throw new InvalidOperationException($"The spec's rules never settled: this one kept changing it. {lastChanged?.Says}");
            }
            changed = false;
            foreach (var rule in All.OfType<SpecRule.Narrowing>())
            {
                switch (rule.Narrow(narrowed))
                {
                    case null:
                        problems.Add(rule.Says);
                        break;
                    case var next when next != narrowed:
                        (narrowed, changed, lastChanged) = (next, true, rule);
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

    /*
      each region of joined subordinators moved to the first position the spec allows all of them,
      where its own isn't one. A region with no such position is parted along the edges the spec
      leaves free, which its rules make sure leaves regions that have one, and the parts keep the
      region's form, homophones as languages have them
     */
    private static Subordination WithSharedPositions(LanguageSpec spec, Subordination subordination)
    {
        foreach (var region in SemanticMap.Regions(SubordinatorMap.Subordinators, subordination.Joined))
        {
            var setting = Settings.SubordinatorOf(region[0]);
            var allowed = region.Select(s => spec.Allowed(Settings.SubordinatorOf(s))).OfType<IReadOnlyList<object>>()
                .Aggregate((IEnumerable<object>)setting.AllValues!, (all, some) => all.Intersect(some)).ToList();
            if (allowed.Count == 0)
            {
                bool Kept((Subordinator First, Subordinator Second) edge) =>
                    !region.Contains(edge.First) || spec.Allowed(Settings.SubordinatorEdge(edge.First, edge.Second)) is [true];
                var parted = subordination with { Joined = [.. subordination.Joined.Where(Kept)] };
                return parted.Joined.Count < subordination.Joined.Count ? WithSharedPositions(spec, parted) : subordination;
            }
            if (!allowed.Contains(subordination.Markers[region[0]].Position))
            {
                subordination = setting.Write(subordination, [allowed[0]]);
            }
        }
        return subordination;
    }

    //two nodes of a semantic map joined by an edge share a marker, so an edge fixed joined narrows
    //them to the values they can share, and two that can't share one can't be joined
    private static LanguageSpec? SharedMarker(LanguageSpec spec, ISetting first, ISetting second, ISetting joined)
    {
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
    }

    //whether the spec has some configuration marked by a localization, or prefers them
    private static bool NeedsSpatialCase(LanguageSpec spec) =>
        Settings.ConfigurationMarkers.Append(Settings.ConfigurationMarking).Any(s => spec.Allowed(s) is [ConfigurationStrategy.Case]);

    /*
      each value a marker allows is tried with every arrangement of the map's edges, assuming the
      best of the stages before them, as a spec is checked. A spec fixing nothing about the map has
      nothing to narrow, which keeps checking it quick. It never refuses a spec itself.

      Trying markers one at a time finds every case the map forces, not just those it forces on one
      marker: configurations joined by edges the spec fixes share a marker, so a value that fits one
      of them fits them all, and an edge left free can always be left unjoined, keeping a group in a
      region of its own. A group said like at reaches it only through groups that are said like at
      too, which any value that fits has to allow already. So where every marker that allows a case
      has some other value that fits, those values all fit together, and no case is needed. Random
      specs bear this out. A map with edges that join groups some other way would need checking anew
     */
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

    //a setting whose values need certain sounds, given as what each needs, or null for one that needs
    //none. What a value needs only gets likelier to hold as sounds are added
    private sealed record SoundNeed<TPhoneme>(ISetting Setting, Func<object, Func<IReadOnlyCollection<TPhoneme>, bool>?> Needs, string Says) where TPhoneme : Phoneme
    {
        public static SoundNeed<TPhoneme> Of<T, TValue>(Setting<T, TValue> setting, Func<TValue, Func<IReadOnlyCollection<TPhoneme>, bool>?> needs, string says)
            where TValue : notnull => new(setting, value => needs((TValue)value), says);

        public bool Allows(object value, IReadOnlyCollection<TPhoneme> phonemes) => Needs(value) is not { } has || has(phonemes);

        /// <summary>What a spec makes an inventory have, where the setting allows only values that need sounds.</summary>
        public Func<IReadOnlyCollection<TPhoneme>, bool>? In(LanguageSpec spec) =>
            spec.Allowed(Setting) is { } values && values.All(v => Needs(v) is not null) ? phonemes => values.Any(v => Allows(v, phonemes)) : null;
    }

    /*
      an inventory can meet a need exactly when every sound the spec doesn't forbid, all together,
      does. Where the inventory is fixed exactly, or sounds forbidden, a value no inventory the spec
      allows can have is narrowed away, and if every value left needs sounds, exact inventories are
      narrowed to those that have them. Where the setting allows only values that need sounds, the
      inventory is drawn again until it has the sounds of one of them, and failing that, has the
      commonest sounds the spec allows added until it does
     */
    private static IEnumerable<SpecRule> NeedRules<TPhoneme>(SoundNeed<TPhoneme> need, InventorySettings<TPhoneme> inventory) where TPhoneme : Phoneme
    {
        var (setting, says) = (need.Setting, need.Says);
        bool Allows(object value, IReadOnlyList<TPhoneme> phonemes) => need.Allows(value, phonemes);
        Func<IReadOnlyCollection<TPhoneme>, bool>? needs(object value) => need.Needs(value);
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
            if (exact is not null && kept.All(v => needs(v) is not null))
            {
                List<object> having = [.. exact.Where(i => kept.Any(v => Allows(v, (IReadOnlyList<TPhoneme>)i)))];
                narrowed = having.Count < exact.Count ? narrowed.With(inventory.Exact, having) : narrowed;
            }
            return narrowed;
        });
        yield return SpecRule.On(inventory.Stage, spec => spec.Allowed(setting) is { } values && values.All(v => needs(v) is not null)
            ? [new Condition<IReadOnlyList<TPhoneme>>(inventory.Stage, says, (phonemes, _) => values.Any(v => Allows(v, phonemes)),
                phonemes => inventory.Allowed(spec).Aggregate(phonemes, (added, sound) => values.Any(v => Allows(v, added)) ? added : SettingWrites.WithSound(added, sound, true)))]
            : []);
    }

    /*
      an inventory needs a sound, and an exact one has to keep to the sounds the spec requires and
      forbids, which it then says all there is to say about. Where some sounds are forbidden and none
      required, a draw with the forbidden ones taken out could be left with none, so the commonest
      sound allowed is put back
     */
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

    /*
      a count has to leave room for the sounds the spec requires and its sound changes need, and can't
      be more than the sounds it allows; an exact inventory says what its count is. A count no draw
      gives is written in by cutting the rarest sounds the spec and its needs can spare, or adding the
      commonest it allows
     */
    private static IEnumerable<SpecRule> CountRules<T>(InventorySettings<T> inventory, IReadOnlyList<SoundNeed<T>> needs) where T : Phoneme
    {
        var name = inventory.Count.Name;
        yield return new SpecRule.Narrowing($"An exact list of {inventory.Noun}s has to have as many as {name} allows.", spec =>
        {
            if (spec.Allowed(inventory.Count) is not { } counts || spec.Allowed(inventory.Exact) is not { } exact)
            {
                return spec;
            }
            List<object> fitting = [.. exact.Where(i => counts.Contains(((IReadOnlyList<T>)i).Count))];
            return fitting.Count == 0 ? null : (fitting.Count < exact.Count ? spec.With(inventory.Exact, fitting) : spec).Unfix(inventory.Count);
        });
        yield return new SpecRule.Narrowing($"{name} has to leave room for the {inventory.Noun}s the spec requires and its sound changes need, and can't be more than it allows.", spec =>
        {
            if (spec.Allowed(inventory.Count) is not { } counts || spec.Allowed(inventory.Exact) is not null
                || Smallest(inventory, spec, AllNeeds(needs, spec)) is not { } smallest)
            {
                return spec;
            }
            var most = inventory.Allowed(spec).Count;
            List<object> kept = [.. counts.Where(c => (int)c >= smallest.Count && (int)c <= most)];
            return kept.Count == 0 ? null : kept.Count < counts.Count ? spec.With(inventory.Count, kept) : spec;
        });
        yield return SpecRule.On(inventory.Stage, spec =>
        {
            if (spec.Allowed(inventory.Count) is not { } allowed || spec.Allowed(inventory.Exact) is not null)
            {
                return [];
            }
            List<int> counts = [.. allowed.Cast<int>()];
            var (required, sounds, need) = (inventory.Required(spec), inventory.Allowed(spec), AllNeeds(needs, spec));
            bool Fits(IReadOnlyList<T> drawn) => counts.Contains(drawn.Count) && need(drawn) && required.All(drawn.Contains) && drawn.All(sounds.Contains);
            return [new Condition<IReadOnlyList<T>>(inventory.Stage, $"{name} is one the spec allows.", (drawn, _) => Fits(drawn),
                drawn => Fit(drawn, inventory, spec, counts, need))];
        });
    }

    //what every need the spec makes of an inventory asks of it
    private static Func<IReadOnlyCollection<T>, bool> AllNeeds<T>(IReadOnlyList<SoundNeed<T>> needs, LanguageSpec spec) where T : Phoneme
    {
        List<Func<IReadOnlyCollection<T>, bool>> active = [.. needs.Select(n => n.In(spec)).OfType<Func<IReadOnlyCollection<T>, bool>>()];
        return phonemes => active.All(a => a(phonemes));
    }

    /*
      the fewest sounds the spec allows that keep the ones it requires and meet its needs, commonest
      first, or null if even every sound it allows doesn't. The needs ask for a few sounds at most, so
      trying the smaller sets first ends quickly
     */
    private static IReadOnlyList<T>? Smallest<T>(InventorySettings<T> inventory, LanguageSpec spec, Func<IReadOnlyCollection<T>, bool> needs) where T : Phoneme
    {
        var required = inventory.Required(spec);
        List<T> others = [.. inventory.Allowed(spec).Except(required)];
        if (!needs([.. required, .. others]))
        {
            return null;
        }
        for (int size = 0; size <= others.Count; size++)
        {
            foreach (var chosen in Combinations(others, size))
            {
                List<T> candidate = [.. required, .. chosen];
                if (candidate.Count > 0 && needs(candidate))
                {
                    return [.. candidate.OrderBy(Generator.Rank)];
                }
            }
        }
        return null;
    }

    private static IEnumerable<IReadOnlyList<T>> Combinations<T>(IReadOnlyList<T> items, int size, int from = 0)
    {
        if (size == 0)
        {
            yield return [];
            yield break;
        }
        for (int i = from; i <= items.Count - size; i++)
        {
            foreach (var rest in Combinations(items, size - 1, i + 1))
            {
                yield return [items[i], .. rest];
            }
        }
    }

    /*
      an inventory brought to a count the spec allows, the nearest to what it has, keeping the sounds
      the spec requires and meeting its needs: the rarest sounds it can spare are cut, and the
      commonest it allows added. Cutting one at a time can leave more than the count where a need
      has two ways to be met, as ə or else a mid vowel with i and u, so then it starts over from the
      fewest sounds that meet it
     */
    private static IReadOnlyList<T> Fit<T>(IReadOnlyList<T> drawn, InventorySettings<T> inventory, LanguageSpec spec, IReadOnlyList<int> counts, Func<IReadOnlyCollection<T>, bool> needs)
        where T : Phoneme
    {
        var (required, sounds) = (inventory.Required(spec), inventory.Allowed(spec));
        IReadOnlyList<T> result = required.Aggregate((IReadOnlyList<T>)[.. drawn.Where(sounds.Contains)], (added, sound) => SettingWrites.WithSound(added, sound, true));
        result = sounds.Aggregate(result, (added, sound) => needs(added) ? added : SettingWrites.WithSound(added, sound, true));
        var target = counts.MinBy(c => Math.Abs(c - result.Count));
        while (result.Count > target && result.LastOrDefault(s => !required.Contains(s) && needs([.. result.Where(r => r != s)])) is { } spare)
        {
            result = [.. result.Where(r => r != spare)];
        }
        if (result.Count > target)
        {
            result = Smallest(inventory, spec, needs)!;
        }
        return sounds.Aggregate(result, (added, sound) => added.Count >= target ? added : SettingWrites.WithSound(added, sound, true));
    }

    private static Case LocationCase(IReadOnlyList<RoleRegion> regions) => regions.First(r => r.Contains(Role.Location)).Case!;

    /*
      a configuration can be said like at only in a region with at, have a marker of its own only in
      a region without, and be marked by case only where the language can have spatial cases, which
      is assumed when nothing's been drawn yet
     */
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
