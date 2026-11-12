namespace LanguageGenerator;

/// <summary>A language's constituent order and the head direction of each phrase.</summary>
//with head-initial, verb would come before objects. with head-final, verb comes after. so the verb
//phrase follows from constituent order, and the other phrases are drawn depending on it, since each
//tends to share the verb phrase's direction to some degree
public record WordOrder
{
    public required ConstituentWordOrder ConstituentWordOrder { get; init; }
    public required HeadDirectionality NounPhraseDirectionality { get; init; }
    public required HeadDirectionality AdjectivePhraseDirectionality { get; init; }
    public required HeadDirectionality AdpositionalPhraseDirectionality { get; init; }
    public required ObliquePosition ObliquePosition { get; init; }
    /// <summary>
    /// Whether adjectives after the noun mirror the order they'd have before it, as 'big gray wolf'
    /// is 'wolf gray big', the way most such languages put them, or keep it, as 'wolf big gray'.
    /// </summary>
    public required bool MirrorsAdjectives { get; init; }
    //we don't necessarily have this as a part of speech, so it's out for now
    //public HeadDirectionality DeterminerPhraseDirectionality { get; init; }

    public HeadDirectionality VerbPhraseDirectionality =>
        ObjectPrecedesVerb(ConstituentWordOrder) ? HeadDirectionality.Final : HeadDirectionality.Initial;

    //some languages are consistently head-initial, while others are consistently head-final;
    //many are somewhere in the middle. This is whichever way most phrases go, and the verb
    //phrase's way in a tie
    public HeadDirectionality HeadDirectionality
    {
        get
        {
            HeadDirectionality[] phrases = [VerbPhraseDirectionality, NounPhraseDirectionality, AdjectivePhraseDirectionality, AdpositionalPhraseDirectionality];
            var initial = phrases.Count(d => d == HeadDirectionality.Initial);
            return initial * 2 == phrases.Length ? VerbPhraseDirectionality
                : initial * 2 > phrases.Length ? HeadDirectionality.Initial
                : HeadDirectionality.Final;
        }
    }

    public static bool ObjectPrecedesVerb(ConstituentWordOrder order) => order is ConstituentWordOrder.SOV or ConstituentWordOrder.OSV or ConstituentWordOrder.OVS;
}

public enum ConstituentWordOrder
{
    SVO,
    VSO,
    VOS,
    SOV,
    OSV,
    OVS
}

/// <summary>Where obliques, like 'in the house', go relative to the verb and object.</summary>
//WALS 84A's XOV and XVO, OXV, and OVX and VOX. Between is drawn only where the object precedes the
//verb, and puts them just before the verb
public enum ObliquePosition
{
    BeforeVerbAndObject,
    BetweenObjectAndVerb,
    AfterVerbAndObject,
}

/// <summary>
/// Whether a phrase's head comes first or last, as a verb comes before or after its object.
/// </summary>
public enum HeadDirectionality
{
    Initial,
    Final
}
