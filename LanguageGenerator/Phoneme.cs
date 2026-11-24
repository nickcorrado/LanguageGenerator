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

    /*
      falling diphthongs, which glide from one vowel toward a close one in a single syllable, as in
      English eye or cow, commonest first. Each is one phoneme, as long vowels are, so it's one
      nucleus. They're written as PHOIBLE writes them, without the IPA's mark for the second part not
      being a syllable of its own, since a syllable break shows where two vowels are in hiatus
      instead. Rising diphthongs, as in Spanish pie, are a glide before a vowel, in the onset
     */
    public static Vowel ai { get; } = new("ai", a, i);
    public static Vowel au { get; } = new("au", a, u);
    public static Vowel ei { get; } = new("ei", e, i);
    public static Vowel ou { get; } = new("ou", o, u);
    public static Vowel oi { get; } = new("oi", o, i);
    public static Vowel ui { get; } = new("ui", u, i);

    /// <summary>Every vowel a language can have, from which the generator prunes.</summary>
    //declared after the vowels themselves, since static properties initialize in order
    public static IReadOnlyList<Vowel> All { get; } = [a, e, i, æ, ɛ, ə, ɨ, ɔ, o, iː, oː, aː, eː, u, uː, ai, au, ei, ou, oi, ui];

    /// <summary>Every diphthong, commonest first.</summary>
    public static IReadOnlyList<Vowel> Diphthongs { get; } = [ai, au, ei, ou, oi, ui];

    //a diphthong's height, backness and rounding are its first part's
    public VowelHeight Height { get; }
    public VowelFrontness Backness { get; }
    public bool Rounded { get; }
    public bool Long { get; }

    /// <summary>The vowel a diphthong starts on, or null for a single vowel.</summary>
    public Vowel? Start { get; }
    /// <summary>The close vowel a diphthong glides toward, or null for a single vowel.</summary>
    public Vowel? Offglide { get; }
    public bool IsDiphthong => Offglide is not null;

    /// <summary>Whether the vowel fills the time of two, as a long vowel or a diphthong does, making its syllable heavy.</summary>
    public bool Heavy => Long || IsDiphthong;

    /// <summary>The same vowel held longer, if there is one. A diphthong has none.</summary>
    public Vowel? Lengthened => Long || IsDiphthong ? null
        : All.FirstOrDefault(v => v.Long && v.Height == Height && v.Backness == Backness && v.Rounded == Rounded);

    /// <summary>The diphthong that starts on one vowel and glides toward the other, if there is one.</summary>
    public static Vowel? DiphthongOf(Phoneme start, Phoneme offglide) => Diphthongs.FirstOrDefault(d => d.Start == start && d.Offglide == offglide);

    private Vowel(string ipaSymbol, VowelHeight vowelHeight, VowelFrontness vowelBackness, bool rounded, bool @long)
        : base(ipaSymbol)
    {
        Height = vowelHeight;
        Backness = vowelBackness;
        Rounded = rounded;
        Long = @long;
    }

    private Vowel(string ipaSymbol, Vowel start, Vowel offglide)
        : this(ipaSymbol, start.Height, start.Backness, start.Rounded, false)
    {
        Start = start;
        Offglide = offglide;
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
    //the softened b and g of Spanish lobo and lago, made with the lips or the back of the tongue
    //close but not touching, here as sounds of their own
    public static Consonant β { get; } = new("β", ConsonantPlace.Labial, ConsonantManner.Fricative, true);
    public static Consonant ɣ { get; } = new("ɣ", ConsonantPlace.Velar, ConsonantManner.Fricative, true);
    //the rest of the palatals: the fricative of German ich, the lateral of Italian figlio, and the
    //voiced partner of c, the gy of Hungarian
    public static Consonant ç { get; } = new("ç", ConsonantPlace.Palatal, ConsonantManner.Fricative, false);
    public static Consonant ʎ { get; } = new("ʎ", ConsonantPlace.Palatal, ConsonantManner.LateralApproximant, true);
    public static Consonant ɟ { get; } = new("ɟ", ConsonantPlace.Palatal, ConsonantManner.Plosive, true);

    /// <summary>Every consonant a language can have.</summary>
    public static IReadOnlyList<Consonant> All { get; } =
        [b, d, g, k, m, n, p, s, t, z, ʃ, ʒ, f, v, ɹ, r, ɾ, l, j, w, θ, ð, ŋ, h, x, ʧ, ʤ, ʦ, ʣ, ʔ, ɲ, c, q, pʰ, tʰ, kʰ, kʷ, gʷ, β, ɣ, ç, ʎ, ɟ];

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
