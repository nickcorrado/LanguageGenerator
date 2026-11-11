namespace LanguageGenerator;

/// <summary>
/// A modal as an affix on the verb, as Turkish -ebil in gid-ebil-ir-im 'I can go', next to its root
/// and outside only a passive.
/// </summary>
//Position is Prefix or Suffix
public record ModalAffix(Affix Marker, ExponentPosition Position);

/// <summary>A modal as a word of its own beside the verb, as Chukchi mecənkə.</summary>
public record ModalParticle(Particle Marker, bool BeforeVerb);

/// <summary>
/// A modal as a verb of its own, as French pouvoir, which takes the clause's tense, agreement and
/// the modal's negation, with the clause's verb bare beside it.
/// </summary>
//the verb's root is glossed by what it means here, so a verb a language uses for both 'can' and
//'may' is glossed 'can' in one use and 'may' in the other, as a shared subordinator is
public record ModalVerb(Lexeme Verb, bool BeforeVerb);

/// <summary>How a language marks one modality.</summary>
public union ModalMarking(ModalAffix, ModalParticle, ModalVerb);

/// <summary>How a language marks each modality.</summary>
public record Modals(IReadOnlyDictionary<Modality, ModalMarking> Markers);

/// <summary>
/// A semantic map of the modalities, after van der Auwera and Plungian's: which share a marker in
/// languages, as English can is both 'able to' and 'allowed to'.
/// </summary>
//WALS counts none of this, so every weight is a guess
public static class ModalityMap
{
    /// <summary>The modalities, in no order that matters but the edges'.</summary>
    public static IReadOnlyList<Modality> Modalities { get; } = [Modality.Ability, Modality.Permission, Modality.Obligation];

    public static IReadOnlyList<(Modality First, Modality Second, double Weight)> Edges { get; } =
    [
        //English can, French pouvoir, Russian moč', Turkish -ebil, against German können and dürfen
        //or Mandarin néng and kěyǐ
        (Modality.Ability, Modality.Permission, 0.6),
        //a marker of possibility and necessity alike, as Old English mōt was 'may' and 'must', and
        //St'át'imcets's modals are neither
        (Modality.Permission, Modality.Obligation, 0.05),
    ];

    /// <summary>The English verb that glosses a modal verb in this meaning.</summary>
    public static string VerbGloss(Modality modality) =>
        modality == Modality.Ability ? "can"
        : modality == Modality.Permission ? "may"
        : modality == Modality.Obligation ? "must"
        : throw new ArgumentOutOfRangeException(nameof(modality), modality, null);
}

/// <summary>
/// A statement said to be possible or necessary, as in 'you must find the key'. Negative negates
/// the modal, as in 'you cannot swim', and the statement's own negation its verb, as in 'you must
/// not sing'.
/// </summary>
//a clause with a verb, a passive or a copular clause
public record Modalized(Statement Clause, Modality Modality, bool Negative);
