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
    //the a of most languages, as in Spanish or Swahili, which is central, not front like æ
    public static Vowel a { get; } = new("a", VowelHeight.Open, VowelFrontness.Central, false, false);
    public static Vowel ə { get; } = new("ə", VowelHeight.Mid, VowelFrontness.Central, false, false);
    public static Vowel ɨ { get; } = new("ɨ", VowelHeight.Close, VowelFrontness.Central, false, false);
    public static Vowel ɔ { get; } = new("ɔ", VowelHeight.OpenMid, VowelFrontness.Back, true, false);
    public static Vowel o { get; } = new("o", VowelHeight.CloseMid, VowelFrontness.Back, true, false);
    public static Vowel oː { get; } = new("oː", VowelHeight.CloseMid, VowelFrontness.Back, true, true);
    public static Vowel aː { get; } = new("aː", VowelHeight.Open, VowelFrontness.Central, false, true);
    public static Vowel eː { get; } = new("eː", VowelHeight.CloseMid, VowelFrontness.Front, false, true);
    public static Vowel u { get; } = new("u", VowelHeight.Close, VowelFrontness.Back, true, false);
    public static Vowel uː { get; } = new("uː", VowelHeight.Close, VowelFrontness.Back, true, true);

    /// <summary>Every vowel a language can have, from which the generator prunes.</summary>
    //declared after the vowels themselves, since static properties initialize in order
    public static IReadOnlyList<Vowel> All { get; } = [a, e, i, æ, ɛ, ə, ɨ, ɔ, o, iː, oː, aː, eː, u, uː];

    public VowelHeight Height { get; }
    public VowelFrontness Backness { get; }
    public bool Rounded { get; }
    public bool Long { get; }

    /// <summary>The same vowel held longer, if there is one.</summary>
    public Vowel? Lengthened => Long ? null : All.FirstOrDefault(v => v.Long && v.Height == Height && v.Backness == Backness && v.Rounded == Rounded);

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
    public static Consonant ɹ { get; } = new("ɹ", ConsonantPlace.Dental, ConsonantManner.Approximant, true);
    public static Consonant r { get; } = new("r", ConsonantPlace.Dental, ConsonantManner.Trill, true);
    public static Consonant ɾ { get; } = new("ɾ", ConsonantPlace.Dental, ConsonantManner.Flap, true);
    public static Consonant l { get; } = new("l", ConsonantPlace.Dental, ConsonantManner.LateralApproximant, true);
    public static Consonant j { get; } = new("j", ConsonantPlace.Palatal, ConsonantManner.Approximant, true);
    //made with both lips and the back of the tongue at once
    public static Consonant w { get; } = new("w", ConsonantPlace.LabialVelar, ConsonantManner.Approximant, true);
    public static Consonant θ { get; } = new("θ", ConsonantPlace.Dental, ConsonantManner.Fricative, false);
    public static Consonant ð { get; } = new("ð", ConsonantPlace.Dental, ConsonantManner.Fricative, true);
    public static Consonant ŋ { get; } = new("ŋ", ConsonantPlace.Velar, ConsonantManner.Nasal, true);
    public static Consonant h { get; } = new("h", ConsonantPlace.Glottal, ConsonantManner.Fricative, false);
    public static Consonant x { get; } = new("x", ConsonantPlace.Velar, ConsonantManner.Fricative, false);
    //the IPA ligatures, since a spelled tʃ couldn't be told from t and ʃ
    public static Consonant ʧ { get; } = new("ʧ", ConsonantPlace.PostAlveolar, ConsonantManner.Affricate, false);
    public static Consonant ʤ { get; } = new("ʤ", ConsonantPlace.PostAlveolar, ConsonantManner.Affricate, true);
    public static Consonant ʦ { get; } = new("ʦ", ConsonantPlace.Dental, ConsonantManner.Affricate, false);
    public static Consonant ʣ { get; } = new("ʣ", ConsonantPlace.Dental, ConsonantManner.Affricate, true);
    public static Consonant ʔ { get; } = new("ʔ", ConsonantPlace.Glottal, ConsonantManner.Plosive, false);
    public static Consonant ɲ { get; } = new("ɲ", ConsonantPlace.Palatal, ConsonantManner.Nasal, true);
    //a stop made at the hard palate, like the ty of Hungarian or the k of English keep pushed forward
    public static Consonant c { get; } = new("c", ConsonantPlace.Palatal, ConsonantManner.Plosive, false);
    //a k made further back, at the uvula, as in Arabic, Quechua or Inuktitut
    public static Consonant q { get; } = new("q", ConsonantPlace.Uvular, ConsonantManner.Plosive, false);
    //a series of stops released with a puff of air, as in Mandarin, Hindi or Korean, where p and pʰ
    //are different sounds. English has them too, in pin against spin, but never to tell words apart
    public static Consonant pʰ { get; } = new("pʰ", ConsonantPlace.Labial, ConsonantManner.Plosive, false, aspirated: true);
    public static Consonant tʰ { get; } = new("tʰ", ConsonantPlace.Dental, ConsonantManner.Plosive, false, aspirated: true);
    public static Consonant kʰ { get; } = new("kʰ", ConsonantPlace.Velar, ConsonantManner.Plosive, false, aspirated: true);
    //velars said with rounded lips, as in English queen, but as single sounds, the commonest
    //labialized consonants by far
    public static Consonant kʷ { get; } = new("kʷ", ConsonantPlace.Velar, ConsonantManner.Plosive, false, labialized: true);
    public static Consonant gʷ { get; } = new("gʷ", ConsonantPlace.Velar, ConsonantManner.Plosive, true, labialized: true);

    /// <summary>Every consonant a language can have.</summary>
    public static IReadOnlyList<Consonant> All { get; } =
        [b, d, g, k, m, n, p, s, t, z, ʃ, ʒ, f, v, ɹ, r, ɾ, l, j, w, θ, ð, ŋ, h, x, ʧ, ʤ, ʦ, ʣ, ʔ, ɲ, c, q, pʰ, tʰ, kʰ, kʷ, gʷ];

    public ConsonantPlace Place { get; }
    public ConsonantManner Manner { get; }
    public bool Voiced { get; }
    public bool Aspirated { get; }
    public bool Labialized { get; }

    /// <summary>Whether the airflow is obstructed: a stop, an affricate or a fricative, sibilant or not.</summary>
    public bool IsObstruent => Manner is ConsonantManner.Plosive or ConsonantManner.Affricate or ConsonantManner.Sibilant or ConsonantManner.Fricative;

    /// <summary>Whether this is a trill, flap or approximant r.</summary>
    public bool IsRhotic => this == r || this == ɾ || this == ɹ;

    private Consonant(string ipaSymbol, ConsonantPlace consonantPlace, ConsonantManner consonantManner, bool voiced, bool aspirated = false, bool labialized = false)
        : base(ipaSymbol)
    {
        Place = consonantPlace;
        Manner = consonantManner;
        Voiced = voiced;
        Aspirated = aspirated;
        Labialized = labialized;
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
    Velar, LabialVelar, Uvular, Glottal
}

public enum ConsonantManner
{
    Nasal, Plosive, Affricate, Sibilant, Fricative,
    Approximant, Flap, Trill, LateralApproximant
}
