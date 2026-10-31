namespace LanguageGenerator;

//a phoneme is only its sound. How it's written depends on the language, so spelling lives in
//each language's Orthography; IpaSymbol is for debugging and for writing underlying forms
public class Phoneme
{
    public string IpaSymbol { get; }

    protected Phoneme(string ipaSymbol)
    {
        IpaSymbol = ipaSymbol;
    }
}

//Strongly considering using this to define default categories, from which the
//generator prunes phonemes
public static class PhonemeManager
{
    public static IReadOnlySet<Phoneme> Phonemes { get; } = new HashSet<Phoneme>
    {
        Vowel.a, Vowel.e, Vowel.i, Vowel.æ,
        Vowel.ɛ, Vowel.ə, Vowel.ɔ, Vowel.o,
        Vowel.iː, Vowel.oː,
        Consonant.b, Consonant.d, Consonant.g, Consonant.k,
        Consonant.m, Consonant.n, Consonant.p, Consonant.s,
        Consonant.t, Consonant.z, Consonant.ʃ, Consonant.ʒ,
        Consonant.f, Consonant.v, Consonant.ɹ, Consonant.l,
    };

    public static IReadOnlyList<Vowel> Vowels { get; } = [
        Vowel.a, Vowel.e, Vowel.i, Vowel.æ, Vowel.ɛ, Vowel.ə, Vowel.ɔ, Vowel.o,
        Vowel.iː, Vowel.oː,
    ];

    public static IReadOnlyList<Consonant> Consonants { get; } =
    [
        Consonant.b, Consonant.d, Consonant.g, Consonant.k,
        Consonant.m, Consonant.n, Consonant.p, Consonant.s,
        Consonant.t, Consonant.z, Consonant.ʃ, Consonant.ʒ,
        Consonant.f, Consonant.v, Consonant.ɹ, Consonant.l,
        Consonant.j, Consonant.w,
    ];

    public static IReadOnlyList<Consonant> Finals { get; } =
    [
        Consonant.b, Consonant.d, Consonant.g, Consonant.k,
        Consonant.m, Consonant.n, Consonant.p, Consonant.s,
        Consonant.t, Consonant.z, Consonant.ʃ, Consonant.ʒ,
        Consonant.f, Consonant.v, Consonant.ɹ, Consonant.l,
        Consonant.j, Consonant.w,
    ];

    public static IReadOnlyList<Consonant> Liquids { get; } =
    [
        Consonant.l, Consonant.ɹ, Consonant.j, Consonant.w,
    ];

    public static IReadOnlyList<Consonant> Sibilants { get; } =
    [
        Consonant.s, Consonant.ʃ,
    ];
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
    public static Consonant ʃ { get; } = new("ʃ", ConsonantPlace.PostAlveolar, ConsonantManner.Fricative, false);
    public static Consonant ʒ { get; } = new("ʒ", ConsonantPlace.PostAlveolar, ConsonantManner.Fricative, true);
    public static Consonant f { get; } = new("f", ConsonantPlace.LabioDental, ConsonantManner.Fricative, false);
    public static Consonant v { get; } = new("v", ConsonantPlace.LabioDental, ConsonantManner.Fricative, true);
    public static Consonant ɹ { get; } = new("ɹ", ConsonantPlace.PostAlveolar, ConsonantManner.Approximant, true);
    public static Consonant l { get; } = new("l", ConsonantPlace.PostAlveolar, ConsonantManner.LateralApproximant, true);
    public static Consonant j { get; } = new("j", ConsonantPlace.Palatal, ConsonantManner.Approximant, true);
    public static Consonant w { get; } = new("w", ConsonantPlace.Velar, ConsonantManner.Approximant, true);

    public ConsonantPlace Place { get; }
    public ConsonantManner Manner { get; }
    public bool Voiced { get; }

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

//TODO add more exotic stuff like clicks
public enum ConsonantPlace
{
    Labial, LabioDental, Dental, //more denti-alveolar
    PostAlveolar, Retroflex, Palatal,
    Velar
}

public enum ConsonantManner
{
    Nasal, Plosive, Sibilant, Fricative,
    Approximant, Flap, Trill, LateralApproximant
}
