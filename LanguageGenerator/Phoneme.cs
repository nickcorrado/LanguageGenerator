namespace LanguageGenerator;

/// <summary>
/// A unit of sound. How it's written depends on the language, so spelling lives in each language's
/// Orthography.
/// </summary>
//IpaSymbol is for debugging and for writing underlying forms. Closed: every phoneme is a Vowel or a
//Consonant, so a switch over the two is exhaustive
public closed class Phoneme
{
    public string IpaSymbol { get; }

    protected Phoneme(string ipaSymbol)
    {
        IpaSymbol = ipaSymbol;
    }
}

public class Vowel : Phoneme
{
    public static Vowel i { get; } = new("i", VowelHeight.Close, VowelFrontness.Front, false, false);
    public static Vowel iː { get; } = new("iː", VowelHeight.Close, VowelFrontness.Front, false, true);
    public static Vowel e { get; } = new("e", VowelHeight.CloseMid, VowelFrontness.Front, false, false);
    public static Vowel ɛ { get; } = new("ɛ", VowelHeight.OpenMid, VowelFrontness.Front, false, false);
    public static Vowel æ { get; } = new("æ", VowelHeight.NearOpen, VowelFrontness.Front, false, false);
    public static Vowel a { get; } = new("a", VowelHeight.Open, VowelFrontness.Front, false, false);
    public static Vowel ə { get; } = new("ə", VowelHeight.Mid, VowelFrontness.Central, false, false);
    public static Vowel ɔ { get; } = new("ɔ", VowelHeight.OpenMid, VowelFrontness.Back, true, false);
    public static Vowel o { get; } = new("o", VowelHeight.CloseMid, VowelFrontness.Back, true, false);
    public static Vowel oː { get; } = new("oː", VowelHeight.CloseMid, VowelFrontness.Back, true, true);
    public static Vowel aː { get; } = new("aː", VowelHeight.Open, VowelFrontness.Front, false, true);
    public static Vowel eː { get; } = new("eː", VowelHeight.CloseMid, VowelFrontness.Front, false, true);

    /// <summary>Every vowel a language can have, from which the generator prunes.</summary>
    //declared after the vowels themselves, since static properties initialize in order
    public static IReadOnlyList<Vowel> All { get; } = [a, e, i, æ, ɛ, ə, ɔ, o, iː, oː, aː, eː];

    public VowelHeight Height { get; }
    public VowelFrontness Backness { get; }
    public bool Rounded { get; }
    public bool Long { get; }

    private Vowel(string ipaSymbol, VowelHeight vowelHeight, VowelFrontness vowelBackness, bool rounded, bool @long)
        : base(ipaSymbol)
    {
        Height = vowelHeight;
        Backness = vowelBackness;
        Rounded = rounded;
        Long = @long;
    }
}

public class Consonant : Phoneme
{
    public static Consonant m { get; } = new("m", ConsonantPlace.Labial, ConsonantManner.Nasal, true);
    public static Consonant n { get; } = new("n", ConsonantPlace.Dental, ConsonantManner.Nasal, true);
    public static Consonant p { get; } = new("p", ConsonantPlace.Labial, ConsonantManner.Plosive, false);
    public static Consonant b { get; } = new("b", ConsonantPlace.Labial, ConsonantManner.Plosive, true);
    public static Consonant t { get; } = new("t", ConsonantPlace.Dental, ConsonantManner.Plosive, false);
    public static Consonant d { get; } = new("d", ConsonantPlace.Dental, ConsonantManner.Plosive, true);
    public static Consonant k { get; } = new("k", ConsonantPlace.Velar, ConsonantManner.Plosive, false);
    public static Consonant g { get; } = new("g", ConsonantPlace.Velar, ConsonantManner.Plosive, true);
    public static Consonant s { get; } = new("s", ConsonantPlace.Dental, ConsonantManner.Sibilant, false);
    public static Consonant z { get; } = new("z", ConsonantPlace.Dental, ConsonantManner.Sibilant, true);
    public static Consonant ʃ { get; } = new("ʃ", ConsonantPlace.PostAlveolar, ConsonantManner.Sibilant, false);
    public static Consonant ʒ { get; } = new("ʒ", ConsonantPlace.PostAlveolar, ConsonantManner.Sibilant, true);
    public static Consonant f { get; } = new("f", ConsonantPlace.LabioDental, ConsonantManner.Fricative, false);
    public static Consonant v { get; } = new("v", ConsonantPlace.LabioDental, ConsonantManner.Fricative, true);
    public static Consonant ɹ { get; } = new("ɹ", ConsonantPlace.PostAlveolar, ConsonantManner.Approximant, true);
    public static Consonant l { get; } = new("l", ConsonantPlace.PostAlveolar, ConsonantManner.LateralApproximant, true);
    public static Consonant j { get; } = new("j", ConsonantPlace.Palatal, ConsonantManner.Approximant, true);
    public static Consonant w { get; } = new("w", ConsonantPlace.Velar, ConsonantManner.Approximant, true);
    public static Consonant θ { get; } = new("θ", ConsonantPlace.Dental, ConsonantManner.Fricative, false);
    public static Consonant ð { get; } = new("ð", ConsonantPlace.Dental, ConsonantManner.Fricative, true);
    public static Consonant ŋ { get; } = new("ŋ", ConsonantPlace.Velar, ConsonantManner.Nasal, true);
    public static Consonant h { get; } = new("h", ConsonantPlace.Glottal, ConsonantManner.Fricative, false);
    public static Consonant x { get; } = new("x", ConsonantPlace.Velar, ConsonantManner.Fricative, false);
    //the IPA ligatures, since a spelled tʃ couldn't be told from t and ʃ
    public static Consonant ʧ { get; } = new("ʧ", ConsonantPlace.PostAlveolar, ConsonantManner.Affricate, false);
    public static Consonant ʤ { get; } = new("ʤ", ConsonantPlace.PostAlveolar, ConsonantManner.Affricate, true);

    /// <summary>Every consonant a language can have, from which the generator prunes.</summary>
    public static IReadOnlyList<Consonant> All { get; } = [b, d, g, k, m, n, p, s, t, z, ʃ, ʒ, f, v, ɹ, l, j, w, θ, ð, ŋ, h, x, ʧ, ʤ];

    public ConsonantPlace Place { get; }
    public ConsonantManner Manner { get; }
    public bool Voiced { get; }

    /// <summary>Whether the airflow is obstructed: a stop, an affricate or a fricative, sibilant or not.</summary>
    public bool IsObstruent => Manner is ConsonantManner.Plosive or ConsonantManner.Affricate or ConsonantManner.Sibilant or ConsonantManner.Fricative;

    private Consonant(string ipaSymbol, ConsonantPlace consonantPlace, ConsonantManner consonantManner, bool voiced)
        : base(ipaSymbol)
    {
        Place = consonantPlace;
        Manner = consonantManner;
        Voiced = voiced;
    }
}

public enum VowelHeight
{
    Close, NearClose, CloseMid, Mid,
    OpenMid, NearOpen, Open
}

public enum VowelFrontness
{
    Front, Central, Back
}

public enum ConsonantPlace
{
    Labial, LabioDental, Dental, //more denti-alveolar
    PostAlveolar, Retroflex, Palatal,
    Velar, Glottal
}

public enum ConsonantManner
{
    Nasal, Plosive, Affricate, Sibilant, Fricative,
    Approximant, Flap, Trill, LateralApproximant
}
