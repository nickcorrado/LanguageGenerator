namespace LanguageGenerator;

/// <summary>
/// Every setting a spec can fix, by the stage of Generate that draws it. A stage with no settings
/// here is always drawn freely.
/// </summary>
//static fields initialize in the order they're written, so each stage comes before its settings
public static class Settings
{
    private const string Sounds = "Sounds";
    private const string Order = "Word order";
    private const string Morphology = "Morphology";
    private const string Roles = "Roles";
    private const string Cases = "Cases";
    private const string Places = "Places";

    private static Choice<bool> YesNo { get; } = Choice<bool>.Of(true, false);

    //sounds

    public static Stage<IReadOnlyList<Vowel>> VowelStage { get; } = new("vowels", l => l.Phonology.Vowels, null);
    /// <summary>The vowels, commonest first.</summary>
    public static Setting<IReadOnlyList<Vowel>, IReadOnlyList<Vowel>> Vowels { get; } =
        VowelStage.Declare("vowels", Sounds, v => v, (_, v) => v, new PhonemeList<Vowel>(Vowel.All));

    public static Stage<IReadOnlyList<Consonant>> ConsonantStage { get; } = new("consonants", l => l.Phonology.Consonants, null);
    /// <summary>The consonants, commonest first.</summary>
    public static Setting<IReadOnlyList<Consonant>, IReadOnlyList<Consonant>> Consonants { get; } =
        ConsonantStage.Declare("consonants", Sounds, c => c, (_, c) => c, new PhonemeList<Consonant>(Consonant.All));

    public static Stage<SyllableShape> SyllableStage { get; } = new("syllable rule", l => new(l.Phonology.SyllableRule.Template, l.Phonology.SyllableRule.Codas), null);
    public static Setting<SyllableShape, SyllableTemplate> SyllableTemplate { get; } = SyllableStage.Declare("syllable-template", Sounds,
        s => s.Template, (s, t) => s with { Template = t },
        new Choice<SyllableTemplate>(
        [
            ("CV", SyllableTemplates.CV), ("CVF", SyllableTemplates.CVF), ("CLV", SyllableTemplates.CLV), ("CLVF", SyllableTemplates.CLVF),
            ("SCV", SyllableTemplates.SCV), ("SCVF", SyllableTemplates.SCVF), ("SCLVF", SyllableTemplates.SCLVF),
        ]));
    /// <summary>Which consonants can close a syllable, where the template lets one.</summary>
    public static Setting<SyllableShape, CodaRestriction> CodaRestriction { get; } = SyllableStage.Declare("coda-restriction", Sounds,
        s => s.Codas, (s, c) => s with { Codas = c }, Choice<CodaRestriction>.Of(Enum.GetValues<CodaRestriction>()));

    public static Stage<Stress> StressStage { get; } = new("stress", l => l.Phonology.Stress, null);
    public static Setting<Stress, Stress> Stress { get; } = StressStage.Declare("stress", Sounds,
        s => s, (_, s) => s, Choice<Stress>.Of(Enum.GetValues<Stress>()));

    public static Stage<ClusterRepair> ClusterRepairStage { get; } = new("cluster repair", l => l.Phonology.ClusterRepair, null);
    /// <summary>How a cluster the syllables can't hold, made by putting morphemes together, is fixed.</summary>
    public static Setting<ClusterRepair, ClusterRepair> ClusterRepair { get; } = ClusterRepairStage.Declare("cluster-repair", Sounds,
        r => r, (_, r) => r, Choice<ClusterRepair>.Of(Enum.GetValues<ClusterRepair>()));

    public static Stage<Dropoff> DropoffStage { get; } = new("dropoff", l => l.Phonology.Dropoff, null);
    /// <summary>
    /// How steeply sounds get rarer down an inventory: each is (1 - dropoff) times as likely as the
    /// one before it, so a higher dropoff favors the first few more. Nothing draws it; it's 0.15
    /// unless a spec says otherwise.
    /// </summary>
    public static Setting<Dropoff, double> Dropoff { get; } = DropoffStage.Declare("dropoff", Sounds, d => d.Value, (_, d) => new(d), new Between(0, 1));

    //word order

