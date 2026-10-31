namespace LanguageGenerator;

//a category a part of speech inflects for, with the values this language distinguishes,
//e.g. Number with SG, DU and PL
public record InflectionalCategory(string Name, IReadOnlyList<Tagmeme> Values);

public enum ExponentPosition
{
    Prefix,
    Suffix,
    //particles stand as separate words before or after the inflected one
    PrecedingParticle,
    FollowingParticle,
}

//one place where a word's features are expressed. A slot covering several categories is
//fusional: each of its exponents expresses a whole combination of their values at once
public class ExponentSlot
{
    public required IReadOnlyList<InflectionalCategory> Categories { get; init; }
    public required ExponentPosition Position { get; init; }
    //one exponent for every combination of the categories' values
    public required IReadOnlyList<(IReadOnlyList<Tagmeme> Values, Morpheme Exponent)> Exponents { get; init; }

    public Morpheme ExponentFor(IReadOnlyList<Tagmeme> features) =>
        Exponents.First(e => e.Values.All(features.Contains)).Exponent;

    public override string ToString() => $"{Position} ({string.Join(" + ", Categories.Select(c => c.Name))})";
}

//how one part of speech inflects. The morphological type of the language shows up here only
//as a tendency in how the slots were built: many categories per slot reads as fusional, one per
//slot as agglutinative, and particles as analytic, and a language can mix all three
public class Paradigm
{
    public required PartOfSpeech PartOfSpeech { get; init; }
    public required IReadOnlyList<InflectionalCategory> Categories { get; init; }
    //innermost first: the first slot sits closest to the root
    public required IReadOnlyList<ExponentSlot> Slots { get; init; }

    //every combination of feature values, one per cell of the paradigm table
    public IEnumerable<IReadOnlyList<Tagmeme>> Cells => Combinations(Categories);

    public static IEnumerable<IReadOnlyList<Tagmeme>> Combinations(IEnumerable<InflectionalCategory> categories)
    {
        IEnumerable<IReadOnlyList<Tagmeme>> cells = [[]];
        foreach (var category in categories)
        {
            cells = cells.SelectMany(cell => category.Values.Select(value => (IReadOnlyList<Tagmeme>)[.. cell, value]));
        }
        return cells;
    }

    //the inflected word, plus any particles, in order
    public IReadOnlyList<Word> Inflect(Lexeme lexeme, IReadOnlyList<Tagmeme> features)
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
        var before = new List<Word>();
        var after = new List<Word>();
        //each slot goes outside the ones before it
        foreach (var slot in Slots)
        {
            var exponent = slot.ExponentFor(features);
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
        return [.. before, new Word(morphemes), .. after];
    }
}
