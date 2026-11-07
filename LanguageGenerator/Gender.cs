namespace LanguageGenerator;

/// <summary>How many genders a language's nouns fall into, and on what basis.</summary>
//WALS 30A and 31A. Its 36 languages with four or more, mostly Bantu-style noun classes, are left
//out
public enum NounGenders
{
    None,
    //like French or Spanish
    MasculineFeminine,
    //like German or Russian
    MasculineFeminineNeuter,
    //like Ojibwe, where people and animals are animate, along with some things
    AnimateInanimate,
}

/// <summary>
/// Whether a noun's gender follows only from what it means, or also from its form.
/// </summary>
//WALS 32A
public enum GenderAssignment
{
    //people by sex, and everything else in one gender, like Tamil's rational and non-rational
    Semantic,
    //people by sex, and everything else by how the word ends, like Spanish -o and -a
    SemanticAndFormal,
}

/// <summary>What kind of thing a noun names, as far as gender cares.</summary>
public enum SemanticClass
{
    Male,
    Female,
    //a person of no particular sex
    Human,
    Animal,
    Thing,
}

/// <summary>
/// A language's noun genders: which there are, how a noun gets one, and how words agree with it.
/// </summary>
public class GenderSystem
{
    public required NounGenders Genders { get; init; }
    public required GenderAssignment Assignment { get; init; }
    /// <summary>
    /// The affix each gender puts on a word that agrees with a noun, all on one side of it. Null in
    /// a language without gender, or an isolating one, with no affixes to agree with.
    /// </summary>
    public required IReadOnlyDictionary<Gender, Affix>? Markers { get; init; }
    public required ExponentPosition MarkerPosition { get; init; }
    /// <summary>
    /// Whether a verb agrees with the gender of its subject, as in Russian on spal 'he slept' and
    /// ona spala 'she slept'.
    /// </summary>
    public required bool VerbsAgree { get; init; }

    public IReadOnlyList<Gender> Values => Genders switch
    {
        NounGenders.None => [],
        NounGenders.MasculineFeminine => [Gender.Masculine, Gender.Feminine],
        NounGenders.MasculineFeminineNeuter => [Gender.Masculine, Gender.Feminine, Gender.Neuter],
        NounGenders.AnimateInanimate => [Gender.Animate, Gender.Inanimate],
        _ => throw new InvalidOperationException($"{Genders} isn't a gender system."),
    };

    //a noun's gender, from what it means and, where assignment is formal, from how its root ends:
    //a front or open vowel makes it feminine, as -a does in Latin, Spanish or Russian, another vowel
    //masculine, and a consonant neuter, or masculine
    //where there's no neuter. In an animacy system, a thing ending in a consonant is animate, as
    //some things are in Ojibwe. People of no particular sex are masculine, the usual default.
    //TODO variance: real systems stray from the semantic core (German Kind 'child' is neuter), so
    //with a small chance, seeded from the language and the meaning, a Human noun could follow its
    //form where assignment is formal. Kin terms and names shouldn't stray, and M and F can't tell
    //those from other words, so Mädchen 'girl' would need more than this
    public Gender? Of(Lexeme noun, SemanticClass meaning)
    {
        var final = noun.Root.Form.LastOrDefault();
        var formal = Assignment == GenderAssignment.SemanticAndFormal;
        return Genders switch
        {
            NounGenders.None => null,
            NounGenders.AnimateInanimate => meaning != SemanticClass.Thing || formal && final is Consonant ? Gender.Animate : Gender.Inanimate,
            _ => meaning switch
            {
                SemanticClass.Male or SemanticClass.Human => Gender.Masculine,
                SemanticClass.Female => Gender.Feminine,
                _ when formal => final switch
                {
                    Vowel { Backness: VowelFrontness.Front } or Vowel { Height: VowelHeight.Open } => Gender.Feminine,
                    Vowel => Gender.Masculine,
                    _ => Genders == NounGenders.MasculineFeminineNeuter ? Gender.Neuter : Gender.Masculine,
                },
                //everything that isn't a person shares one gender
                _ => Genders == NounGenders.MasculineFeminineNeuter ? Gender.Neuter : Gender.Masculine,
            },
        };
    }

    //the word, agreeing with this gender: the marker goes next to the root for an adjective, as
    //gender sits inside number and case in Latin magn-a, and at the edge of a verb, outside its
    //other affixes
    public Word Agree(Word word, Gender gender, bool nextToRoot)
    {
        if (Markers is null || !Markers.TryGetValue(gender, out var marker) || marker.Form.Count == 0)
        {
            return word;
        }
        var morphemes = word.Morphemes.ToList();
        var root = morphemes.FindIndex(m => m.Value is Root);
        var at = MarkerPosition == ExponentPosition.Prefix ? (nextToRoot ? root : 0) : (nextToRoot ? root + 1 : morphemes.Count);
        morphemes.Insert(at, marker);
        return word with { Morphemes = morphemes };
    }
}