    public static Stage<WordOrder> WordOrderStage { get; } = new("word order", l => l.WordOrder, null);
    public static Setting<WordOrder, ConstituentWordOrder> ConstituentOrder { get; } = WordOrderStage.Declare("constituent-order", Order,
        w => w.ConstituentWordOrder, (w, o) => w with { ConstituentWordOrder = o }, Choice<ConstituentWordOrder>.Of(Enum.GetValues<ConstituentWordOrder>()));
    //named as WALS names these orders, since a direction alone says little
    public static Setting<WordOrder, HeadDirectionality> AdpositionOrder { get; } = WordOrderStage.Declare("adpositions", Order,
        w => w.AdpositionalPhraseDirectionality, (w, d) => w with { AdpositionalPhraseDirectionality = d },
        new Choice<HeadDirectionality>([("Prepositions", HeadDirectionality.Initial), ("Postpositions", HeadDirectionality.Final)]));
    public static Setting<WordOrder, HeadDirectionality> AdjectiveOrder { get; } = WordOrderStage.Declare("adjective-order", Order,
        w => w.NounPhraseDirectionality, (w, d) => w with { NounPhraseDirectionality = d },
        new Choice<HeadDirectionality>([("NounAdjective", HeadDirectionality.Initial), ("AdjectiveNoun", HeadDirectionality.Final)]));
    public static Setting<WordOrder, HeadDirectionality> DegreeWordOrder { get; } = WordOrderStage.Declare("degree-word-order", Order,
        w => w.AdjectivePhraseDirectionality, (w, d) => w with { AdjectivePhraseDirectionality = d },
        new Choice<HeadDirectionality>([("AdjectiveDegree", HeadDirectionality.Initial), ("DegreeAdjective", HeadDirectionality.Final)]));
    public static Setting<WordOrder, ObliquePosition> ObliquePosition { get; } = WordOrderStage.Declare("oblique-position", Order,
        w => w.ObliquePosition, (w, p) => w with { ObliquePosition = p }, Choice<ObliquePosition>.Of(Enum.GetValues<ObliquePosition>()));
    /// <summary>Whether adjectives after the noun mirror their order before it.</summary>
    public static Setting<WordOrder, bool> MirrorsAdjectives { get; } = WordOrderStage.Declare("mirrors-adjectives", Order,
        w => w.MirrorsAdjectives, (w, m) => w with { MirrorsAdjectives = m }, YesNo);

    //morphology

    public static Stage<MorphologicalProfile> MorphologyStage { get; } = new("morphology", l => l.MorphologicalProfile, null);
    public static Setting<MorphologicalProfile, Fusion> Fusion { get; } = MorphologyStage.Declare("fusion", Morphology,
        m => m.Fusion, (m, f) => m with { Fusion = f }, Choice<Fusion>.Of(Enum.GetValues<Fusion>()));
    public static Setting<MorphologicalProfile, Affixation> Affixation { get; } = MorphologyStage.Declare("affixation", Morphology,
        m => m.Affixation, (m, a) => m with { Affixation = a }, Choice<Affixation>.Of(Enum.GetValues<Affixation>()));
    public static Setting<MorphologicalProfile, bool> CaseWithNumber { get; } = MorphologyStage.Declare("case-with-number", Morphology,
        m => m.Exponence.CaseWithNumber, (m, b) => m with { Exponence = m.Exponence with { CaseWithNumber = b } }, YesNo);
    public static Setting<MorphologicalProfile, bool> TamWithAgreement { get; } = MorphologyStage.Declare("tam-with-agreement", Morphology,
        m => m.Exponence.TamWithAgreement, (m, b) => m with { Exponence = m.Exponence with { TamWithAgreement = b } }, YesNo);
    public static Setting<MorphologicalProfile, bool> TenseWithAspect { get; } = MorphologyStage.Declare("tense-with-aspect", Morphology,
        m => m.Exponence.TenseWithAspect, (m, b) => m with { Exponence = m.Exponence with { TenseWithAspect = b } }, YesNo);
    public static Setting<MorphologicalProfile, bool> CaseWithLocalization { get; } = MorphologyStage.Declare("case-with-localization", Morphology,
        m => m.Exponence.CaseWithLocalization, (m, b) => m with { Exponence = m.Exponence with { CaseWithLocalization = b } }, YesNo);
    public static Setting<MorphologicalProfile, bool> ZeroSingular { get; } = MorphologyStage.Declare("zero-singular", Morphology,
        m => m.ZeroMarking.Singular, (m, b) => m with { ZeroMarking = m.ZeroMarking with { Singular = b } }, YesNo);
    public static Setting<MorphologicalProfile, bool> ZeroCoreCase { get; } = MorphologyStage.Declare("zero-core-case", Morphology,
        m => m.ZeroMarking.CoreCase, (m, b) => m with { ZeroMarking = m.ZeroMarking with { CoreCase = b } }, YesNo);
    public static Setting<MorphologicalProfile, bool> ZeroPresent { get; } = MorphologyStage.Declare("zero-present", Morphology,
        m => m.ZeroMarking.Present, (m, b) => m with { ZeroMarking = m.ZeroMarking with { Present = b } }, YesNo);
    public static Setting<MorphologicalProfile, ThirdPersonZero> ZeroThirdPerson { get; } = MorphologyStage.Declare("zero-third-person", Morphology,
        m => m.ZeroMarking.ThirdPerson, (m, z) => m with { ZeroMarking = m.ZeroMarking with { ThirdPerson = z } }, Choice<ThirdPersonZero>.Of(Enum.GetValues<ThirdPersonZero>()));

