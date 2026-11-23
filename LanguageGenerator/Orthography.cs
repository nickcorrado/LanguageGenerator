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

    /// <summary>How the orthography marks a stress its language's rule wouldn't give.</summary>
    public StressSpelling Stress { get; }

    //a stressed vowel, spelled with its mark, is distinct from every other spelling too
    public Orthography(IReadOnlyDictionary<Phoneme, string> spellings, StressSpelling stress, OrthographicStyle? style = null)
    {
        this.spellings = new(spellings);
        Stress = stress;
        Style = style;
        var all = Stress == StressSpelling.Unmarked ? spellings.Values : spellings.Values.Concat(spellings.Keys.OfType<Vowel>().Select(SpellStressed));
        if (!AreDistinct(all))
        {
            throw new ArgumentException($"Two phonemes share a spelling: {string.Join(", ", spellings.Select(s => $"{s.Key.IpaSymbol}={s.Value}"))}, stress {stress}", nameof(spellings));
        }
    }

    /// <summary>IPA as an orthography, for debugging and for underlying forms.</summary>
    public static Orthography Ipa { get; } =
        new(Vowel.All.Concat<Phoneme>(Consonant.All).ToDictionary(p => p, p => p.IpaSymbol), StressSpelling.Unmarked);

    public string Spell(Phoneme phoneme) => spellings.TryGetValue(phoneme, out var spelling)
        ? spelling
        : throw new ArgumentException($"This orthography has no spelling for {phoneme.IpaSymbol}.", nameof(phoneme));

    /*
      the vowel with the stress mark on its first letter, composed where Unicode has the letter, as
      á or ý, and otherwise stacked, as on a letter that already has a mark of its own (ā́), so it
      always has a spelling. An orthography that doesn't mark stress spells it as usual
     */
    public string SpellStressed(Vowel vowel) => Stress switch
    {
        StressSpelling.Unmarked => Spell(vowel),
        StressSpelling.Acute or StressSpelling.Grave => WithMark(Spell(vowel), Stress),
        _ => throw new ArgumentOutOfRangeException(nameof(Stress), Stress, null),
    };

    public static string WithMark(string spelling, StressSpelling mark) =>
        (spelling[..1] + CombiningMark(mark) + spelling[1..]).Normalize(System.Text.NormalizationForm.FormC);

    /// <summary>The combining character for a stress mark.</summary>
    public static char CombiningMark(StressSpelling mark) => mark switch
    {
        StressSpelling.Acute => '́',
        StressSpelling.Grave => '̀',
        StressSpelling.Unmarked => throw new ArgumentException("An unmarked stress has no mark.", nameof(mark)),
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };

    public string Spell(IEnumerable<Phoneme> phonemes) => string.Concat(phonemes.Select(Spell));

    public static bool AreDistinct(IEnumerable<string> spellings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return spellings.All(seen.Add);
    }
}
