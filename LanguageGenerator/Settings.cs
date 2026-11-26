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
            ("CV", SyllableTemplates.CV), ("CVF", SyllableTemplates.CVF), ("CLV", SyllableTemplates.CLV), ("CLVF", SyllableTemplates.CLVF), ("CGV", SyllableTemplates.CGV), ("CGVF", SyllableTemplates.CGVF),
            ("SCV", SyllableTemplates.SCV), ("SCVF", SyllableTemplates.SCVF), ("SCLVF", SyllableTemplates.SCLVF),
            ("CVFF", SyllableTemplates.CVFF), ("CLVFF", SyllableTemplates.CLVFF), ("SCLVFF", SyllableTemplates.SCLVFF),
        ]));
    /// <summary>Which consonants can close a syllable, where the template lets one.</summary>
    public static Setting<SyllableShape, CodaRestriction> CodaRestriction { get; } = SyllableStage.Declare("coda-restriction", Sounds,
        s => s.Codas, (s, c) => s with { Codas = c }, Choice<CodaRestriction>.Of(Enum.GetValues<CodaRestriction>()));

    public static Stage<StressSystem> StressStage { get; } = new("stress", l => l.Phonology.Stress, null);
    /// <summary>Where the rule puts stress, which free stress keeps for roots that store no accent.</summary>
    public static Setting<StressSystem, Stress> Stress { get; } = StressStage.Declare("stress", Sounds,
        s => s.Rule, (s, r) => s with { Rule = r }, Choice<Stress>.Of(Enum.GetValues<Stress>()));
    /// <summary>Whether each root stores which of its vowels is stressed, as in Russian.</summary>
    public static Setting<StressSystem, bool> FreeStress { get; } = StressStage.Declare("free-stress", Sounds,
        s => s.Free, (s, f) => s with { Free = f }, YesNo);

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
    public static Setting<IReadOnlyList<SoundChange>, bool> DiphthongFormation { get; } = Change(SoundChangeStage, SoundChange.SoundChangeOrder, "diphthong-formation", SoundChange.DiphthongFormation);
    public static Setting<IReadOnlyList<SoundChange>, HiatusResolution> HiatusResolution { get; } = ChangeKind(SoundChangeStage, SoundChange.SoundChangeOrder, "hiatus-resolution", SoundChange.HiatusResolutions);

    public static Stage<IReadOnlyList<SoundChange>> SyllableChangeStage { get; } = new("syllable changes", l => l.Phonology.SyllableChanges, null);
    public static Setting<IReadOnlyList<SoundChange>, bool> CodaDevoicing { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "coda-devoicing", SoundChange.CodaDevoicing);
    public static Setting<IReadOnlyList<SoundChange>, bool> CodaWeakening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "coda-weakening", SoundChange.CodaWeakening);
    public static Setting<IReadOnlyList<SoundChange>, bool> CompensatoryLengthening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "compensatory-lengthening", SoundChange.CompensatoryLengthening);
    public static Setting<IReadOnlyList<SoundChange>, bool> ClosedSyllableLaxing { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "closed-syllable-laxing", SoundChange.ClosedSyllableLaxing);
    public static Setting<IReadOnlyList<SoundChange>, bool> OpenSyllableLengthening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "open-syllable-lengthening", SoundChange.OpenSyllableLengthening);
    public static Setting<IReadOnlyList<SoundChange>, bool> UnstressedShortening { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "unstressed-shortening", SoundChange.UnstressedShortening);
    public static Setting<IReadOnlyList<SoundChange>, bool> UnstressedMonophthongization { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "unstressed-monophthongization", SoundChange.UnstressedMonophthongization);
    public static Setting<IReadOnlyList<SoundChange>, bool> Syncope { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "syncope", SoundChange.Syncope);
    public static Setting<IReadOnlyList<SoundChange>, bool> PretonicSyncope { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "pretonic-syncope", SoundChange.PretonicSyncope);
    public static Setting<IReadOnlyList<SoundChange>, bool> Apocope { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "apocope", SoundChange.Apocope);
    public static Setting<IReadOnlyList<SoundChange>, bool> Aphaeresis { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "aphaeresis", SoundChange.Aphaeresis);
    public static Setting<IReadOnlyList<SoundChange>, bool> VernersLaw { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "verners-law", SoundChange.VernersLaw);
    public static Setting<IReadOnlyList<SoundChange>, bool> AccentRetraction { get; } = Change(SyllableChangeStage, SoundChange.SyllableChangeOrder, "accent-retraction", SoundChange.AccentRetraction);
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

    //inflection

    public static Stage<Paradigm> NounParadigmStage { get; } = new("noun paradigm", l => l.Paradigms.Nouns, null);
    /// <summary>Which numbers nouns inflect for: none, singular and plural, or a dual besides.</summary>
    public static Setting<Paradigm, NounNumber> NounNumber { get; } = NounParadigmStage.Declare("noun-number", Morphology,
        SettingWrites.NounNumberOf, SettingWrites.Unwritable<Paradigm, NounNumber>("A noun paradigm with other numbers"),
        Choice<NounNumber>.Of(Enum.GetValues<NounNumber>()));

    public static Stage<Paradigm> VerbParadigmStage { get; } = new("verb paradigm", l => l.Paradigms.Verbs, null);
    /// <summary>Which tenses verbs inflect for.</summary>
    public static Setting<Paradigm, Tenses> Tenses { get; } = VerbParadigmStage.Declare("tense", Morphology,
        SettingWrites.TensesOf, SettingWrites.Unwritable<Paradigm, Tenses>("A verb paradigm with other tenses"),
        Choice<Tenses>.Of(Enum.GetValues<Tenses>()));
    /// <summary>Whether verbs inflect for perfective and imperfective aspect.</summary>
    public static Setting<Paradigm, bool> Aspect { get; } = VerbParadigmStage.Declare("aspect", Morphology,
        p => p.Categories.Has<Aspect>(), SettingWrites.Unwritable<Paradigm, bool>("A verb paradigm with or without aspect"), YesNo);
    /// <summary>Whether verbs agree with their subject in person and number.</summary>
    public static Setting<Paradigm, bool> VerbAgreement { get; } = VerbParadigmStage.Declare("verb-agreement", Morphology,
        p => p.Categories.Has<Person>(), SettingWrites.Unwritable<Paradigm, bool>("A verb paradigm with or without agreement"), YesNo);

    //pronouns

    public static Stage<PronounSystem> PronounStage { get; } = new("pronouns", l => l.Pronouns, null);
    /// <summary>How the pronouns say 'we': with one word, with the word for 'I', or apart for including the hearer.</summary>
    public static Setting<PronounSystem, FirstPersonPlural> FirstPersonPlural { get; } = PronounStage.Declare("first-person-plural", Nouns,
        p => p.FirstPersonPlural, SettingWrites.Unwritable<PronounSystem, FirstPersonPlural>("Pronouns saying 'we' another way"),
        Choice<FirstPersonPlural>.Of(Enum.GetValues<FirstPersonPlural>()));
    /// <summary>Which pronouns distinguish gender.</summary>
    public static Setting<PronounSystem, PronounGender> GenderedPronouns { get; } = PronounStage.Declare("gendered-pronouns", Nouns,
        p => p.GenderedPronouns, SettingWrites.Unwritable<PronounSystem, PronounGender>("Pronouns with gender in other persons"),
        Choice<PronounGender>.Of(Enum.GetValues<PronounGender>()));
    /// <summary>Whether a subject pronoun is usually left out.</summary>
    public static Setting<PronounSystem, bool> DropsSubjects { get; } = PronounStage.Declare("drops-subjects", Nouns,
        p => p.DropsSubjects, SettingWrites.WithDropsSubjects, YesNo);

    //predication

    public static Stage<Predication> PredicationStage { get; } = new("predication", l => l.Predication, null);
    /// <summary>Whether an adjective said of something acts like a verb, or needs a copula.</summary>
    public static Setting<Predication, PredicativeAdjectives> PredicativeAdjectives { get; } = PredicationStage.Declare("predicative-adjectives", Clauses,
        p => p.Adjectives, (p, adjectives) => p with { Adjectives = adjectives }, Choice<PredicativeAdjectives>.Of(Enum.GetValues<PredicativeAdjectives>()));
    /// <summary>Whether the copula is left out in the present, as in Russian ty durak 'you (are a) fool'.</summary>
    public static Setting<Predication, bool> ZeroCopula { get; } = PredicationStage.Declare("zero-copula", Clauses,
        p => p.ZeroCopula, (p, zero) => p with { ZeroCopula = zero }, YesNo);

    public static Stage<Predication> LocationalCopulaStage { get; } = new("locational copula", l => l.Predication, null);
    /// <summary>Whether the language has a verb of its own for where something is, like Spanish estar.</summary>
    public static Setting<Predication, bool> LocationalCopula { get; } = LocationalCopulaStage.Declare("locational-copula", Clauses,
        p => p.LocationalCopula is not null, SettingWrites.WithLocationalCopula, YesNo);

    public static Stage<AdjectiveAgreement> AdjectiveAgreementStage { get; } = new("adjective agreement", l => l.AdjectiveAgreement, null);
    /// <summary>Whether an adjective agrees with its noun, or the phrase is marked once at its edge.</summary>
    public static Setting<AdjectiveAgreement, AdjectiveAgreement> AdjectiveAgreement { get; } = AdjectiveAgreementStage.Declare("adjective-agreement", Nouns,
        a => a, (_, agreement) => agreement, Choice<AdjectiveAgreement>.Of(Enum.GetValues<AdjectiveAgreement>()));

    //clauses

    public static Stage<Commands> CommandStage { get; } = new("commands", l => l.Commands, null);
    /// <summary>Which numbers have an imperative of their own: both, one, the same one for both, or neither.</summary>
    public static Setting<Commands, ImperativeForms> Imperatives { get; } = CommandStage.Declare("imperatives", Clauses,
        SettingWrites.ImperativesOf, SettingWrites.WithImperatives, Choice<ImperativeForms>.Of(Enum.GetValues<ImperativeForms>()));
    /// <summary>Whether a prohibitive, 'don't eat', uses the imperative's form of the verb.</summary>
    public static Setting<Commands, bool> ProhibitiveUsesImperative { get; } = CommandStage.Declare("prohibitive-uses-imperative", Clauses,
        c => c.ProhibitiveUsesImperative, (c, uses) => c with { ProhibitiveUsesImperative = uses }, YesNo);
    /// <summary>Whether a prohibitive has a negator of its own, as Latin has nē beside nōn.</summary>
    public static Setting<Commands, bool> ProhibitiveNegator { get; } = CommandStage.Declare("prohibitive-negator", Clauses,
        c => c.Prohibitive is not null, SettingWrites.WithProhibitiveNegator, YesNo);

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

    public static Stage<Aspects> AspectStage { get; } = new("aspects", l => l.Aspects, null);
    /// <summary>How the perfect is marked: not at all, by an affix, a particle, or an auxiliary 'have', 'be' or 'finish'.</summary>
    public static Setting<Aspects, AspectKind> Perfect { get; } = AspectStage.Declare("perfect", Clauses,
        a => a.Perfect.Kind, (a, kind) => a with { Perfect = a.Perfect with { Kind = kind } }, Choice<AspectKind>.Of([.. LanguageGenerator.Aspects.PerfectKinds]));
    /// <summary>How the progressive is marked: not at all, by an affix, a particle, or an auxiliary 'be' or 'sit'.</summary>
    public static Setting<Aspects, AspectKind> Progressive { get; } = AspectStage.Declare("progressive", Clauses,
        a => a.Progressive.Kind, (a, kind) => a with { Progressive = a.Progressive with { Kind = kind } }, Choice<AspectKind>.Of([.. LanguageGenerator.Aspects.ProgressiveKinds]));
    /// <summary>How the habitual is marked: not at all, by an affix, a particle, or an auxiliary 'be'.</summary>
    public static Setting<Aspects, AspectKind> Habitual { get; } = AspectStage.Declare("habitual", Clauses,
        a => a.Habitual.Kind, (a, kind) => a with { Habitual = a.Habitual with { Kind = kind } }, Choice<AspectKind>.Of([.. LanguageGenerator.Aspects.HabitualKinds]));
    /// <summary>How beginning to do something is marked on the verb: not at all, where it's said with 'begin', by an affix or by a particle.</summary>
    public static Setting<Aspects, AspectKind> Inceptive { get; } = AspectStage.Declare("inceptive", Clauses,
        a => a.Inceptive.Kind, (a, kind) => a with { Inceptive = a.Inceptive with { Kind = kind } }, Choice<AspectKind>.Of([.. LanguageGenerator.Aspects.PhaseKinds]));
    /// <summary>How ceasing to do something is marked on the verb: not at all, where it's said with 'stop', by an affix or by a particle.</summary>
    public static Setting<Aspects, AspectKind> Cessative { get; } = AspectStage.Declare("cessative", Clauses,
        a => a.Cessative.Kind, (a, kind) => a with { Cessative = a.Cessative with { Kind = kind } }, Choice<AspectKind>.Of([.. LanguageGenerator.Aspects.PhaseKinds]));
    /// <summary>The settings of how each aspect is marked.</summary>
    public static IReadOnlyList<Setting<Aspects, AspectKind>> AspectMarkers { get; } = [Perfect, Progressive, Habitual, Inceptive, Cessative];

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

    public static Stage<NumeralSystem> NumeralStage { get; } = new("numerals", l => l.Numerals, null);
    /// <summary>How numbers are built past the first few words: on tens, on twenties, or not at all.</summary>
    public static Setting<NumeralSystem, NumeralBase> NumeralBase { get; } = NumeralStage.Declare("numeral-base", Nouns,
        n => n.Base, SettingWrites.Unwritable<NumeralSystem, NumeralBase>("Numerals on another base"), Choice<NumeralBase>.Of(Enum.GetValues<NumeralBase>()));
    /// <summary>Whether a numeral goes before its noun.</summary>
    public static Setting<NumeralSystem, bool> NumeralsBeforeNoun { get; } = NumeralStage.Declare("numerals-before-noun", Nouns,
        n => n.BeforeNoun, (n, before) => n with { BeforeNoun = before }, YesNo);
    /// <summary>How 'first', 'second' and the rest are said.</summary>
    public static Setting<NumeralSystem, OrdinalType> Ordinals { get; } = NumeralStage.Declare("ordinals", Nouns,
        n => n.Ordinals.Type, SettingWrites.Unwritable<NumeralSystem, OrdinalType>("Ordinals of another type"), Choice<OrdinalType>.Of(Enum.GetValues<OrdinalType>()));

    public static Stage<Demonstratives> DemonstrativeStage { get; } = new("demonstratives", l => l.Demonstratives, null);
    /// <summary>How many distances demonstratives tell apart, as WALS 41A has it.</summary>
    public static Setting<Demonstratives, DistanceContrast> DistanceContrast { get; } = DemonstrativeStage.Declare("distance-contrast", Nouns,
        d => d.Contrast, SettingWrites.Unwritable<Demonstratives, DistanceContrast>("Demonstratives telling apart another number of distances"),
        Choice<DistanceContrast>.Of(Enum.GetValues<DistanceContrast>()));
    /// <summary>Where a demonstrative goes: a word before or after its noun, or an affix on it, as WALS 88A has it.</summary>
    public static Setting<Demonstratives, ExponentPosition> DemonstrativePosition { get; } = DemonstrativeStage.Declare("demonstrative-position", Nouns,
        d => d.Position, SettingWrites.WithDemonstrativePosition,
        new Choice<ExponentPosition>([("Before", ExponentPosition.PrecedingParticle), ("After", ExponentPosition.FollowingParticle),
            ("Prefix", ExponentPosition.Prefix), ("Suffix", ExponentPosition.Suffix)]));
    /// <summary>Whether a demonstrative agrees with its noun's gender, as Spanish este and esta do.</summary>
    public static Setting<Demonstratives, bool> DemonstrativeGenderAgreement { get; } = DemonstrativeStage.Declare("demonstrative-gender-agreement", Nouns,
        d => d.AgreesInGender, SettingWrites.WithDemonstrativeGenderAgreement, YesNo);
    /// <summary>Whether a demonstrative standing alone has a stem of its own, rather than being the one beside a noun, as WALS 42A has it.</summary>
    public static Setting<Demonstratives, bool> DemonstrativePronounsOwnStem { get; } = DemonstrativeStage.Declare("demonstrative-pronouns-own-stem", Nouns,
        d => d.Pronouns.OwnStem, (d, own) => d with { Pronouns = d.Pronouns with { OwnStem = own } }, YesNo);
    /// <summary>Whether 'here' and 'there' are the demonstratives standing alone, marked as a place is, rather than words of their own.</summary>
    public static Setting<Demonstratives, bool> PlacesFromDemonstratives { get; } = DemonstrativeStage.Declare("here-from-demonstratives", Nouns,
        d => d.Places.FromDemonstratives, (d, from) => d with { Places = d.Places with { FromDemonstratives = from } }, YesNo);

    public static Stage<Articles> ArticleStage { get; } = new("articles", l => l.Articles, null);
    public static Setting<Articles, DefiniteArticle> DefiniteArticle { get; } = ArticleStage.Declare("definite-article", Nouns,
        SettingWrites.DefiniteOf, SettingWrites.WithDefinite, Choice<DefiniteArticle>.Of(Enum.GetValues<DefiniteArticle>()));
    public static Setting<Articles, IndefiniteArticle> IndefiniteArticle { get; } = ArticleStage.Declare("indefinite-article", Nouns,
        SettingWrites.IndefiniteOf, SettingWrites.WithIndefinite, Choice<IndefiniteArticle>.Of(Enum.GetValues<IndefiniteArticle>()));
    /// <summary>Whether an article agrees with its noun's gender, as Spanish el and la do.</summary>
    public static Setting<Articles, bool> ArticleGenderAgreement { get; } = ArticleStage.Declare("article-gender-agreement", Nouns,
        a => a.Definite?.AgreesInGender == true || a.Indefinite?.AgreesInGender == true, SettingWrites.WithArticleGenderAgreement, YesNo);
    /// <summary>Whether the definite article stays beside a demonstrative, as Greek aftó to spíti 'this the house' keeps it.</summary>
    public static Setting<Articles, bool> ArticleWithDemonstrative { get; } = ArticleStage.Declare("article-with-demonstrative", Nouns,
        a => a.WithDemonstrative, (a, with) => a with { WithDemonstrative = with }, YesNo);

    public static Stage<Indefinites> IndefiniteStage { get; } = new("indefinites", l => l.Indefinites, null);
    /// <summary>What indefinite pronouns like 'someone' are made from, as WALS 46A has it.</summary>
    public static Setting<Indefinites, IndefinitePronouns> IndefinitePronouns { get; } = IndefiniteStage.Declare("indefinite-pronouns", Nouns,
        i => i.Type, SettingWrites.WithIndefinitePronouns, Choice<IndefinitePronouns>.Of(Enum.GetValues<IndefinitePronouns>()));
    /// <summary>Whether a negative indefinite like 'nobody' takes the verb's negation too, as WALS 115A has it.</summary>
    public static Setting<Indefinites, NegativeIndefiniteNegation> NegativeIndefiniteNegation { get; } = IndefiniteStage.Declare("negative-indefinite-negation", Nouns,
        i => i.Negation, (i, negation) => i with { Negation = negation }, Choice<NegativeIndefiniteNegation>.Of(Enum.GetValues<NegativeIndefiniteNegation>()));
    /// <summary>Whether a quantifier like 'all' goes before its noun.</summary>
    public static Setting<Indefinites, bool> QuantifiersBeforeNoun { get; } = IndefiniteStage.Declare("quantifiers-before-noun", Nouns,
        i => i.QuantifiersBeforeNoun, (i, before) => i with { QuantifiersBeforeNoun = before }, YesNo);

    //joining clauses and nouns

    public static Stage<Conjunctions> ConjunctionStage { get; } = new("conjunctions", l => l.Conjunctions, null);
    /// <summary>Whether 'and' between nouns is the word for 'with', as in 'the dog with the cat slept'.</summary>
    public static Setting<Conjunctions, bool> AndIsWith { get; } = ConjunctionStage.Declare("and-is-with", Clauses,
        c => c.NounAnd.Value is AndWith, SettingWrites.WithAndIsWith, YesNo);
    /// <summary>How clauses are joined with 'and'.</summary>
    public static Setting<Conjunctions, ClauseAnd> ClauseAnd { get; } = ConjunctionStage.Declare("clause-and", Clauses,
        SettingWrites.ClauseAndOf, SettingWrites.WithClauseAnd, Choice<ClauseAnd>.Of(Enum.GetValues<ClauseAnd>()));

    public static Stage<Subordination> SubordinationStage { get; } = new("subordination", l => l.Subordination, null);
    //the edges come before the markers, since joining two regions gives them one marker, which a
    //marker's fixed position then goes on
    /// <summary>Whether each edge of the subordinator map joins its two subordinators, so they share a marker.</summary>
    public static IReadOnlyList<Setting<Subordination, bool>> SubordinatorEdges { get; } =
    [
        .. SubordinatorMap.Edges.Select(e => (e.First, e.Second)).Select(edge => SubordinationStage.Declare(
            $"subordinators-{Lower(edge.First)}-{Lower(edge.Second)}", Clauses,
            s => s.Joined.Contains(edge), (s, joined) => SettingWrites.WithSubordinatorEdge(s, edge, joined), YesNo)),
    ];
    /// <summary>Where each kind of subordinate clause is marked: by a word at its start or its end, or a suffix on its verb.</summary>
    public static IReadOnlyList<Setting<Subordination, ExponentPosition>> SubordinatorPositions { get; } =
    [
        .. SubordinatorMap.Subordinators.Select(subordinator => SubordinationStage.Declare($"{Lower(subordinator)}-subordinator", Clauses,
            s => s.Markers[subordinator].Position, (s, position) => SettingWrites.WithSubordinatorPosition(s, subordinator, position),
            new Choice<ExponentPosition>([("Initial", ExponentPosition.PrecedingParticle), ("Final", ExponentPosition.FollowingParticle), ("Suffix", ExponentPosition.Suffix)]))),
    ];
    /// <summary>Whether each kind of subordinate clause comes before the main clause.</summary>
    public static IReadOnlyList<Setting<Subordination, bool>> SubordinateClauseFirst { get; } =
    [
        .. SubordinatorMap.Subordinators.Select(subordinator => SubordinationStage.Declare($"{Lower(subordinator)}-clause-first", Clauses,
            s => s.Markers[subordinator].BeforeMain,
            (s, first) => s with { Markers = new Dictionary<Subordinator, SubordinatorMarking>(s.Markers) { [subordinator] = s.Markers[subordinator] with { BeforeMain = first } } },
            YesNo)),
    ];
    /// <summary>Whether a purpose clause's verb keeps its ordinary form, as WALS 125A asks.</summary>
    public static Setting<Subordination, Balance> PurposeBalance { get; } = SubordinationStage.Declare("purpose-balance", Clauses,
        s => s.Markers[Subordinator.Purpose].Balance, (s, balance) => SettingWrites.WithBalance(s, [Subordinator.Purpose], balance), Choice<Balance>.Of(Enum.GetValues<Balance>()));
    /// <summary>Whether a reason clause's verb keeps its ordinary form, as WALS 127A asks.</summary>
    public static Setting<Subordination, Balance> ReasonBalance { get; } = SubordinationStage.Declare("reason-balance", Clauses,
        s => s.Markers[Subordinator.Because].Balance, (s, balance) => SettingWrites.WithBalance(s, [Subordinator.Because], balance), Choice<Balance>.Of(Enum.GetValues<Balance>()));
    /// <summary>
    /// Whether the verb of a 'when' clause keeps its ordinary form, as WALS 126A asks, and so the
    /// other temporal and conditional clauses' verbs, which follow it.
    /// </summary>
    public static Setting<Subordination, Balance> TemporalBalance { get; } = SubordinationStage.Declare("temporal-balance", Clauses,
        s => s.Markers[Subordinator.When].Balance,
        (s, balance) => SettingWrites.WithBalance(s, [Subordinator.If, Subordinator.When, Subordinator.Before, Subordinator.After, Subordinator.Until], balance),
        Choice<Balance>.Of(Enum.GetValues<Balance>()));

    private static string Lower(Subordinator subordinator) => subordinator.ToString().ToLowerInvariant();

    public static Stage<Complementation> ComplementationStage { get; } = new("complementation", l => l.Complementation, null);
    /// <summary>
    /// Where the complementizer of a clause saying what's said, known or seen goes: nowhere, a word
    /// at its start or end, or a suffix on its verb.
    /// </summary>
    public static Setting<Complementation, ComplementizerPosition> PropositionComplementizer { get; } = ComplementationStage.Declare("proposition-complementizer", Clauses,
        c => c.Proposition.Position, (c, position) => SettingWrites.WithComplementizer(c, ComplementKind.Proposition, position),
        Choice<ComplementizerPosition>.Of(Enum.GetValues<ComplementizerPosition>()));
    /// <summary>Where the complementizer of a clause saying what's wanted, tried or begun goes.</summary>
    public static Setting<Complementation, ComplementizerPosition> EventComplementizer { get; } = ComplementationStage.Declare("event-complementizer", Clauses,
        c => c.Event.Position, (c, position) => SettingWrites.WithComplementizer(c, ComplementKind.Event, position),
        Choice<ComplementizerPosition>.Of(Enum.GetValues<ComplementizerPosition>()));
    /// <summary>Whether the verb of a clause saying what's said, known or seen keeps its ordinary form.</summary>
    public static Setting<Complementation, Balance> PropositionBalance { get; } = ComplementationStage.Declare("proposition-balance", Clauses,
        c => c.Proposition.Balance, (c, balance) => c with { Proposition = c.Proposition with { Balance = balance } }, Choice<Balance>.Of(Enum.GetValues<Balance>()));
    /// <summary>
    /// Whether the verb of a clause saying what's wanted, tried or begun keeps its ordinary form, as
    /// WALS 124A asks of 'want': deranked is its implicit subject, and balanced its overt one.
    /// </summary>
    public static Setting<Complementation, Balance> EventBalance { get; } = ComplementationStage.Declare("event-balance", Clauses,
        c => c.Event.Balance, (c, balance) => c with { Event = c.Event with { Balance = balance } }, Choice<Balance>.Of(Enum.GetValues<Balance>()));
    /// <summary>How a quotation is marked: not at all, with the complementizer, or with a word of its own.</summary>
    public static Setting<Complementation, QuotativeKind> Quotative { get; } = ComplementationStage.Declare("quotative", Clauses,
        c => c.Quotative.Kind, SettingWrites.WithQuotative, Choice<QuotativeKind>.Of(Enum.GetValues<QuotativeKind>()));
    /// <summary>Whether a complement clause that keeps its verb, or a quotation, goes last in its clause.</summary>
    public static Setting<Complementation, bool> ComplementsLast { get; } = ComplementationStage.Declare("complements-last", Clauses,
        c => c.Last, (c, last) => c with { Last = last }, YesNo);
    /// <summary>How 'want' is said: a verb, or an affix or particle on the verb of what's wanted, as WALS 124A has it.</summary>
    public static Setting<Complementation, DesiderativeKind> Desiderative { get; } = ComplementationStage.Declare("desiderative", Clauses,
        c => c.Desiderative.Kind, (c, kind) => c with { Desiderative = c.Desiderative with { Kind = kind } }, Choice<DesiderativeKind>.Of(Enum.GetValues<DesiderativeKind>()));

    public static Stage<Relativization> RelativizationStage { get; } = new("relativization", l => l.Relativization, null);
    /// <summary>Whether a relative clause goes before its noun.</summary>
    public static Setting<Relativization, bool> RelativeClausesFirst { get; } = RelativizationStage.Declare("relative-clauses-before-noun", Clauses,
        r => r.BeforeNoun, (r, before) => r with { BeforeNoun = before }, YesNo);
    /// <summary>How a relative clause says its noun as its subject, as WALS 122A has it.</summary>
    public static Setting<Relativization, RelativeStrategy> SubjectRelativization { get; } = RelativizationStage.Declare("subject-relativization", Clauses,
        r => r.Subject, (r, strategy) => r with { Subject = strategy }, Choice<RelativeStrategy>.Of(Enum.GetValues<RelativeStrategy>()));
    /// <summary>How a relative clause says its noun as its object.</summary>
    public static Setting<Relativization, RelativeStrategy> ObjectRelativization { get; } = RelativizationStage.Declare("object-relativization", Clauses,
        r => r.Object, (r, strategy) => r with { Object = strategy }, Choice<RelativeStrategy>.Of(Enum.GetValues<RelativeStrategy>()));
    /// <summary>How a relative clause says its noun in any other role, as WALS 123A has it.</summary>
    public static Setting<Relativization, RelativeStrategy> ObliqueRelativization { get; } = RelativizationStage.Declare("oblique-relativization", Clauses,
        r => r.Oblique, (r, strategy) => r with { Oblique = strategy }, Choice<RelativeStrategy>.Of(Enum.GetValues<RelativeStrategy>()));
    /// <summary>What marks a relative clause: nothing, a word at its edge beside its noun, or an affix on its verb.</summary>
    public static Setting<Relativization, RelativizerKind> Relativizer { get; } = RelativizationStage.Declare("relativizer", Clauses,
        r => r.Relativizer.Kind, (r, kind) => r with { Relativizer = r.Relativizer with { Kind = kind } }, Choice<RelativizerKind>.Of(Enum.GetValues<RelativizerKind>()));

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
        RoleRegionStage, CaseStage, ConfigurationRegionStage, ConfigurationPlanStage, NounParadigmStage, VerbParadigmStage, PronounStage, PredicationStage, LocationalCopulaStage, AdjectiveAgreementStage,
        CommandStage, NegationStage, QuestionStage, PassiveStage, ModalStage, AspectStage, PossessionStage, GenderStage, NumeralStage, DemonstrativeStage, ArticleStage, IndefiniteStage, ConjunctionStage, SubordinationStage, ComplementationStage, RelativizationStage, ComparisonStage, DegreeStage, SimilativeStage,
    ];

    /// <summary>Every setting, in the order they're declared.</summary>
    public static IReadOnlyList<ISetting> All { get; } =
    [
        .. VowelStage.Settings, .. ConsonantStage.Settings, .. SyllableStage.Settings, .. StressStage.Settings, .. ClusterRepairStage.Settings,
        .. SyllableContactStage.Settings, .. DropoffStage.Settings, .. SoundChangeStage.Settings, .. SyllableChangeStage.Settings, .. WordOrderStage.Settings, .. MorphologyStage.Settings, .. AlignmentStage.Settings, .. RoleRegionStage.Settings,
        .. CaseStage.Settings, .. ConfigurationRegionStage.Settings, .. ConfigurationPlanStage.Settings, .. NounParadigmStage.Settings,
        .. VerbParadigmStage.Settings, .. PronounStage.Settings, .. PredicationStage.Settings, .. LocationalCopulaStage.Settings, .. AdjectiveAgreementStage.Settings,
        .. CommandStage.Settings, .. NegationStage.Settings, .. QuestionStage.Settings, .. PassiveStage.Settings, .. ModalStage.Settings, .. AspectStage.Settings, .. PossessionStage.Settings, .. GenderStage.Settings,
        .. NumeralStage.Settings, .. DemonstrativeStage.Settings, .. ArticleStage.Settings, .. IndefiniteStage.Settings, .. ConjunctionStage.Settings, .. SubordinationStage.Settings, .. ComplementationStage.Settings, .. RelativizationStage.Settings, .. ComparisonStage.Settings,
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

    /// <summary>The setting for an edge of the subordinator map.</summary>
    public static Setting<Subordination, bool> SubordinatorEdge(Subordinator first, Subordinator second) =>
        SubordinatorEdges[SubordinatorMap.Edges.Select(e => (e.First, e.Second)).ToList().IndexOf((first, second)) is var i and >= 0
            ? i : throw new ArgumentException($"The subordinator map has no edge from {first} to {second}.")];

    /// <summary>The setting for where a kind of subordinate clause is marked.</summary>
    public static Setting<Subordination, ExponentPosition> SubordinatorOf(Subordinator subordinator) => SubordinatorPositions[(int)subordinator];

    /// <summary>The setting for whether a kind of subordinate clause comes first.</summary>
    public static Setting<Subordination, bool> ClauseFirst(Subordinator subordinator) => SubordinateClauseFirst[(int)subordinator];

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
