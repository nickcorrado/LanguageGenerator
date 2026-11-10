namespace LanguageGenerator;

/// <summary>How a language builds numbers past its first few words.</summary>
//WALS 131A
public enum NumeralBase
{
    //tens: 37 is three ten seven, as in Mandarin sān shí qī
    Decimal,
    //twenties, with tens below twenty: 37 is twenty ten seven, as in Basque hogeita hamazazpi
    Vigesimal,
    //words for only the first few numbers, and one word for 'many' past them
    Restricted,
}

/// <summary>How a language says 'first', 'second', 'seventh' and the rest.</summary>
//WALS 53A, by what the first few ordinals are, as English has first, second, third-th
public enum OrdinalType
{
    //no ordinals of their own: the cardinal does, as in 'the three day'
    Cardinal,
    //a word for 'first', and cardinals after it
    FirstThenCardinal,
    //every ordinal built from its cardinal, 'one-th', 'two-th', 'three-th'
    Derived,
    //a word for 'first', and the rest built from cardinals, the commonest type
    FirstThenDerived,
    //words for 'first' and 'second', and the rest built from cardinals, as in English
    FirstSecondThenDerived,
}

/// <summary>
/// A language's ordinals: words of their own for the first one or two, if it has them, and a marker
/// that makes the rest from cardinals, if it builds them.
/// </summary>
//the marker goes on the whole numeral, as English -th goes on thirty-seven, so a prefix goes on its
//first word and a suffix on its last. Only the ordinal alone is suppletive: 21st is twenty-one-th,
//as in German einundzwanzigste, not twenty-first
public record Ordinals(OrdinalType Type, Lexeme? First, Lexeme? Second, (ExponentPosition Position, Morpheme Marker)? Marking);

/// <summary>A language's numerals, each a word of its own.</summary>
//Words has one for each number from 1 to 9 and for each power of the base (10 and 100, or 10, 20
//and 400), or in a restricted system for each number up to Limit, with Many past it. A numeral goes
//before or after its noun, outside any adjectives, and some languages leave the noun singular after
//one, as Turkish üç kurt 'three wolf' does
public record NumeralSystem(NumeralBase Base, int Limit, IReadOnlyDictionary<int, Lexeme> Words, Lexeme? Many,
    bool BeforeNoun, bool NounStaysSingular, bool LargestFirst, Ordinals Ordinals)
{
    /// <summary>The words for an ordinal, as in 'the seventh day'.</summary>
    public IReadOnlyList<Word> SayOrdinal(int number)
    {
        if (number == 1 && Ordinals.First is { } first)
        {
            return [new Word([first.Root])];
        }
        if (number == 2 && Ordinals.Second is { } second)
        {
            return [new Word([second.Root])];
        }
        List<Word> words = [.. Say(number).Select(n => new Word([n.Root]))];
        if (Ordinals.Marking is not (var position, var marker))
        {
            return words;
        }
        switch (position)
        {
            case ExponentPosition.Prefix:
                words[0] = words[0] with { Morphemes = [marker, .. words[0].Morphemes] };
                return words;
            case ExponentPosition.Suffix:
                words[^1] = words[^1] with { Morphemes = [.. words[^1].Morphemes, marker] };
                return words;
            case ExponentPosition.PrecedingParticle:
                return [new Word([marker]), .. words];
            case ExponentPosition.FollowingParticle:
                return [.. words, new Word([marker])];
            default:
                throw new ArgumentOutOfRangeException(nameof(position), position, null);
        }
    }

    //the largest number the clause format can ask for, which every base can say
    public const int Largest = 999;

    //the words for a number: each power of the base it has, after how many of it if more than one,
    //and then the units, largest first, as in English thirty-seven, or smallest first, as in German
    //siebenunddreißig
    public IReadOnlyList<Lexeme> Say(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Largest);
        if (Base == NumeralBase.Restricted)
        {
            return [number <= Limit ? Words[number] : Many!];
        }

        int[] powers = Base == NumeralBase.Decimal ? [100, 10] : [400, 20, 10];
        var parts = new List<IReadOnlyList<Lexeme>>();
        var rest = number;
        foreach (var power in powers)
        {
            var times = rest / power;
            if (times == 0)
            {
                continue;
            }
            parts.Add([.. times > 1 ? Say(times) : [], Words[power]]);
            rest %= power;
        }
        if (rest > 0)
        {
            parts.Add([Words[rest]]);
        }
        if (!LargestFirst)
        {
            parts.Reverse();
        }
        return [.. parts.SelectMany(p => p)];
    }
}
