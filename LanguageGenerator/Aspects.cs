namespace LanguageGenerator;

/// <summary>
/// How a language marks an aspect, as a spec names it: not at all, by an affix or a word of its
/// own, or by an auxiliary verb, named for the verb it is, which takes the clause's tense and
/// agreement with the verb bare beside it.
/// </summary>
public enum AspectKind
{
    None,
    Affix,
    Particle,
    /// <summary>'Have', as English 'has gone' and Spanish ha ido have it.</summary>
    Have,
    /// <summary>
    /// 'Be': the copula for a perfect, as French est venu has it, or for a habitual, as Hindi jātā
    /// hai 'goes, habitually' has it, and for a progressive the copula that says where, as Spanish
    /// estar is in está esperando.
    /// </summary>
    Be,
    /// <summary>'Finish', after the verb, as Yoruba and many languages of Southeast Asia have it.</summary>
    Finish,
    /// <summary>'Sit', as Dutch zit te wachten 'sits waiting' is 'is waiting'.</summary>
    Sit,
}

/// <summary>The verb an aspect's auxiliary is, as AspectKind describes each.</summary>
public enum AspectVerb
{
    Have,
    Be,
    Finish,
    Sit,
}

/// <summary>
/// An aspect as an auxiliary verb, which takes the clause's tense, agreement and negation, with the
/// clause's verb bare beside it, as modal verbs do.
/// </summary>
public record AspectAuxiliary(AspectVerb Verb, bool BeforeVerb)
{
    /// <summary>The auxiliary for this verb, on the side of the verb the language puts its words, but for 'finish'.</summary>
    //a completive 'finish' follows its verb in VO and OV languages alike, as Thai kin lɛ́ɛw 'eat
    //finish' and Japanese tabe-te shimau 'eat-CONV finish' do
    public static AspectAuxiliary Of(AspectVerb verb, bool beforeVerb) => new(verb, verb != AspectVerb.Finish && beforeVerb);
}

/// <summary>
/// How a language marks an aspect: an affix next to the verb's root, outside only a passive, glossed
/// as the aspect, a word of its own beside the verb, as Mandarin zài is, or an auxiliary.
/// </summary>
public union AspectMarking(Bound, Free, AspectAuxiliary);

/// <summary>
/// How a language marks the perfect, as in 'nobody has repaired it', the progressive, as in 'a
/// woman is waiting', and the habitual, as in 'she used to walk there', and whether it marks
/// beginning or ceasing to do something on the verb, as an inceptive or a cessative. A language
/// that marks none says a perfect as a perfective past, a progressive or habitual as an
/// imperfective, as Russian does, and beginning and ceasing with verbs of their own, as English
/// does.
/// </summary>
//each is null where the language doesn't mark it
public record Aspects(AspectMarking? Perfect, AspectMarking? Progressive, AspectMarking? Habitual, AspectMarking? Inceptive, AspectMarking? Cessative)
{
    /// <summary>The kinds a perfect can be.</summary>
    public static IReadOnlyList<AspectKind> PerfectKinds { get; } = [AspectKind.None, AspectKind.Affix, AspectKind.Particle, AspectKind.Have, AspectKind.Be, AspectKind.Finish];
    /// <summary>The kinds a progressive can be.</summary>
    public static IReadOnlyList<AspectKind> ProgressiveKinds { get; } = [AspectKind.None, AspectKind.Affix, AspectKind.Particle, AspectKind.Be, AspectKind.Sit];
    /// <summary>The kinds a habitual can be.</summary>
    public static IReadOnlyList<AspectKind> HabitualKinds { get; } = [AspectKind.None, AspectKind.Affix, AspectKind.Particle, AspectKind.Be];
    /// <summary>
    /// The kinds an inceptive or cessative can be: a marker on the verb, or none, where the
    /// language says it with a verb of its own, as English does with 'begin' or 'stop'.
    /// </summary>
    public static IReadOnlyList<AspectKind> PhaseKinds { get; } = [AspectKind.None, AspectKind.Affix, AspectKind.Particle];

    /// <summary>The verb an auxiliary of this kind is, or null for a kind that isn't one.</summary>
    public static AspectVerb? VerbOf(AspectKind kind) => kind switch
    {
        AspectKind.Have => AspectVerb.Have,
        AspectKind.Be => AspectVerb.Be,
        AspectKind.Finish => AspectVerb.Finish,
        AspectKind.Sit => AspectVerb.Sit,
        AspectKind.None or AspectKind.Affix or AspectKind.Particle => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The kind a marking is, as a spec names it.</summary>
    public static AspectKind KindOf(AspectMarking? marking) => marking is not { } m ? AspectKind.None : m switch
    {
        Bound => AspectKind.Affix,
        Free => AspectKind.Particle,
        AspectAuxiliary { Verb: AspectVerb.Have } => AspectKind.Have,
        AspectAuxiliary { Verb: AspectVerb.Be } => AspectKind.Be,
        AspectAuxiliary { Verb: AspectVerb.Finish } => AspectKind.Finish,
        AspectAuxiliary { Verb: AspectVerb.Sit } => AspectKind.Sit,
        AspectAuxiliary a => throw new ArgumentOutOfRangeException(nameof(marking), a.Verb, null),
    };

    /// <summary>Each aspect's and phase's marking, in the order of the settings for them.</summary>
    public IReadOnlyList<AspectMarking?> All => [Perfect, Progressive, Habitual, Inceptive, Cessative];

    /// <summary>
    /// How a clause's tense and aspect are said: the tense and aspect its verb inflects for, and the
    /// marker of a perfect, progressive or habitual beside them, if the language has one. A perfect
    /// the language doesn't mark is a perfective, and in the past where it's present, as 'nobody
    /// has repaired it' is 'nobody repaired it'; a progressive or habitual it doesn't mark is an
    /// imperfective, as WALS 65A finds imperfectives covering habituals.
    /// </summary>
    public (Tense Tense, Aspect Aspect, AspectMarking? Marking) Said(Tense tense, Aspect aspect)
    {
        if (aspect == Aspect.Perfect)
        {
            return (Perfect is null && tense == Tense.Present ? Tense.Past : tense, Aspect.Perfective, Perfect);
        }
        if (aspect == Aspect.Progressive)
        {
            return (tense, Aspect.Imperfective, Progressive);
        }
        if (aspect == Aspect.Habitual)
        {
            return (tense, Aspect.Imperfective, Habitual);
        }
        return (tense, aspect, null);
    }

    /// <summary>The marking of beginning or ceasing on the verb, or null where the language says it with a verb.</summary>
    public AspectMarking? MarkingOf(Phase phase) =>
        phase == Phase.Inceptive ? Inceptive
        : phase == Phase.Cessative ? Cessative
        : throw new ArgumentOutOfRangeException(nameof(phase), phase, null);

    /// <summary>The phase a verb says, begin the inceptive and stop the cessative, or null for any other verb.</summary>
    public static Phase? PhaseOf(string verb) => verb switch
    {
        "begin" => Phase.Inceptive,
        "stop" => Phase.Cessative,
        _ => null,
    };
}
