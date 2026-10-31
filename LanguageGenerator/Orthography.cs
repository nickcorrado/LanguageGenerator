namespace LanguageGenerator;

//how a language writes its phonemes. Spellings must be unambiguous, so that any written word
//splits back into phonemes in only one way: a spelled "ae" can't be both æ and a + ɛ
public class Orthography
{
    private readonly Dictionary<Phoneme, string> spellings;

    public IReadOnlyDictionary<Phoneme, string> Spellings => spellings;

    public Orthography(IReadOnlyDictionary<Phoneme, string> spellings)
    {
        if (!IsUnambiguous(spellings.Values))
        {
            throw new ArgumentException($"These spellings are ambiguous: {string.Join(", ", spellings.Select(s => $"{s.Key.IpaSymbol}={s.Value}"))}", nameof(spellings));
        }
        this.spellings = new(spellings);
    }

    //IPA as an orthography, for debugging and for underlying forms
    public static Orthography Ipa { get; } =
        new(PhonemeManager.Vowels.Concat<Phoneme>(PhonemeManager.Consonants).ToDictionary(p => p, p => p.IpaSymbol));

    public string Spell(Phoneme phoneme) => spellings.TryGetValue(phoneme, out var spelling)
        ? spelling
        : throw new ArgumentException($"This orthography has no spelling for {phoneme.IpaSymbol}.", nameof(phoneme));

    public string Spell(IEnumerable<Phoneme> phonemes) => string.Concat(phonemes.Select(Spell));

    //real orthographies' ways of writing each sound, from which each language picks one. The
    //first is the plainest
    public static IReadOnlyDictionary<Phoneme, string[]> Options { get; } = new Dictionary<Phoneme, string[]>
    {
        [Vowel.a] = ["a"],
        [Vowel.e] = ["e", "é", "ei"],
        [Vowel.i] = ["i"],
        [Vowel.æ] = ["ä", "æ", "ae"],
        [Vowel.ɛ] = ["e", "è", "ê"],
        [Vowel.ə] = ["e", "ë", "ə", "y"],
        [Vowel.ɔ] = ["o", "ò", "ô", "å"],
        [Vowel.o] = ["o", "ó"],
        [Vowel.iː] = ["ī", "ii", "í"],
        [Vowel.oː] = ["ō", "oo", "ó"],
        [Consonant.b] = ["b"],
        [Consonant.d] = ["d"],
        [Consonant.g] = ["g"],
        [Consonant.k] = ["k", "c"],
        [Consonant.m] = ["m"],
        [Consonant.n] = ["n"],
        [Consonant.p] = ["p"],
        [Consonant.s] = ["s"],
        [Consonant.t] = ["t"],
        [Consonant.z] = ["z"],
        [Consonant.ʃ] = ["sh", "š", "x", "c"],
        [Consonant.ʒ] = ["zh", "ž", "j"],
        [Consonant.f] = ["f"],
        [Consonant.v] = ["v"],
        [Consonant.ɹ] = ["r"],
        [Consonant.l] = ["l"],
        [Consonant.j] = ["y", "j"],
        [Consonant.w] = ["w"],
    };

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
