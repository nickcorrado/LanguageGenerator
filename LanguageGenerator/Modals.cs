namespace LanguageGenerator;

/// <summary>
/// A modal as a verb of its own, as French pouvoir, which takes the clause's tense, agreement and
/// the modal's negation, with the clause's verb bare beside it.
/// </summary>
//the verb's root is glossed by what it means here, so a verb a language uses for both 'can' and
//'may' is glossed 'can' in one use and 'may' in the other, as a shared subordinator is
public record ModalVerb(Lexeme Verb, bool BeforeVerb);

/// <summary>
/// How a language marks one modality: an affix on the verb, as Turkish -ebil in gid-ebil-ir-im 'I
/// can go', next to its root and outside only a passive, a word of its own beside the verb, as
/// Chukchi mecənkə, or a verb of its own.
/// </summary>
public union ModalMarking(Bound, Free, ModalVerb);

/// <summary>How a modality is marked, as a spec names it.</summary>
public enum ModalKind
{
    Affix,
    Verb,
    Particle,
}

/// <summary>
/// How a language marks each modality, and the edges of the modality map it joins, whose
/// modalities share a marker's form.
/// </summary>
public record Modals(IReadOnlyList<(Modality First, Modality Second)> Joined, IReadOnlyDictionary<Modality, ModalMarking> Markers)
{
    /// <summary>The modalities that share a marker with this one, itself among them.</summary>
    public IReadOnlyList<Modality> RegionOf(Modality modality) =>
        SemanticMap.Regions(ModalityMap.Modalities, Joined).First(r => r.Contains(modality));
}

/// <summary>
/// A semantic map of the modalities, after van der Auwera and Plungian's: which share a marker in
/// languages, as English can is both 'able to' and 'allowed to', and French devoir both 'must' and,
/// in the conditional, 'should'. An epistemic modal joins the map through the situational one
/// it grew from, 'might' through 'may' and a sure 'must' through an obliging one.
/// </summary>
//WALS counts none of the situational edges, so their weights are guesses
public static class ModalityMap
{
    /// <summary>The modalities, in no order that matters but the edges'.</summary>
    public static IReadOnlyList<Modality> Modalities { get; } =
        [Modality.Ability, Modality.Permission, Modality.Obligation, Modality.Advisability, Modality.Dubitative, Modality.Presumptive];

    public static IReadOnlyList<(Modality First, Modality Second, double Weight)> Edges { get; } =
    [
        //English can, French pouvoir, Russian moč', Turkish -ebil, against German können and dürfen
        //or Mandarin néng and kěyǐ
        (Modality.Ability, Modality.Permission, 0.6),
        //a marker of possibility and necessity alike, as Old English mōt was 'may' and 'must', and
        //St'át'imcets's modals are neither
        (Modality.Permission, Modality.Obligation, 0.05),
        /*
          'should' as a softer 'must', which many languages don't tell apart, or tell apart only by
          putting 'must' in a conditional, as French devrais and Spanish debería do, against English
          should, German sollen or Russian sledovalo by
         */
        (Modality.Obligation, Modality.Advisability, 0.5),
        /*
          van der Auwera and Plungian find epistemic possibility growing out of permission and
          epistemic necessity out of obligation, as English may and must are both. WALS 76A counts
          the two overlaps together, so Generator.GenerateModals draws them together from its table,
          and these weights are each one's share alone, where 'either' is split evenly
         */
        (Modality.Permission, Modality.Dubitative, 69.0 / 207),
        (Modality.Obligation, Modality.Presumptive, 69.0 / 207),
    ];

    /// <summary>The English verb that glosses a modal verb in this meaning.</summary>
    public static string VerbGloss(Modality modality) =>
        modality == Modality.Ability ? "can"
        : modality == Modality.Permission ? "may"
        : modality == Modality.Obligation ? "must"
        : modality == Modality.Advisability ? "should"
        : modality == Modality.Dubitative ? "might"
        : modality == Modality.Presumptive ? "must"
        : throw new ArgumentOutOfRangeException(nameof(modality), modality, null);
}

/// <summary>
/// A statement said to be possible or necessary, as in 'you must find the key'. Negative negates
/// the modal, as in 'you cannot swim', and the statement's own negation its verb, as in 'you must
/// not sing'. An epistemic one judges the statement whole, its tense and aspect too, as in 'he
/// must have left'.
/// </summary>
//a clause with a verb, a passive or a copular clause
public record Modalized(Statement Clause, Modality Modality, bool Negative);
