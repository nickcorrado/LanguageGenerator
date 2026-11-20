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
    private const string Clauses = "Clauses";
    private const string Nouns = "Noun phrases";
    private const string Comparing = "Comparison";
    private const string Changes = "Sound changes";

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

    /// <summary>
    /// Whether the language has each sound: true requires it and false forbids it, leaving the rest
    /// of the inventory to be drawn. A spec gives them as "vowels": { "require": [...], "forbid": [...] }.
    /// </summary>
    public static IReadOnlyList<Setting<IReadOnlyList<Vowel>, bool>> VowelSounds { get; } =
        [.. Vowel.All.Select(v => VowelStage.Declare($"vowels:{v.IpaSymbol}", Sounds, l => l.Contains(v), (l, on) => SettingWrites.WithSound(l, v, on), YesNo))];
    public static IReadOnlyList<Setting<IReadOnlyList<Consonant>, bool>> ConsonantSounds { get; } =
        [.. Consonant.All.Select(c => ConsonantStage.Declare($"consonants:{c.IpaSymbol}", Sounds, l => l.Contains(c), (l, on) => SettingWrites.WithSound(l, c, on), YesNo))];

    /// <summary>How many vowels and consonants the language has.</summary>
    public static Setting<IReadOnlyList<Vowel>, int> VowelCount { get; } =
        VowelStage.Declare("vowel-count", Sounds, l => l.Count, (l, n) => SettingWrites.WithCount(l, Vowel.All, n), new Count(Vowel.All.Count));
    public static Setting<IReadOnlyList<Consonant>, int> ConsonantCount { get; } =
        ConsonantStage.Declare("consonant-count", Sounds, l => l.Count, (l, n) => SettingWrites.WithCount(l, Consonant.All, n), new Count(Consonant.All.Count));

    public static InventorySettings<Vowel> VowelInventory { get; } = new("vowel", VowelStage, Vowels, [.. Vowel.All.Zip(VowelSounds)], VowelCount);
    public static InventorySettings<Consonant> ConsonantInventory { get; } = new("consonant", ConsonantStage, Consonants, [.. Consonant.All.Zip(ConsonantSounds)], ConsonantCount);

    /// <summary>The inventory a setting for one sound is part of, or null for any other setting.</summary>
    public static ISetting? InventoryOf(ISetting setting) =>
        VowelInventory.Sounds.Any(s => s.Has == setting) ? Vowels : ConsonantInventory.Sounds.Any(s => s.Has == setting) ? Consonants : null;

    /// <summary>For an inventory's setting, its sounds and the setting for each.</summary>
    public static IReadOnlyList<(Phoneme Sound, ISetting Has)> SoundsOf(ISetting inventory) =>
        inventory == Vowels ? [.. VowelInventory.Sounds.Select(s => ((Phoneme)s.Sound, (ISetting)s.Has))]
        : inventory == Consonants ? [.. ConsonantInventory.Sounds.Select(s => ((Phoneme)s.Sound, (ISetting)s.Has))]
        : [];

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

    public static Stage<SyllableContact> SyllableContactStage { get; } = new("syllable contact", l => l.Phonology.SyllableContact, null);
    /// <summary>What may meet across a syllable boundary, which only matters where syllables have codas.</summary>
    public static Setting<SyllableContact, SyllableContact> SyllableContact { get; } = SyllableContactStage.Declare("syllable-contact", Sounds,
        c => c, (_, c) => c, Choice<SyllableContact>.Of(Enum.GetValues<SyllableContact>()));

    public static Stage<Dropoff> DropoffStage { get; } = new("dropoff", l => l.Phonology.Dropoff, null);
    /// <summary>
    /// How steeply sounds get rarer down an inventory: each is (1 - dropoff) times as likely as the
    /// one before it, so a higher dropoff favors the first few more. Nothing draws it; it's 0.15
    /// unless a spec says otherwise.
    /// </summary>
    public static Setting<Dropoff, double> Dropoff { get; } = DropoffStage.Declare("dropoff", Sounds, d => d.Value, (_, d) => new(d), new Between(0, 1));

    //sound changes, which apply in a fixed order, so a spec says only which a language has

    public static Stage<IReadOnlyList<SoundChange>> SoundChangeStage { get; } = new("sound changes", l => l.Phonology.SoundChanges, null);
    public static Setting<IReadOnlyList<SoundChange>, bool> NasalAssimilation { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "nasal-assimilation", SoundChange.NasalAssimilation);
    public static Setting<IReadOnlyList<SoundChange>, bool> Degemination { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "degemination", SoundChange.Degemination);
    public static Setting<IReadOnlyList<SoundChange>, bool> IntervocalicVoicing { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "intervocalic-voicing", SoundChange.IntervocalicVoicing);
    public static Setting<IReadOnlyList<SoundChange>, bool> Lenition { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "lenition", SoundChange.Lenition);
    public static Setting<IReadOnlyList<SoundChange>, bool> VoicingAssimilation { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "voicing-assimilation", SoundChange.VoicingAssimilation);
    public static Setting<IReadOnlyList<SoundChange>, bool> FinalDevoicing { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "final-devoicing", SoundChange.FinalDevoicing);
    public static Setting<IReadOnlyList<SoundChange>, VowelHarmony> VowelHarmony { get; } = ChangeKind(SoundChangeStage, SoundChange.SoundChangeOrder, "vowel-harmony", SoundChange.Harmonies);
    public static Setting<IReadOnlyList<SoundChange>, bool> GlideFormation { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "glide-formation", SoundChange.GlideFormation);
    public static Setting<IReadOnlyList<SoundChange>, HiatusResolution> HiatusResolution { get; } = ChangeKind(SoundChangeStage, SoundChange.SoundChangeOrder, "hiatus-resolution", SoundChange.HiatusResolutions);

    public static Stage<IReadOnlyList<SoundChange>> SyllableChangeStage { get; } = new("syllable changes", l => l.Phonology.SyllableChanges, null);
    public static Setting<IReadOnlyList<SoundChange>, bool> CodaDevoicing { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "coda-devoicing", SoundChange.CodaDevoicing);
    public static Setting<IReadOnlyList<SoundChange>, bool> CodaWeakening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "coda-weakening", SoundChange.CodaWeakening);
    public static Setting<IReadOnlyList<SoundChange>, bool> CompensatoryLengthening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "compensatory-lengthening", SoundChange.CompensatoryLengthening);
    public static Setting<IReadOnlyList<SoundChange>, bool> ClosedSyllableLaxing { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "closed-syllable-laxing", SoundChange.ClosedSyllableLaxing);
    public static Setting<IReadOnlyList<SoundChange>, bool> OpenSyllableLengthening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "open-syllable-lengthening", SoundChange.OpenSyllableLengthening);
    public static Setting<IReadOnlyList<SoundChange>, bool> UnstressedShortening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "unstressed-shortening", SoundChange.UnstressedShortening);
    public static Setting<IReadOnlyList<SoundChange>, bool> Syncope { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "syncope", SoundChange.Syncope);
    public static Setting<IReadOnlyList<SoundChange>, UnstressedVowels> UnstressedVowels { get; } = ChangeKind(SyllableChangeStage, SoundChange.SyllableChangeOrder, "unstressed-vowels", SoundChange.UnstressedChanges);
    public static Setting<IReadOnlyList<SoundChange>, ContactRepair> ContactRepair { get; } = ChangeKind(SyllableChangeStage, SoundChange.SyllableChangeOrder, "contact-repair", SoundChange.ContactRepairs);

    private static Setting<IReadOnlyList<SoundChange>, bool> Change(Stage<IReadOnlyList<SoundChange>> stage, IReadOnlyList<SoundChange> order, string name, SoundChange change) =>
        stage.Declare(name, Changes, c => c.Contains(change), (c, on) => SettingWrites.WithChange(c, order, change, on), YesNo);

    private static Setting<IReadOnlyList<SoundChange>, TKind> ChangeKind<TKind>(Stage<IReadOnlyList<SoundChange>> stage, IReadOnlyList<SoundChange> order, string name,
        IReadOnlyList<(TKind Kind, SoundChange? Change)> kinds) where TKind : struct, Enum =>
        stage.Declare(name, Changes, c => SettingWrites.ChangeKindOf(c, kinds), (c, kind) => SettingWrites.WithChangeKind(c, order, kinds, kind), Choice<TKind>.Of(Enum.GetValues<TKind>()));

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

    //clauses

    public static Stage<Negation> NegationStage { get; } = new("negation", l => l.Negation, null);
    public static Setting<Negation, NegationStrategy> Negation { get; } = NegationStage.Declare("negation", Clauses,
        n => n.Strategy, SettingWrites.WithNegation, Choice<NegationStrategy>.Of(Enum.GetValues<NegationStrategy>()));

    public static Stage<Questions> QuestionStage { get; } = new("questions", l => l.Questions, null);
    public static Setting<Questions, PolarQuestion> PolarQuestions { get; } = QuestionStage.Declare("polar-questions", Clauses,
        SettingWrites.PolarQuestionOf, SettingWrites.WithPolarQuestion, Choice<PolarQuestion>.Of(Enum.GetValues<PolarQuestion>()));
    /// <summary>Whether a question word goes first in its clause, as English who does, or stays where what it asks for would be.</summary>
    public static Setting<Questions, bool> FrontsQuestionWords { get; } = QuestionStage.Declare("fronts-question-words", Clauses,
        q => q.FrontsQuestionWords, (q, f) => q with { FrontsQuestionWords = f }, YesNo);

    public static Stage<Passive?> PassiveStage { get; } = new("passive", l => l.Passive, null);
    public static Setting<Passive?, bool> HasPassive { get; } = PassiveStage.Declare("passive", Clauses,
        p => p is not null, SettingWrites.WithPassive, YesNo);

    public static Stage<Modals> ModalStage { get; } = new("modals", l => l.Modals, null);
    /// <summary>Whether each edge of the modality map joins its two modalities, so they share a marker.</summary>
    public static IReadOnlyList<Setting<Modals, bool>> ModalityEdges { get; } =
    [
        .. ModalityMap.Edges.Select(e => (e.First, e.Second)).Select(edge => ModalStage.Declare(
            $"modalities-{edge.First.Name.ToLowerInvariant()}-{edge.Second.Name.ToLowerInvariant()}", Clauses,
            m => m.Joined.Contains(edge), (m, joined) => SettingWrites.WithModalEdge(m, edge, joined), YesNo)),
    ];
    /// <summary>How each modality is marked: by an affix on the verb, a modal verb or a particle.</summary>
    public static IReadOnlyList<Setting<Modals, ModalKind>> ModalMarkers { get; } =
    [
        .. ModalityMap.Modalities.Select(modality => ModalStage.Declare($"{modality.Name.ToLowerInvariant()}-modal", Clauses,
            m => SettingWrites.KindOf(m.Markers[modality]), (m, kind) => SettingWrites.WithModalKind(m, modality, kind),
            Choice<ModalKind>.Of(Enum.GetValues<ModalKind>()))),
    ];

    //noun phrases

    public static Stage<Possession> PossessionStage { get; } = new("possession", l => l.Possession, null);
    public static Setting<Possession, PossessiveMarking> PossessiveMarking { get; } = PossessionStage.Declare("possession", Nouns,
        p => p.Marking, SettingWrites.WithPossession, Choice<PossessiveMarking>.Of(Enum.GetValues<PossessiveMarking>()));
    /// <summary>Which side of the possessed noun its affix agreeing with the possessor goes on, where there's one.</summary>
    public static Setting<Possession, ExponentPosition> PossessiveAffixes { get; } = PossessionStage.Declare("possessive-affixes", Nouns,
        p => p.AffixPosition, (p, position) => p with { AffixPosition = position },
        new Choice<ExponentPosition>([("Prefix", ExponentPosition.Prefix), ("Suffix", ExponentPosition.Suffix)]));

    public static Stage<GenderSystem> GenderStage { get; } = new("genders", l => l.Genders, null);
    public static Setting<GenderSystem, NounGenders> Genders { get; } = GenderStage.Declare("genders", Nouns,
        g => g.Genders, SettingWrites.WithGenders, Choice<NounGenders>.Of(Enum.GetValues<NounGenders>()));
    public static Setting<GenderSystem, GenderAssignment> GenderAssignment { get; } = GenderStage.Declare("gender-assignment", Nouns,
        g => g.Assignment, SettingWrites.WithAssignment, Choice<GenderAssignment>.Of(Enum.GetValues<GenderAssignment>()));

    //comparison

    public static Stage<Comparison> ComparisonStage { get; } = new("comparison", l => l.Comparison, null);
    public static Setting<Comparison, ComparisonKind> Comparison { get; } = ComparisonStage.Declare("comparison", Comparing,
        SettingWrites.ComparisonKindOf, SettingWrites.WithComparison, Choice<ComparisonKind>.Of(Enum.GetValues<ComparisonKind>()));

    public static Stage<DegreeMarking> DegreeStage { get; } = new("degree", l => l.Degree, null);
    public static Setting<DegreeMarking, MarkerKind> Comparative { get; } = DegreeStage.Declare("comparative", Comparing,
        d => d.Marker.Value is DegreeAffix ? MarkerKind.Affix : MarkerKind.Word, SettingWrites.WithComparative, Choice<MarkerKind>.Of(Enum.GetValues<MarkerKind>()));

    public static Stage<Similative> SimilativeStage { get; } = new("similative", l => l.Similative, null);
    public static Setting<Similative, MarkerKind> Similative { get; } = SimilativeStage.Declare("similative", Comparing,
        s => s.Value is SimilativeAffix ? MarkerKind.Affix : MarkerKind.Word, SettingWrites.WithSimilative, Choice<MarkerKind>.Of(Enum.GetValues<MarkerKind>()));

    /// <summary>Every stage with settings, in the order they're declared.</summary>
    public static IReadOnlyList<IStage> Stages { get; } =
    [
        VowelStage, ConsonantStage, SyllableStage, StressStage, ClusterRepairStage, SyllableContactStage, DropoffStage, SoundChangeStage, SyllableChangeStage, WordOrderStage, MorphologyStage, AlignmentStage,
        RoleRegionStage, CaseStage, ConfigurationRegionStage, ConfigurationPlanStage, NegationStage, QuestionStage, PassiveStage, ModalStage,
        PossessionStage, GenderStage, ComparisonStage, DegreeStage, SimilativeStage,
    ];

    /// <summary>Every setting, in the order they're declared.</summary>
    public static IReadOnlyList<ISetting> All { get; } =
    [
        .. VowelStage.Settings, .. ConsonantStage.Settings, .. SyllableStage.Settings, .. StressStage.Settings, .. ClusterRepairStage.Settings,
        .. SyllableContactStage.Settings, .. DropoffStage.Settings, .. SoundChangeStage.Settings, .. SyllableChangeStage.Settings, .. WordOrderStage.Settings, .. MorphologyStage.Settings, .. AlignmentStage.Settings, .. RoleRegionStage.Settings,
        .. CaseStage.Settings, .. ConfigurationRegionStage.Settings, .. ConfigurationPlanStage.Settings, .. NegationStage.Settings,
        .. QuestionStage.Settings, .. PassiveStage.Settings, .. ModalStage.Settings, .. PossessionStage.Settings, .. GenderStage.Settings, .. ComparisonStage.Settings,
        .. DegreeStage.Settings, .. SimilativeStage.Settings,
    ];

    /// <summary>
    /// The setting a spec names this way, or null if there's none. A sound's setting has no name of
    /// its own in a spec, which gives it in its inventory's.
    /// </summary>
    public static ISetting? Named(string name) => All.FirstOrDefault(s => s.Name == name && InventoryOf(s) is null);

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

    /// <summary>The setting for how a modality is marked.</summary>
    public static Setting<Modals, ModalKind> ModalOf(Modality modality) => ModalMarkers[ModalityMap.Modalities.ToList().IndexOf(modality) is var i and >= 0
        ? i : throw new ArgumentException($"{modality.Name} isn't a modality on the map.")];

    /// <summary>The setting for an edge of the modality map.</summary>
    public static Setting<Modals, bool> ModalityEdge(Modality first, Modality second) =>
        ModalityEdges[ModalityMap.Edges.Select(e => (e.First, e.Second)).ToList().IndexOf((first, second)) is var i and >= 0
            ? i : throw new ArgumentException($"The modality map has no edge from {first.Name} to {second.Name}.")];

    /// <summary>The setting for how a configuration is marked.</summary>
    public static Setting<ConfigurationPlan, ConfigurationStrategy> MarkerOf(Configuration configuration) => configuration == Configuration.At
        ? throw new ArgumentException("At has no marker: it's what the others are said like.")
        : ConfigurationMarkers[ConfigurationMap.Configurations.ToList().IndexOf(configuration) - 1];

    /// <summary>A name as a spec writes it, as in-front for InFront.</summary>
    public static string Kebab<T>(T value) where T : struct, Enum =>
        string.Concat(value.ToString().Select((c, i) => char.IsUpper(c) && i > 0 ? $"-{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}"));
}

/// <summary>
/// The settings of an inventory: the stage that draws it, the setting that fixes it exactly, one for
/// each sound it can have, which requires or forbids that sound, and how many sounds it has.
/// </summary>
public sealed record InventorySettings<T>(string Noun, Stage<IReadOnlyList<T>> Stage, Setting<IReadOnlyList<T>, IReadOnlyList<T>> Exact,
    IReadOnlyList<(T Sound, Setting<IReadOnlyList<T>, bool> Has)> Sounds, Setting<IReadOnlyList<T>, int> Count) where T : Phoneme
{
    /// <summary>The sounds a spec requires.</summary>
    public IReadOnlyList<T> Required(LanguageSpec spec) => [.. Sounds.Where(s => spec.Allowed(s.Has) is [true]).Select(s => s.Sound)];

    /// <summary>The sounds a spec doesn't forbid, commonest first, as they'd be written in.</summary>
    public IReadOnlyList<T> Allowed(LanguageSpec spec) => [.. Sounds.Where(s => spec.Allowed(s.Has) is not [false]).Select(s => s.Sound).OrderBy(Generator.Rank)];
}
