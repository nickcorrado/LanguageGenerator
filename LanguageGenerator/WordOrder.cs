namespace LanguageGenerator;

//what about the relationship between head-directionality and constituent word order??
//with head-initial, verb would come before objects. with head-final, verb comes after
public class WordOrder
{
    public required ConstituentWordOrder ConstituentWordOrder { get; init; }
    public required HeadDirectionality HeadDirectionality { get; init; }
    //this one's related to constituent word order. TODO revise
    public required HeadDirectionality VerbPhraseDirectionality { get; init; }
    public required HeadDirectionality NounPhraseDirectionality { get; init; }
    public required HeadDirectionality AdjectivePhraseDirectionality { get; init; }
    public required HeadDirectionality AdpositionalPhraseDirectionality { get; init; }
    //we don't necessarily have this as a part of speech, so it's out for now
    //public HeadDirectionality DeterminerPhraseDirectionality { get; init; }
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

public enum HeadDirectionality
{
    Initial,
    Final
}
