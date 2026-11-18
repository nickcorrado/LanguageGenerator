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

    private static Choice<bool> YesNo { get; } = Choice<bool>.Of(true, false);

    //sounds

    public static Stage<IReadOnlyList<Vowel>> VowelStage { get; } = new("vowels", l => l.Phonology.Vowels);
    /// <summary>The vowels, commonest first.</summary>
    public static Setting<IReadOnlyList<Vowel>, IReadOnlyList<Vowel>> Vowels { get; } =
        VowelStage.Declare("vowels", Sounds, v => v, (_, v) => v, new PhonemeList<Vowel>(Vowel.All));

    public static Stage<IReadOnlyList<Consonant>> ConsonantStage { get; } = new("consonants", l => l.Phonology.Consonants);
    /// <summary>The consonants, commonest first.</summary>
    public static Setting<IReadOnlyList<Consonant>, IReadOnlyList<Consonant>> Consonants { get; } =
        ConsonantStage.Declare("consonants", Sounds, c => c, (_, c) => c, new PhonemeList<Consonant>(Consonant.All));

    public static Stage<SyllableShape> SyllableStage { get; } = new("syllable rule", l => new(l.Phonology.SyllableRule.Template, l.Phonology.SyllableRule.Codas));
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

    public static Stage<Stress> StressStage { get; } = new("stress", l => l.Phonology.Stress);
    public static Setting<Stress, Stress> Stress { get; } = StressStage.Declare("stress", Sounds,
        s => s, (_, s) => s, Choice<Stress>.Of(Enum.GetValues<Stress>()));

    public static Stage<ClusterRepair> ClusterRepairStage { get; } = new("cluster repair", l => l.Phonology.ClusterRepair);
    /// <summary>How a cluster the syllables can't hold, made by putting morphemes together, is fixed.</summary>
    public static Setting<ClusterRepair, ClusterRepair> ClusterRepair { get; } = ClusterRepairStage.Declare("cluster-repair", Sounds,
        r => r, (_, r) => r, Choice<ClusterRepair>.Of(Enum.GetValues<ClusterRepair>()));

    public static Stage<double> DropoffStage { get; } = new("dropoff", l => l.Dropoff);
    /// <summary>
    /// How steeply sounds get rarer down an inventory: each is (1 - dropoff) times as likely as the
    /// one before it, so a higher dropoff favors the first few more. Nothing draws it; it's 0.15
    /// unless a spec says otherwise.
    /// </summary>
    public static Setting<double, double> Dropoff { get; } = DropoffStage.Declare("dropoff", Sounds, d => d, (_, d) => d, new Between(0, 1));

    //word order

    public static Stage<WordOrder> WordOrderStage { get; } = new("word order", l => l.WordOrder);
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

    public static Stage<MorphologicalProfile> MorphologyStage { get; } = new("morphology", l => l.MorphologicalProfile);
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

    public static Stage<MorphosyntacticAlignment> AlignmentStage { get; } = new("alignment", l => l.MorphosyntacticAlignment);
    public static Setting<MorphosyntacticAlignment, MorphosyntacticAlignment> Alignment { get; } = AlignmentStage.Declare("alignment", Roles,
        a => a, (_, a) => a, Choice<MorphosyntacticAlignment>.Of(Enum.GetValues<MorphosyntacticAlignment>()));

    /// <summary>Every setting, in the order they're declared.</summary>
    public static IReadOnlyList<ISetting> All { get; } =
    [
        .. VowelStage.Settings, .. ConsonantStage.Settings, .. SyllableStage.Settings, .. StressStage.Settings, .. ClusterRepairStage.Settings,
        .. DropoffStage.Settings, .. WordOrderStage.Settings, .. MorphologyStage.Settings, .. AlignmentStage.Settings,
    ];

    /// <summary>The setting a spec names this way, or null if there's none.</summary>
    public static ISetting? Named(string name) => All.FirstOrDefault(s => s.Name == name);
}

/// <summary>A rule about settings that can contradict each other, as a spec might fix them.</summary>
public sealed record SpecRule(string Says, Func<LanguageSpec, string?> Check)
{
    /// <summary>
    /// Two choice settings whose values have to go together: a spec breaks the rule when none of the
    /// values it allows for one goes with any it allows for the other, a free setting allowing all.
    /// </summary>
    public static SpecRule Together<TStageA, TA, TStageB, TB>(Setting<TStageA, TA> first, Setting<TStageB, TB> second, Func<TA, TB, bool> together, string says)
        where TA : notnull where TB : notnull => new(says, spec =>
        {
            if (spec.Allowed(first) is null && spec.Allowed(second) is null)
            {
                return null;
            }
            var firsts = (spec.Allowed(first) ?? first.AllValues!).Cast<TA>();
            var seconds = (spec.Allowed(second) ?? second.AllValues!).Cast<TB>();
            return firsts.Any(a => seconds.Any(b => together(a, b))) ? null : says;
        });
}

/// <summary>The rules a spec has to keep to, besides each setting's own values.</summary>
public static class SpecRules
{
    public static IReadOnlyList<SpecRule> All { get; } =
    [
        //a restriction is only ever drawn for a template with a coda, and means nothing without one
        SpecRule.Together(Settings.SyllableTemplate, Settings.CodaRestriction,
            (template, codas) => codas == LanguageGenerator.CodaRestriction.Any || template.Slots.Contains(Slot.Final),
            "A coda restriction needs a syllable template with a coda: CVF, CLVF, SCVF or SCLVF."),
        //with the verb before its object, between them is just before the verb, which WALS has no
        //languages with obliques in either
        SpecRule.Together(Settings.ConstituentOrder, Settings.ObliquePosition,
            (order, position) => position != LanguageGenerator.ObliquePosition.BetweenObjectAndVerb || WordOrder.ObjectPrecedesVerb(order),
            "Obliques can only go between the object and the verb where the object comes first: SOV, OVS or OSV."),
    ];
}
