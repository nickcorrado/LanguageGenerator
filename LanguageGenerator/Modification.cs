namespace LanguageGenerator;

//where a noun phrase's number and case show when an adjective modifies the noun. WALS has no
//chapter counting these, so the generator's odds are guesses
public enum AdjectiveAgreement
{
    //only the noun inflects, and the adjective is bare, like English big dogs
    None,
    //the adjective agrees, taking the noun's number and case markers too, like Latin canes magnos
    //'big dogs (accusative)'
    Agreement,
    //the phrase is marked once, at its edge: its suffixes go on its last word and its prefixes on
    //its first, wherever the noun is, like the English possessive in the king of England's
    Phrasal,
}
