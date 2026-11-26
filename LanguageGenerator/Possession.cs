namespace LanguageGenerator;

/// <summary>
/// Which noun in a possessive phrase like 'the king's house' shows the possession.
/// </summary>
//WALS 24A
public enum PossessiveMarking
{
    //the possessor, with a genitive case or an adposition, as in English the king's house
    Dependent,
    //the possessed noun, with an affix agreeing with the possessor, as in Hungarian a király ház-a
    //'the king house-3SG'
    Head,
    //both, as in Turkish kral-ın ev-i 'king-GEN house-3SG'
    Double,
    //neither: the two nouns are just side by side
    None,
}

/// <summary>
/// How a language says whose something is: whether it marks the possessor, with the possessive
/// role's case or adposition, and how the possessed noun agrees with the possessor, where it does.
/// </summary>
//the possessor goes before the possessed noun or after it, outside any adjectives and numerals
public record Possession(bool PossessorFirst, bool MarksPossessor, PossessedAgreement? Agreement)
{
    public bool MarksPossessed => Agreement is not null;
}

/// <summary>
/// The possessed noun's affixes agreeing with its possessor's person and number, glossed as
/// possessive, as in 3SG.POSS, all on one side of it.
/// </summary>
public record PossessedAgreement(IReadOnlyDictionary<(Person, Number), Affix> Affixes, Side Side)
{
    private static readonly Number[] Numbers = [Number.Singular, Number.Plural];

    /*
      the possessed noun with its affix for a possessor of this person and number, with a dual or a
      paucal falling back on the plural, as pronouns' numbers do. It goes outside the noun's own
      number but inside its case, as in Turkish ev-ler-im-i 'house-PL-1SG-ACC'
     */
    public Word Agree(Word noun, Person person, Number number)
    {
        var nearest = (Number)(Syntax.Nearest(number, Numbers) ?? Number.Plural);
        if (!Affixes.TryGetValue((person, nearest), out var affix))
        {
            return noun;
        }
        var morphemes = noun.Morphemes.ToList();
        static bool IsNumber(Morpheme m) => m.Value is Affix { Grammemes: [Number] };
        var root = morphemes.FindIndex(m => m.Value is Root);
        var at = root;
        if (Side == Side.Before)
        {
            while (at > 0 && IsNumber(morphemes[at - 1]))
            {
                at--;
            }
        }
        else
        {
            at++;
            while (at < morphemes.Count && IsNumber(morphemes[at]))
            {
                at++;
            }
        }
        morphemes.Insert(at, affix);
        return noun with { Morphemes = morphemes };
    }
}
