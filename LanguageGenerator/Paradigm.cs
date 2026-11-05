namespace LanguageGenerator;

/// <summary>
/// A category a part of speech inflects for, with the values this language distinguishes, such as
/// Number with SG, DU and PL.
/// </summary>
//Name is for display; Is says which category it is, by its values' type
public record InflectionalCategory(string Name, IReadOnlyList<Grammeme> Values)
{
    public bool Is<T>() where T : Grammeme => Values is [T, ..];
}

public static class InflectionalCategories
{
    //whether any of these is the category whose values are T, as in paradigm.Categories.Has<Person>()
    public static bool Has<T>(this IEnumerable<InflectionalCategory> categories) where T : Grammeme => categories.Any(c => c.Is<T>());
}

/// <summary>
/// Where a marker of grammatical features, its exponent, goes relative to the word it marks.
/// </summary>
public enum ExponentPosition
{
    Prefix,
    Suffix,
    //particles stand as separate words before or after the inflected one
    PrecedingParticle,
    FollowingParticle,
}

/// <summary>One place where a word's features are expressed.</summary>
//a slot covering several categories is fusional: each of its exponents expresses a whole
//combination of their values at once
public class ExponentSlot
{
    public required IReadOnlyList<InflectionalCategory> Categories { get; init; }
    public required ExponentPosition Position { get; init; }
    /// <summary>One exponent for every combination of the categories' values.</summary>
    public required IReadOnlyList<(IReadOnlyList<Grammeme> Values, Morpheme Exponent)> Exponents { get; init; }

    public Morpheme ExponentFor(IReadOnlyList<Grammeme> features) =>
        Exponents.First(e => e.Values.All(features.Contains)).Exponent;

    public override string ToString() => $"{Position} ({string.Join(" + ", Categories.Select(c => c.Name))})";
}

/// <summary>How one part of speech inflects.</summary>
//the morphological type of the language shows up here only as a tendency in how the slots were
//built: many categories per slot reads as fusional, one per slot as agglutinative, and particles as
//analytic, and a language can mix all three
public class Paradigm
{
    public required PartOfSpeech PartOfSpeech { get; init; }
    public required IReadOnlyList<InflectionalCategory> Categories { get; init; }
    /// <summary>Innermost first: the first slot sits closest to the root.</summary>
    public required IReadOnlyList<ExponentSlot> Slots { get; init; }

    /// <summary>Every combination of feature values, one per cell of the paradigm table.</summary>
    public IEnumerable<IReadOnlyList<Grammeme>> Cells => Combinations(Categories);

    public static IEnumerable<IReadOnlyList<Grammeme>> Combinations(IEnumerable<InflectionalCategory> categories)
    {
        IEnumerable<IReadOnlyList<Grammeme>> cells = [[]];
        foreach (var category in categories)
        {
            cells = cells.SelectMany(cell => category.Values.Select(value => (IReadOnlyList<Grammeme>)[.. cell, value]));
        }
        return cells;
    }

    //the inflected word, plus any particles, in order
    public IReadOnlyList<Word> Inflect(Lexeme lexeme, IReadOnlyList<Grammeme> features)
    {
        if (lexeme.PartOfSpeech != PartOfSpeech)
        {
            throw new ArgumentException($"A {lexeme.PartOfSpeech} can't be inflected with the {PartOfSpeech} paradigm.", nameof(lexeme));
        }
        foreach (var category in Categories)
        {
            if (category.Values.Count(features.Contains) != 1)
            {
                throw new ArgumentException($"Inflecting a {PartOfSpeech} needs exactly one {category.Name} value.", nameof(features));
            }
        }

        var morphemes = new List<Morpheme> { lexeme.Root };
        var covert = new List<Grammeme>();
        var before = new List<Word>();
        var after = new List<Word>();
        //each slot goes outside the ones before it
        foreach (var slot in Slots)
        {
            var exponent = slot.ExponentFor(features);
            //a zero exponent adds nothing to say, only features to gloss
            if (exponent.Form.Count == 0)
            {
                covert.AddRange(exponent switch
                {
                    Affix a => a.Grammemes,
                    Particle p => p.Grammemes,
                    Root => [],
                });
                continue;
            }
            switch (slot.Position)
            {
                case ExponentPosition.Prefix:
                    morphemes.Insert(0, exponent);
                    break;
                case ExponentPosition.Suffix:
                    morphemes.Add(exponent);
                    break;
                case ExponentPosition.PrecedingParticle:
                    before.Insert(0, new Word([exponent]));
                    break;
                case ExponentPosition.FollowingParticle:
                    after.Add(new Word([exponent]));
                    break;
            }
        }
        return [.. before, new Word(morphemes, covert), .. after];
    }
}
