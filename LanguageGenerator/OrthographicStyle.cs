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

public enum DentalFricativeSpelling
{
    //þ ð, as in Icelandic
    Thorn,
    //th dh, as in English or Albanian
    Digraph,
}

public enum DentalAffricateSpelling
{
    //ts dz, as in Japanese romanization or Albanian x's neighbor dz
    Digraph,
    //c dz, as in Czech, Polish or Hungarian
    SingleLetter,
}

/// <summary>How a language with aspirated stops tells them from plain ones.</summary>
public enum AspirationSpelling
{
    //ph th kh, as Hindi and Thai are romanized
    H,
    //p' t' k', as in Wade–Giles
    Apostrophe,
    //p t k for the aspirates and b d g for the plain stops, as in Pinyin, which only works in a
    //language without voiced stops for b d g to mean
    VoicedLetters,
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
    string PalatalGlide,
    DentalFricativeSpelling DentalFricatives = DentalFricativeSpelling.Thorn,
    string VelarNasal = "ŋ",
    string VelarFricative = "x",
    DentalAffricateSpelling DentalAffricates = DentalAffricateSpelling.Digraph,
    AspirationSpelling Aspiration = AspirationSpelling.H,
    string GlottalStop = "'",
    string PalatalNasal = "ñ",
    string PalatalStop = "ty",
    string CloseCentral = "î")
{
    //single sounds with a few established spellings each
    public static IReadOnlyList<string> SchwaOptions { get; } = ["ë", "ə", "e", "y"];
    public static IReadOnlyList<string> NearOpenFrontOptions { get; } = ["ä", "æ", "ae"];
    public static IReadOnlyList<string> VelarStopOptions { get; } = ["k", "c"];
    public static IReadOnlyList<string> PalatalGlideOptions { get; } = ["y", "j"];
    public static IReadOnlyList<string> VelarNasalOptions { get; } = ["ŋ", "ng"];
    public static IReadOnlyList<string> VelarFricativeOptions { get; } = ["x", "kh"];
    //an apostrophe, or the Hawaiian ʻokina, since a glottal stop has no Latin letter
    public static IReadOnlyList<string> GlottalStopOptions { get; } = ["'", "ʻ"];
    //Spanish ñ, Swahili ny, Portuguese nh
    public static IReadOnlyList<string> PalatalNasalOptions { get; } = ["ñ", "ny", "nh"];
    //Hungarian ty, Latvian ķ, or c, as in Albanian or the IPA
    public static IReadOnlyList<string> PalatalStopOptions { get; } = ["ty", "ķ", "c"];
    //Guarani and Polish y, Romanian î, or the IPA letter
    public static IReadOnlyList<string> CloseCentralOptions { get; } = ["y", "î", "ɨ"];

    //each aspirated stop and its plain one
    private static readonly Dictionary<Phoneme, Consonant> Unaspirated = new() { [Consonant.pʰ] = Consonant.p, [Consonant.tʰ] = Consonant.t, [Consonant.kʰ] = Consonant.k };
    private static readonly Dictionary<Phoneme, string> VoicedLetters = new() { [Consonant.p] = "b", [Consonant.t] = "d", [Consonant.k] = "g" };

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
        //affricates go with the fricatives beside them: Czech č, English ch and j, Turkish ç and c
        if (phoneme == Consonant.ʧ || phoneme == Consonant.ʤ)
        {
            var voiced = phoneme == Consonant.ʤ;
            return Postalveolars switch
            {
                PostalveolarSpelling.Hacek => voiced ? "ǧ" : "č",
                PostalveolarSpelling.Digraph => voiced ? "j" : "ch",
                PostalveolarSpelling.SingleLetter => voiced ? "c" : "ç",
                _ => throw new ArgumentOutOfRangeException(nameof(Postalveolars), Postalveolars, null),
            };
        }
        if (phoneme == Consonant.θ || phoneme == Consonant.ð)
        {
            var voiced = phoneme == Consonant.ð;
            return DentalFricatives == DentalFricativeSpelling.Thorn ? (voiced ? "ð" : "þ") : (voiced ? "dh" : "th");
        }
        if (Unaspirated.TryGetValue(phoneme, out var plain))
        {
            return Aspiration switch
            {
                AspirationSpelling.H => PlainStop(plain) + "h",
                AspirationSpelling.Apostrophe => PlainStop(plain) + "'",
                AspirationSpelling.VoicedLetters => PlainStop(plain),
                _ => throw new ArgumentOutOfRangeException(nameof(Aspiration), Aspiration, null),
            };
        }
        //with VoicedLetters, a plain stop beside its aspirate takes the voiced letter
        if (Aspiration == AspirationSpelling.VoicedLetters && VoicedLetters.TryGetValue(phoneme, out var voicedLetter)
            && Unaspirated.Any(u => u.Value == phoneme && inventory.Contains(u.Key)))
        {
            return voicedLetter;
        }
        if (phoneme == Consonant.kʷ || phoneme == Consonant.gʷ)
        {
            return Spell(phoneme == Consonant.kʷ ? Consonant.k : Consonant.g, inventory) + "w";
        }
        if (phoneme == Consonant.ʦ || phoneme == Consonant.ʣ)
        {
            return phoneme == Consonant.ʣ ? "dz" : DentalAffricates == DentalAffricateSpelling.Digraph ? "ts" : "c";
        }
        //a single rhotic is r. Beside a tap, a trill is rr, as in Spanish pero 'but' and perro 'dog'.
        //English's ɹ is r too, unless it's beside another rhotic, which no generated language is
        if (phoneme is Consonant { IsRhotic: true } rhotic)
        {
            var others = inventory.OfType<Consonant>().Count(c => c.IsRhotic && c != rhotic);
            return others == 0 ? "r"
                : rhotic == Consonant.r && inventory.Contains(Consonant.ɾ) ? "rr"
                : rhotic == Consonant.ɾ ? "r"
                : rhotic.IpaSymbol;
        }
        return phoneme == Vowel.ə ? Schwa
            : phoneme == Vowel.æ ? NearOpenFront
            : phoneme == Consonant.ʔ ? GlottalStop
            : phoneme == Consonant.ɲ ? PalatalNasal
            : phoneme == Consonant.c ? PalatalStop
            : phoneme == Vowel.ɨ ? CloseCentral
            : phoneme == Consonant.ŋ ? VelarNasal
            : phoneme == Consonant.x ? VelarFricative
            : PlainStop(phoneme);
    }

    //a phoneme's own letter, which for k is the language's letter for it
    private string PlainStop(Phoneme phoneme) =>
        phoneme == Consonant.k ? VelarStop : phoneme == Consonant.j ? PalatalGlide : phoneme.IpaSymbol;

    public override string ToString() =>
        $"{Postalveolars} postalveolars, {LongVowels} long vowels, {MidVowels} mid vowels, {DentalFricatives} dental fricatives, schwa {Schwa}, æ {NearOpenFront}, k {VelarStop}, j {PalatalGlide}, ŋ {VelarNasal}, x {VelarFricative}, "
        + $"{DentalAffricates} dental affricates, {Aspiration} aspirates, ʔ {GlottalStop}, ɲ {PalatalNasal}, c {PalatalStop}, ɨ {CloseCentral}";
}
