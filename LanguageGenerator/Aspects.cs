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

/// <summary>
/// An aspect as an auxiliary verb, which takes the clause's tense, agreement and negation, with the
/// clause's verb bare beside it, as modal verbs do. Kind is Have, Be, Finish or Sit, and says which
/// verb it is.
/// </summary>
public record AspectAuxiliary(AspectKind Kind, bool BeforeVerb);

/// <summary>
/// How a language marks an aspect: an affix next to the verb's root, outside only a passive, a
/// word of its own beside the verb, as Mandarin zài is, or an auxiliary.
/// </summary>
public union AspectMarking(Bound, Free, AspectAuxiliary);

/// <summary>
/// How a language marks one aspect: its kind, the form of its affix or particle, which side of the
/// root the affix goes on, and which side of the verb a particle or auxiliary goes on.
/// </summary>
//the form is coined whatever the kind, so that a spec can write one in, as the desiderative's is
public record AspectMarker(AspectKind Kind, IReadOnlyList<Phoneme> Form, AffixAccent Accent, ExponentPosition AffixPosition, bool BeforeVerb)
{
    /// <summary>
    /// The marking of this aspect or phase, glossed as it, or null where the language doesn't mark
    /// it.
    /// </summary>
    public AspectMarking? MarkingOf(Grammeme aspect) => Kind switch
    {
        AspectKind.None => null,
        AspectKind.Affix => new Bound(new Affix(Form, [aspect], Accent), AffixPosition == ExponentPosition.Prefix ? Side.Before : Side.After),
        AspectKind.Particle => new Free(new Particle(Form, [aspect]), BeforeVerb ? Side.Before : Side.After),
        //a completive 'finish' follows its verb in VO and OV languages alike, as Thai kin lɛ́ɛw 'eat
        //finish' and Japanese tabe-te shimau 'eat-CONV finish' do
        AspectKind.Finish => new AspectAuxiliary(Kind, BeforeVerb: false),
        AspectKind.Have or AspectKind.Be or AspectKind.Sit => new AspectAuxiliary(Kind, BeforeVerb),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };
}

/// <summary>
/// How a language marks the perfect, as in 'nobody has repaired it', the progressive, as in 'a
/// woman is waiting', and the habitual, as in 'she used to walk there', and whether it marks
/// beginning or ceasing to do something on the verb, as an inceptive or a cessative. A language
/// that marks none says a perfect as a perfective past, a progressive or habitual as an
/// imperfective, as Russian does, and beginning and ceasing with verbs of their own, as English
/// does.
/// </summary>
public record Aspects(AspectMarker Perfect, AspectMarker Progressive, AspectMarker Habitual, AspectMarker Inceptive, AspectMarker Cessative)
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
            var perfect = Perfect.MarkingOf(Aspect.Perfect);
            return (perfect is null && tense == Tense.Present ? Tense.Past : tense, Aspect.Perfective, perfect);
        }
        if (aspect == Aspect.Progressive)
        {
            return (tense, Aspect.Imperfective, Progressive.MarkingOf(Aspect.Progressive));
        }
        if (aspect == Aspect.Habitual)
        {
            return (tense, Aspect.Imperfective, Habitual.MarkingOf(Aspect.Habitual));
        }
        return (tense, aspect, null);
    }

    /// <summary>The marking of beginning or ceasing on the verb, or null where the language says it with a verb.</summary>
    public AspectMarking? MarkingOf(Phase phase) =>
        phase == Phase.Inceptive ? Inceptive.MarkingOf(phase)
        : phase == Phase.Cessative ? Cessative.MarkingOf(phase)
        : throw new ArgumentOutOfRangeException(nameof(phase), phase, null);

    /// <summary>The phase a verb says, begin the inceptive and stop the cessative, or null for any other verb.</summary>
    public static Phase? PhaseOf(string verb) => verb switch
    {
        "begin" => Phase.Inceptive,
        "stop" => Phase.Cessative,
        _ => null,
    };
}
