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

/// <summary>A language's numerals, each a word of its own.</summary>
//Words has one for each number from 1 to 9 and for each power of the base (10 and 100, or 10, 20
//and 400), or in a restricted system for each number up to Limit, with Many past it. A numeral goes
//before or after its noun, outside any adjectives, and some languages leave the noun singular after
//one, as Turkish üç kurt 'three wolf' does
public record NumeralSystem(NumeralBase Base, int Limit, IReadOnlyDictionary<int, Lexeme> Words, Lexeme? Many,
    bool BeforeNoun, bool NounStaysSingular, bool LargestFirst)
{
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