    //roles

    public static Stage<MorphosyntacticAlignment> AlignmentStage { get; } = new("alignment", l => l.MorphosyntacticAlignment, null);
    public static Setting<MorphosyntacticAlignment, MorphosyntacticAlignment> Alignment { get; } = AlignmentStage.Declare("alignment", Roles,
        a => a, (_, a) => a, Choice<MorphosyntacticAlignment>.Of(Enum.GetValues<MorphosyntacticAlignment>()));

    public static Stage<RoleRegions> RoleRegionStage { get; } = new("role regions", l => l.RoleRegions, () => new RoleRegions([]));
    /// <summary>
    /// Whether each edge of the role map joins its two roles, as "roles-recipient-goal" says that
    /// recipients and goals share a marker, unless something else joins them.
    /// </summary>
    public static IReadOnlyList<Setting<RoleRegions, bool>> RoleEdges { get; } =
    [
        .. RoleMap.Edges.Select(e => (e.First, e.Second)).Select(edge => RoleRegionStage.Declare($"roles-{Kebab(edge.First)}-{Kebab(edge.Second)}", Roles,
            r => r.Joined.Contains(edge),
            (r, joined) => new RoleRegions([.. RoleMap.Edges.Select(e => (e.First, e.Second)).Where(e => e == edge ? joined : r.Joined.Contains(e))]),
            YesNo)),
    ];

    //cases

    public static Stage<CaseSystem> CaseStage { get; } = new("cases", l => new(l.RoleRegions.Regions, l.Cases), null);
    /// <summary>Whether the language has each oblique case, and the vocative. Its core cases follow its alignment.</summary>
    public static IReadOnlyList<Setting<CaseSystem, bool>> CaseSettings { get; } =
    [
        .. RoleMap.Roles.Select(RoleMap.CaseFor).OfType<Case>().Append(Case.Vocative).Select(@case => CaseStage.Declare($"{@case.Name.ToLowerInvariant()}-case", Cases,
            s => s.Cases.Contains(@case),
            (s, has) => s with { Cases = CaseSystem.Ordered(has ? [.. s.Cases, @case] : s.Cases.Where(c => c != @case)) },
            YesNo)),
    ];

    //places

    public static Stage<ConfigurationRegions> ConfigurationRegionStage { get; } = new("configuration regions", l => l.Configurations.Plan.Map, () => new ConfigurationRegions([]));
    /// <summary>
    /// Whether each edge of the configuration map joins its two configurations, so they share a
    /// marker, or for an edge to at, are said as plainly as 'at'.
    /// </summary>
    public static IReadOnlyList<Setting<ConfigurationRegions, bool>> ConfigurationEdges { get; } =
    [
        .. ConfigurationMap.Edges.Select(e => (e.First, e.Second)).Select(edge => ConfigurationRegionStage.Declare($"configurations-{Kebab(edge.First)}-{Kebab(edge.Second)}", Places,
            m => m.Joined.Contains(edge),
            (m, joined) => new ConfigurationRegions([.. ConfigurationMap.Edges.Select(e => (e.First, e.Second)).Where(e => e == edge ? joined : m.Joined.Contains(e))]),
            YesNo)),
    ];

