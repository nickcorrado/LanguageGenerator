namespace LanguageGenerator;

public class Phoneme
{
    public string IpaSymbol { get; set; }
    //should be a list of transcriptions, if anything, I think
    //or perhaps there should be a Roman transcription and a
    //few others. When a symbol is picked, a transcription
    //should be picked from the list
    public string Transcription { get; set; }
}

//Strongly considering using this to define default categories, from which the
//generator prunes phonemes
public static class PhonemeManager
{
    public static HashSet<Phoneme> Phonemes { get; private set; } =
    [
        Vowel.a, Vowel.e, Vowel.i, Vowel.æ,
        Vowel.ɛ, Vowel.ə, Vowel.ɔ, Vowel.o,
        Vowel.iː, Vowel.oː,
        Consonant.b, Consonant.d, Consonant.g, Consonant.k,
        Consonant.m, Consonant.n, Consonant.p, Consonant.s,
        Consonant.t, Consonant.z, Consonant.ʃ, Consonant.ʒ,
        Consonant.f, Consonant.v, Consonant.ɹ, Consonant.l,
    ];

    public static List<Vowel> Vowels { get; private set; } = [
        Vowel.a, Vowel.e, Vowel.i, Vowel.æ, Vowel.ɛ, Vowel.ə, Vowel.ɔ, Vowel.o,
        Vowel.iː, Vowel.oː,
    ];

    public static List<Consonant> Consonants { get; private set; } =
    [
        Consonant.b, Consonant.d, Consonant.g, Consonant.k,
        Consonant.m, Consonant.n, Consonant.p, Consonant.s,
        Consonant.t, Consonant.z, Consonant.ʃ, Consonant.ʒ,
        Consonant.f, Consonant.v, Consonant.ɹ, Consonant.l,
        Consonant.j, Consonant.w,
    ];

    public static List<Consonant> Finals { get; private set; } =
    [
        Consonant.b, Consonant.d, Consonant.g, Consonant.k,
        Consonant.m, Consonant.n, Consonant.p, Consonant.s,
        Consonant.t, Consonant.z, Consonant.ʃ, Consonant.ʒ,
        Consonant.f, Consonant.v, Consonant.ɹ, Consonant.l,
        Consonant.j, Consonant.w,
    ];

    public static List<Consonant> Liquids { get; private set; } =
    [
        Consonant.l, Consonant.ɹ, Consonant.j, Consonant.w,
    ];

    public static List<Consonant> Sibilants { get; private set; } =
    [
        Consonant.s, Consonant.ʃ,
    ];

    //consonants
    //by place
    //public static Category<Consonant> Labials { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Place == ConsonantPlace.Labial));
    //public static Category<Consonant> Dentals { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Place == ConsonantPlace.Dental));
    //public static Category<Consonant> PostAlveolars { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Place == ConsonantPlace.PostAlveolar));
    //public static Category<Consonant> Retroflexes { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Place == ConsonantPlace.Retroflex));
    //public static Category<Consonant> Velars { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Place == ConsonantPlace.Velar));
    //public static Category<Consonant> LabioDentals { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Place == ConsonantPlace.LabioDental));

    ////by manner
    //public static Category<Consonant> Nasals { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Manner == ConsonantManner.Nasal));
    //public static Category<Consonant> Plosives { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Manner == ConsonantManner.Plosive));
    //public static Category<Consonant> Sibilants { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Manner == ConsonantManner.Sibilant));
    //public static Category<Consonant> Fricatives { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Manner == ConsonantManner.Fricative));
    //public static Category<Consonant> Approximants { get; private set; } = new Category<Consonant>(Consonants.Where(c => c.Manner is ConsonantManner.Approximant or ConsonantManner.LateralApproximant));

    ////list of categories! we need this so that some can be pruned
    //public static List<Category<Consonant>> ConsonantCategories { get; private set; } = new List<Category<Consonant>>() { Labials, Dentals, PostAlveolars, Retroflexes, Velars, LabioDentals, Nasals, Plosives, Sibilants, Fricatives, Approximants };

    //public static Category<Vowel> Opens { get; private set; } = new Category<Vowel>(Vowels.Where(v => v.Height is VowelHeight.NearOpen or VowelHeight.Open));
    //public static Category<Vowel> Mids { get; private set; } = new Category<Vowel>(Vowels.Where(v => v.Height is VowelHeight.CloseMid or VowelHeight.Mid));
    //public static Category<Vowel> Closes { get; private set; } = new Category<Vowel>(Vowels.Where(v => v.Height is VowelHeight.Close or VowelHeight.CloseMid));
    //public static List<Category<Vowel>> VowelCategories { get; private set; } = new List<Category<Vowel>>() { Opens, Mids, Closes };
}

