namespace LanguageGenerator;

//WALS 24A: which noun in a possessive phrase like 'the king's house' shows the possession
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

//how a language says whose something is. The possessor goes before the possessed noun or after it,
//outside any adjectives and numerals, and Affixes, where the possessed noun is marked, agree with
//the possessor's person and number, next to the root
public record Possession(PossessiveMarking Marking, bool PossessorFirst, IReadOnlyDictionary<(Person, Number), Affix>? Affixes, ExponentPosition AffixPosition)
{
    private static readonly Number[] Numbers = [Number.Singular, Number.Plural];

    public bool MarksPossessor => Marking is PossessiveMarking.Dependent or PossessiveMarking.Double;
    public bool MarksPossessed => Marking is PossessiveMarking.Head or PossessiveMarking.Double;

    //the possessed noun with its affix for a possessor of this person and number, with a dual or a
    //paucal falling back on the plural, as pronouns' numbers do. It goes outside the noun's own
    //number but inside its case, as in Turkish ev-ler-im-i 'house-PL-1SG-ACC'
    public Word Agree(Word noun, Person person, Number number)
    {
        var nearest = (Number)(Syntax.Nearest(number, Numbers) ?? Number.Plural);
        if (Affixes is null || !Affixes.TryGetValue((person, nearest), out var affix))
        {
            return noun;
        }
        var morphemes = noun.Morphemes.ToList();
        static bool IsNumber(Morpheme m) => m.Value is Affix { Tagmemes: [Number] };
        var root = morphemes.FindIndex(m => m.Value is Root);
        var at = root;
        if (AffixPosition == ExponentPosition.Prefix)
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
