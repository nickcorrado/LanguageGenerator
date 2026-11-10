namespace LanguageGenerator;

/// <summary>How a language writes its phonemes.</summary>
//no two phonemes may share a spelling, but spellings may run together: with s, h and sh, a written
//"sh" could in principle be s + h, the way real orthographies tolerate English "mishap"
public class Orthography
{
    private readonly Dictionary<Phoneme, string> spellings;

    public IReadOnlyDictionary<Phoneme, string> Spellings => spellings;
    /// <summary>The conventions the spellings come from, if they came from any.</summary>
    public OrthographicStyle? Style { get; }

    public Orthography(IReadOnlyDictionary<Phoneme, string> spellings, OrthographicStyle? style = null)
    {
        if (!AreDistinct(spellings.Values))
        {
            throw new ArgumentException($"Two phonemes share a spelling: {string.Join(", ", spellings.Select(s => $"{s.Key.IpaSymbol}={s.Value}"))}", nameof(spellings));
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

    public static bool AreDistinct(IEnumerable<string> spellings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return spellings.All(seen.Add);
    }
}