//This is more readable, but I don't love the need for pragmas
//TODO change to properties?
public class Vowel : Phoneme
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static Vowel i = new("i", "i", VowelHeight.Close, VowelFrontness.Front, false, false);
    public static Vowel iː = new("iː", "ī", VowelHeight.Close, VowelFrontness.Front, false, true);
    public static Vowel e = new("e", "ei", VowelHeight.CloseMid, VowelFrontness.Front, false, false);
    public static Vowel ɛ = new("ɛ", "e", VowelHeight.OpenMid, VowelFrontness.Front, false, false);
    public static Vowel æ = new("æ", "ae", VowelHeight.NearOpen, VowelFrontness.Front, false, false);
    public static Vowel a = new("a", "a", VowelHeight.Open, VowelFrontness.Front, false, false);
    public static Vowel ə = new("ə", "uh", VowelHeight.Mid, VowelFrontness.Central, false, false);
    public static Vowel ɔ = new("ɔ", "aw", VowelHeight.OpenMid, VowelFrontness.Back, true, false);
    public static Vowel o = new("o", "oh", VowelHeight.CloseMid, VowelFrontness.Back, true, false);
    public static Vowel oː = new("oː", "ō", VowelHeight.CloseMid, VowelFrontness.Back, true, true);

#pragma warning restore CA2211 // Non-constant fields should not be visible

    public VowelHeight Height { get; private set; }
    public VowelFrontness Backness { get; private set; }
    public bool Rounded { get; private set; }
    public bool Long { get; private set; }

    private Vowel(string ipaSymbol, string transcription, VowelHeight vowelHeight, VowelFrontness vowelBackness, bool rounded, bool @long)
    {
        Height = vowelHeight;
        Backness = vowelBackness;
        Rounded = rounded;
        Transcription = transcription;
        IpaSymbol = ipaSymbol;
        Long = @long;
    }
}

public class Consonant : Phoneme
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static Consonant m = new("m", "m", ConsonantPlace.Labial, ConsonantManner.Nasal, true);
    public static Consonant n = new("n", "n", ConsonantPlace.Dental, ConsonantManner.Nasal, true);
    public static Consonant p = new("p", "p", ConsonantPlace.Labial, ConsonantManner.Plosive, false);
    public static Consonant b = new("b", "b", ConsonantPlace.Labial, ConsonantManner.Plosive, true);
    public static Consonant t = new("t", "t", ConsonantPlace.Dental, ConsonantManner.Plosive, false);
    public static Consonant d = new("d", "d", ConsonantPlace.Dental, ConsonantManner.Plosive, true);
    public static Consonant k = new("k", "k", ConsonantPlace.Velar, ConsonantManner.Plosive, false);
    public static Consonant g = new("g", "g", ConsonantPlace.Velar, ConsonantManner.Plosive, true);
    public static Consonant s = new("s", "s", ConsonantPlace.Dental, ConsonantManner.Sibilant, false);
    public static Consonant z = new("z", "z", ConsonantPlace.Dental, ConsonantManner.Sibilant, true);
    public static Consonant ʃ = new("ʃ", "sh", ConsonantPlace.PostAlveolar, ConsonantManner.Fricative, false);
    public static Consonant ʒ = new("ʒ", "zh", ConsonantPlace.PostAlveolar, ConsonantManner.Fricative, true);
    public static Consonant f = new("f", "f", ConsonantPlace.LabioDental, ConsonantManner.Fricative, false);
    public static Consonant v = new("v", "v", ConsonantPlace.LabioDental, ConsonantManner.Fricative, true);
    public static Consonant ɹ = new("ɹ", "r", ConsonantPlace.PostAlveolar, ConsonantManner.Approximant, true);
    public static Consonant l = new("l", "l", ConsonantPlace.PostAlveolar, ConsonantManner.LateralApproximant, true);
    public static Consonant j = new("j", "y", ConsonantPlace.Palatal, ConsonantManner.Approximant, true);
    public static Consonant w = new("w", "w", ConsonantPlace.Velar, ConsonantManner.Approximant, true);

#pragma warning restore CA2211 // Non-constant fields should not be visible

    public ConsonantPlace Place { get; private set; }
    public ConsonantManner Manner { get; private set; }
    public bool Voiced { get; private set; }

    private Consonant(string ipaSymbol, string transcription, ConsonantPlace consonantPlace, ConsonantManner consonantManner, bool voiced)
    {
        Place = consonantPlace;
        Manner = consonantManner;
        Voiced = voiced;
        Transcription = transcription;
        IpaSymbol = ipaSymbol;
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
