namespace LanguageGenerator;

/// <summary>How a language writes its phonemes.</summary>
//spellings must be unambiguous, so that any written word splits back into phonemes in only one way:
//a spelled "ae" can't be both æ and a + ɛ
public class Orthography
{
    private readonly Dictionary<Phoneme, string> spellings;

    public IReadOnlyDictionary<Phoneme, string> Spellings => spellings;
    /// <summary>The conventions the spellings come from, if they came from any.</summary>
    public OrthographicStyle? Style { get; }

    public Orthography(IReadOnlyDictionary<Phoneme, string> spellings, OrthographicStyle? style = null)
    {
        if (!IsUnambiguous(spellings.Values))
        {
            throw new ArgumentException($"These spellings are ambiguous: {string.Join(", ", spellings.Select(s => $"{s.Key.IpaSymbol}={s.Value}"))}", nameof(spellings));
        }
        this.spellings = new(spellings);
        Style = style;
    }

    /// <summary>IPA as an orthography, for debugging and for underlying forms.</summary>
    public static Orthography Ipa { get; } =
        new(Vowel.All.Concat<Phoneme>(Consonant.All).ToDictionary(p => p, p => p.IpaSymbol));

    public string Spell(Phoneme phoneme) => spellings.TryGetValue(phoneme, out var spelling)
        ? spelling
        : throw new ArgumentException($"This orthography has no spelling for {phoneme.IpaSymbol}.", nameof(phoneme));

    public string Spell(IEnumerable<Phoneme> phonemes) => string.Concat(phonemes.Select(Spell));

    //the Sardinas–Patterson test: spellings are unambiguous unless some chain of "dangling
    //suffixes", left over when one spelling starts another, eventually spells a whole word
    public static bool IsUnambiguous(IEnumerable<string> spellings)
    {
        var words = spellings.ToList();
        if (words.Count != words.Distinct(StringComparer.Ordinal).Count())
        {
            return false;
        }
        var code = words.ToHashSet(StringComparer.Ordinal);

        //what's left of each b after a different a it starts with
        static IEnumerable<string> Dangling(IEnumerable<string> a, IEnumerable<string> b) =>
            from x in a
            from y in b
            where y.Length > x.Length && y.StartsWith(x, StringComparison.Ordinal)
            select y[x.Length..];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var suffixes = Dangling(code, code).ToHashSet(StringComparer.Ordinal);
        while (suffixes.Count > 0)
        {
            if (suffixes.Overlaps(code))
            {
                return false;
            }
            seen.UnionWith(suffixes);
            suffixes = Dangling(code, suffixes).Concat(Dangling(suffixes, code)).Where(s => !seen.Contains(s)).ToHashSet(StringComparer.Ordinal);
        }
        return true;
    }
}
