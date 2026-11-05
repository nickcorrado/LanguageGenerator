namespace LanguageGenerator;

//the conventions an orthography is built from. Unlike the rest of a language, orthographies are
//usually designed and then standardized, so a convention, once chosen, covers every sound it
//applies to: a language that marks one long vowel with a macron marks them all that way
public enum PostalveolarSpelling
{
    //š ž, as in Czech or Lithuanian
    Hacek,
    //sh zh, as in English or Albanian
    Digraph,
    //x j, as in Basque or Portuguese
    SingleLetter,
}

public enum LongVowelSpelling
{
    //ī ō, as in Latvian
    Macron,
    //í ó, as in Czech
    Acute,
    //ii oo, as in Finnish
    Doubled,
}

/// <summary>
/// How /e/ is told apart from /ɛ/, and /o/ from /ɔ/, in a language that has both.
/// </summary>
public enum MidVowelSpelling
{
    //è ò for the open vowels, as in Italian dictionaries
    GraveOnOpen,
    //é ó for the close vowels, as in Portuguese
    AcuteOnClose,
    //ê ô for the open vowels
    CircumflexOnOpen,
}

public record OrthographicStyle(
    PostalveolarSpelling Postalveolars,
    LongVowelSpelling LongVowels,
    MidVowelSpelling MidVowels,
    string Schwa,
    string NearOpenFront,
    string VelarStop,
    string PalatalGlide)
{
    //single sounds with a few established spellings each
    public static IReadOnlyList<string> SchwaOptions { get; } = ["ë", "ə", "e", "y"];
    public static IReadOnlyList<string> NearOpenFrontOptions { get; } = ["ä", "æ", "ae"];
    public static IReadOnlyList<string> VelarStopOptions { get; } = ["k", "c"];
    public static IReadOnlyList<string> PalatalGlideOptions { get; } = ["y", "j"];

    private static readonly Dictionary<string, string> Macrons = new() { ["a"] = "ā", ["e"] = "ē", ["i"] = "ī", ["o"] = "ō", ["u"] = "ū" };
    private static readonly Dictionary<string, string> Acutes = new() { ["a"] = "á", ["e"] = "é", ["i"] = "í", ["o"] = "ó", ["u"] = "ú" };
    private static readonly Dictionary<string, string> Graves = new() { ["e"] = "è", ["o"] = "ò" };
    private static readonly Dictionary<string, string> Circumflexes = new() { ["e"] = "ê", ["o"] = "ô" };

    //the close and open members of each mid vowel pair, and the letter they share
    private static readonly (Vowel Close, Vowel Open, string Letter)[] MidVowelPairs = [(Vowel.e, Vowel.ɛ, "e"), (Vowel.o, Vowel.ɔ, "o")];

    //every phoneme in the inventory, spelled by these conventions. Anything no convention covers
    //is written with its IPA letter, which for these phonemes is also the ordinary Latin letter
    public Dictionary<Phoneme, string> Spell(IReadOnlyCollection<Phoneme> inventory) =>
        inventory.ToDictionary(p => p, p => Spell(p, inventory));

    private string Spell(Phoneme phoneme, IReadOnlyCollection<Phoneme> inventory)
    {
        if (phoneme is Vowel { Long: true } longVowel)
        {
            var letter = longVowel.IpaSymbol.TrimEnd('ː');
            return LongVowels switch
            {
                LongVowelSpelling.Macron => Macrons[letter],
                LongVowelSpelling.Acute => Acutes[letter],
                LongVowelSpelling.Doubled => letter + letter,
                _ => throw new ArgumentOutOfRangeException(nameof(LongVowels), LongVowels, null),
            };
        }
        foreach (var (close, open, letter) in MidVowelPairs)
        {
            if (phoneme != close && phoneme != open)
            {
                continue;
            }
            //a vowel without its partner needs no mark
            if (!inventory.Contains(close) || !inventory.Contains(open))
            {
                return letter;
            }
            return (MidVowels, phoneme == open) switch
            {
                (MidVowelSpelling.GraveOnOpen, true) => Graves[letter],
                (MidVowelSpelling.CircumflexOnOpen, true) => Circumflexes[letter],
                (MidVowelSpelling.AcuteOnClose, false) => Acutes[letter],
                _ => letter,
            };
        }
        if (phoneme == Consonant.ʃ || phoneme == Consonant.ʒ)
        {
            var voiced = phoneme == Consonant.ʒ;
            return Postalveolars switch
            {
                PostalveolarSpelling.Hacek => voiced ? "ž" : "š",
                PostalveolarSpelling.Digraph => voiced ? "zh" : "sh",
                PostalveolarSpelling.SingleLetter => voiced ? "j" : "x",
                _ => throw new ArgumentOutOfRangeException(nameof(Postalveolars), Postalveolars, null),
            };
        }
        return phoneme == Vowel.ə ? Schwa
            : phoneme == Vowel.æ ? NearOpenFront
            : phoneme == Consonant.k ? VelarStop
            : phoneme == Consonant.j ? PalatalGlide
            : phoneme == Consonant.ɹ ? "r"
            : phoneme.IpaSymbol;
    }

    public override string ToString() =>
        $"{Postalveolars} postalveolars, {LongVowels} long vowels, {MidVowels} mid vowels, schwa {Schwa}, æ {NearOpenFront}, k {VelarStop}, j {PalatalGlide}";
}
