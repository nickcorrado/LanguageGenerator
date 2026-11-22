namespace LanguageGenerator;

/// <summary>
/// How a language marks a perfect or a progressive, as a spec names it: not at all, by an affix or a
/// word of its own, or by an auxiliary verb, named for the verb it is, which takes the clause's tense
/// and agreement with the verb bare beside it.
/// </summary>
public enum AspectKind
{
    None,
    Affix,
    Particle,
    /// <summary>'Have', as English 'has gone' and Spanish ha ido have it.</summary>
    Have,
    /// <summary>
    /// 'Be': the copula for a perfect, as French est venu has it, and for a progressive the copula
    /// that says where, as Spanish estar is in está esperando.
    /// </summary>
    Be,
    /// <summary>'Finish', after the verb, as Yoruba and many languages of Southeast Asia have it.</summary>
    Finish,
    /// <summary>'Sit', as Dutch zit te wachten 'sits waiting' is 'is waiting'.</summary>
    Sit,
}

/// <summary>A perfect or progressive as an affix next to the verb's root, outside only a passive.</summary>
//Position is Prefix or Suffix
public record AspectAffix(Affix Marker, ExponentPosition Position);

/// <summary>A perfect or progressive as a word of its own beside the verb, as Mandarin zài is.</summary>
public record AspectParticle(Particle Marker, bool BeforeVerb);

/// <summary>
/// A perfect or progressive as an auxiliary verb, which takes the clause's tense, agreement and
/// negation, with the clause's verb bare beside it, as modal verbs do. Kind is Have, Be, Finish or
/// Sit, and says which verb it is.
/// </summary>
public record AspectAuxiliary(AspectKind Kind, bool BeforeVerb);

/// <summary>How a language marks a perfect or a progressive.</summary>
public union AspectMarking(AspectAffix, AspectParticle, AspectAuxiliary);

/// <summary>
/// How a language marks one aspect: its kind, the form of its affix or particle, which side of the
/// root the affix goes on, and which side of the verb a particle or auxiliary goes on.
/// </summary>
//the form is coined whatever the kind, so that a spec can write one in, as the desiderative's is
public record AspectMarker(AspectKind Kind, IReadOnlyList<Phoneme> Form, ExponentPosition AffixPosition, bool BeforeVerb)
{
    /// <summary>The marking of this aspect, glossed as it, or null where the language doesn't mark it.</summary>
    public AspectMarking? MarkingOf(Aspect aspect) => Kind switch
    {
        AspectKind.None => null,
        AspectKind.Affix => new AspectAffix(new Affix(Form, [aspect]), AffixPosition),
        AspectKind.Particle => new AspectParticle(new Particle(Form, [aspect]), BeforeVerb),
        //a completive 'finish' follows its verb in VO and OV languages alike, as Thai kin lɛ́ɛw 'eat
        //finish' and Japanese tabe-te shimau 'eat-CONV finish' do
        AspectKind.Finish => new AspectAuxiliary(Kind, BeforeVerb: false),
        AspectKind.Have or AspectKind.Be or AspectKind.Sit => new AspectAuxiliary(Kind, BeforeVerb),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };
}

/// <summary>
/// How a language marks the perfect, as in 'nobody has repaired it', and the progressive, as in 'a
/// woman is waiting'. A language that marks neither says a perfect as a perfective past and a
/// progressive as an imperfective, as Russian does.
/// </summary>
public record Aspects(AspectMarker Perfect, AspectMarker Progressive)
{
    /// <summary>The kinds a perfect can be.</summary>
    public static IReadOnlyList<AspectKind> PerfectKinds { get; } = [AspectKind.None, AspectKind.Affix, AspectKind.Particle, AspectKind.Have, AspectKind.Be, AspectKind.Finish];
    /// <summary>The kinds a progressive can be.</summary>
    public static IReadOnlyList<AspectKind> ProgressiveKinds { get; } = [AspectKind.None, AspectKind.Affix, AspectKind.Particle, AspectKind.Be, AspectKind.Sit];

    /// <summary>
    /// How a clause's tense and aspect are said: the tense and aspect its verb inflects for, and the
    /// marker of a perfect or progressive beside them, if the language has one. A perfect the
    /// language doesn't mark is a perfective, and in the past where it's present, as 'nobody has
    /// repaired it' is 'nobody repaired it'; a progressive it doesn't mark is an imperfective.
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
        return (tense, aspect, null);
    }
}
