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

/// <summary>What the generator knows of what nouns mean.</summary>
//anything not here is taken to be a thing, which a front end could do better at, from a resource
//like WordNet
public static class Semantics
{
    private static readonly Dictionary<string, SemanticClass> Classes = new()
    {
        ["man"] = SemanticClass.Male, ["boy"] = SemanticClass.Male, ["king"] = SemanticClass.Male, ["father"] = SemanticClass.Male,
        ["son"] = SemanticClass.Male, ["brother"] = SemanticClass.Male, ["husband"] = SemanticClass.Male, ["prince"] = SemanticClass.Male,
        ["woman"] = SemanticClass.Female, ["girl"] = SemanticClass.Female, ["queen"] = SemanticClass.Female, ["mother"] = SemanticClass.Female,
        ["daughter"] = SemanticClass.Female, ["sister"] = SemanticClass.Female, ["wife"] = SemanticClass.Female, ["princess"] = SemanticClass.Female,
        ["person"] = SemanticClass.Human, ["child"] = SemanticClass.Human, ["friend"] = SemanticClass.Human, ["enemy"] = SemanticClass.Human,
        ["hunter"] = SemanticClass.Human, ["soldier"] = SemanticClass.Human, ["guard"] = SemanticClass.Human, ["stranger"] = SemanticClass.Human,
        ["fool"] = SemanticClass.Human, ["warrior"] = SemanticClass.Human, ["merchant"] = SemanticClass.Human, ["god"] = SemanticClass.Human,
        ["dog"] = SemanticClass.Animal, ["cat"] = SemanticClass.Animal, ["fish"] = SemanticClass.Animal, ["bird"] = SemanticClass.Animal,
        ["wolf"] = SemanticClass.Animal, ["horse"] = SemanticClass.Animal, ["cow"] = SemanticClass.Animal, ["sheep"] = SemanticClass.Animal,
        ["pig"] = SemanticClass.Animal, ["snake"] = SemanticClass.Animal, ["bear"] = SemanticClass.Animal, ["deer"] = SemanticClass.Animal,
        ["mouse"] = SemanticClass.Animal, ["goat"] = SemanticClass.Animal, ["lion"] = SemanticClass.Animal, ["dragon"] = SemanticClass.Animal,
    };

    public static SemanticClass Of(string meaning) => Classes.GetValueOrDefault(meaning, SemanticClass.Thing);
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
    //a front vowel makes it feminine, another vowel masculine, and a consonant neuter, or masculine
    //where there's no neuter. In an animacy system, a thing ending in a consonant is animate, as
    //some things are in Ojibwe. People of no particular sex are masculine, the usual default
    public Gender? Of(Lexeme noun)
    {
        var meaning = Semantics.Of(noun.Root.Meaning);
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
                    Vowel { Backness: VowelFrontness.Front } => Gender.Feminine,
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