    public static Stage<ConfigurationPlan> ConfigurationPlanStage { get; } = new("configurations", l => l.Configurations.Plan, null);
    /// <summary>
    /// How each configuration but at is marked: by its region's marker, or as plainly as at where its
    /// region has at, which only a configuration the map joins to at, by some path, can be.
    /// </summary>
    public static IReadOnlyList<Setting<ConfigurationPlan, ConfigurationStrategy>> ConfigurationMarkers { get; } =
    [
        .. ConfigurationMap.Configurations.Skip(1).Select(configuration => ConfigurationPlanStage.Declare($"{Kebab(configuration)}-marker", Places,
            p => p.StrategyFor(configuration), (p, s) => p.WithStrategy(configuration, s),
            Choice<ConfigurationStrategy>.Of([.. Enum.GetValues<ConfigurationStrategy>().Where(s => s != ConfigurationStrategy.At
                || ConfigurationMap.Regions(ConfigurationMap.Edges.Select(e => (e.First, e.Second))).First(r => r.Contains(configuration)).Contains(Configuration.At))]))),
    ];
    /// <summary>The way of marking configurations a language prefers, which most of its regions follow.</summary>
    public static Setting<ConfigurationPlan, ConfigurationStrategy> ConfigurationMarking { get; } = ConfigurationPlanStage.Declare("configuration-marking", Places,
        p => p.Preferred, (p, s) => s == ConfigurationStrategy.Case && !p.CasesAllowed ? p : p with { Preferred = s },
        Choice<ConfigurationStrategy>.Of(ConfigurationStrategy.Adposition, ConfigurationStrategy.RelationalNoun, ConfigurationStrategy.Case));

    /// <summary>Every stage with settings, in the order they're declared.</summary>
    public static IReadOnlyList<IStage> Stages { get; } =
    [
        VowelStage, ConsonantStage, SyllableStage, StressStage, ClusterRepairStage, DropoffStage, WordOrderStage, MorphologyStage, AlignmentStage,
        RoleRegionStage, CaseStage, ConfigurationRegionStage, ConfigurationPlanStage,
    ];

    /// <summary>Every setting, in the order they're declared.</summary>
    public static IReadOnlyList<ISetting> All { get; } =
    [
        .. VowelStage.Settings, .. ConsonantStage.Settings, .. SyllableStage.Settings, .. StressStage.Settings, .. ClusterRepairStage.Settings,
        .. DropoffStage.Settings, .. WordOrderStage.Settings, .. MorphologyStage.Settings, .. AlignmentStage.Settings, .. RoleRegionStage.Settings,
        .. CaseStage.Settings, .. ConfigurationRegionStage.Settings, .. ConfigurationPlanStage.Settings,
    ];

    /// <summary>The setting a spec names this way, or null if there's none.</summary>
    public static ISetting? Named(string name) => All.FirstOrDefault(s => s.Name == name);

    /// <summary>The setting for an edge of the role map.</summary>
    public static Setting<RoleRegions, bool> RoleEdge(Role first, Role second) =>
        RoleEdges[RoleMap.Edges.Select(e => (e.First, e.Second)).ToList().IndexOf((first, second)) is var i and >= 0
            ? i : throw new ArgumentException($"The role map has no edge from {first} to {second}.")];

    /// <summary>The setting for an edge of the configuration map.</summary>
    public static Setting<ConfigurationRegions, bool> ConfigurationEdge(Configuration first, Configuration second) =>
        ConfigurationEdges[ConfigurationMap.Edges.Select(e => (e.First, e.Second)).ToList().IndexOf((first, second)) is var i and >= 0
            ? i : throw new ArgumentException($"The configuration map has no edge from {first} to {second}.")];

    /// <summary>The setting for whether a language has a case, oblique or vocative.</summary>
    public static Setting<CaseSystem, bool> HasCase(Case @case) => CaseSettings.FirstOrDefault(s => s.Name == $"{@case.Name.ToLowerInvariant()}-case")
        ?? throw new ArgumentException($"{@case.Name} has no setting: core cases follow the alignment.");

    /// <summary>The setting for how a configuration is marked.</summary>
    public static Setting<ConfigurationPlan, ConfigurationStrategy> MarkerOf(Configuration configuration) => configuration == Configuration.At
        ? throw new ArgumentException("At has no marker: it's what the others are said like.")
        : ConfigurationMarkers[ConfigurationMap.Configurations.ToList().IndexOf(configuration) - 1];

    /// <summary>A name as a spec writes it, as in-front for InFront.</summary>
    public static string Kebab<T>(T value) where T : struct, Enum =>
        string.Concat(value.ToString().Select((c, i) => char.IsUpper(c) && i > 0 ? $"-{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}"));
}
