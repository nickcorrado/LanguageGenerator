namespace LanguageGenerator;

/// <summary>
/// One sound as a sound change sees it: the sound before it, already changed, and the one after it,
/// not yet.
/// </summary>
public readonly record struct Site(Segment Segment, Segment? Previous, Segment? Next)
{
    public Phoneme Sound => Segment.Phoneme;
    public bool BetweenVowels => Previous?.Phoneme is Vowel && Next?.Phoneme is Vowel;
}

/// <summary>
/// A regular change from underlying to surface sounds, like a rule of sandhi, the changes sounds
/// undergo where they meet: which sounds it applies to, in what surroundings, and what they become.
/// </summary>
//each change is applied across a whole word, left to right, so it sees morpheme boundaries, and a
//language applies its changes in order, so one can feed or bleed the next. Changes only ever use
//phonemes from the language's own inventory; where it has no suitable phoneme, the sound is left
//alone. A change is a value, so a language's are a list that can be drawn, compared or extended
public sealed class SoundChange(string name, Func<Site, bool> when, Func<Site, Phonology, Phoneme?> becomes)
{
    public override string ToString() => name;

    public List<Segment> Apply(List<Segment> segments, Phonology phonology)
    {
        var result = new List<Segment>();
        for (int i = 0; i < segments.Count; i++)
        {
            var site = new Site(segments[i], result.LastOrDefault(), i + 1 < segments.Count ? segments[i + 1] : null);
            if (!when(site))
            {
                result.Add(segments[i]);
            }
            else if (becomes(site, phonology) is { } sound)
            {
                result.Add(segments[i] with { Phoneme = sound });
            }
        }
        return result;
    }

    //for changes that delete what they apply to
    private static Phoneme? Nothing(Site site, Phonology phonology) => null;

    /// <summary>Simplifies a doubled consonant to a single one.</summary>
    //the surviving consonant keeps the first one's morpheme, so across a boundary the second morpheme
    //can be left with no sound of its own
    public static SoundChange Degemination { get; } = new("degemination (tt → t)",
        site => site.Sound is Consonant && site.Previous?.Phoneme == site.Sound,
        Nothing);

    /// <summary>A nasal takes the place of articulation of a following stop or nasal.</summary>
    public static SoundChange NasalAssimilation { get; } = new("nasal assimilation (np → mp)",
        site => site.Sound is Consonant { Manner: ConsonantManner.Nasal } nasal
            && site.Next?.Phoneme is Consonant { Manner: ConsonantManner.Plosive or ConsonantManner.Nasal } next && next.Place != nasal.Place,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, place: ((Consonant)site.Next!.Phoneme).Place) ?? site.Sound);

    /// <summary>An unvoiced consonant between vowels takes its voiced counterpart.</summary>
    public static SoundChange IntervocalicVoicing { get; } = new("intervocalic voicing (apa → aba)",
        site => site.Sound is Consonant { Voiced: false } && site.BetweenVowels,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: true) ?? site.Sound);

    private static readonly Dictionary<Phoneme, Consonant> Lenited = new()
    {
        [Consonant.p] = Consonant.f,
        [Consonant.b] = Consonant.v,
        [Consonant.t] = Consonant.s,
        [Consonant.d] = Consonant.z,
        [Consonant.g] = Consonant.w,
    };

    /// <summary>A stop between vowels weakens to a continuant, as in Spanish or Tuscan.</summary>
    //after voicing where a language has both, so that p can weaken all the way to v. The inventory has
    //no x, so k stays, and d and t become sibilants, as the High German consonant shift made t into s
    public static SoundChange Lenition { get; } = new("lenition (apa → afa)",
        site => site.BetweenVowels && Lenited.ContainsKey(site.Sound),
        (site, phonology) => phonology.Consonants.Contains(Lenited[site.Sound]) ? Lenited[site.Sound] : site.Sound);

    /// <summary>An obstruent takes the voicing of an obstruent after it, as in Russian.</summary>
    public static SoundChange VoicingAssimilation { get; } = new("voicing assimilation (apda → abda)",
        site => site.Sound is Consonant { IsObstruent: true } consonant
            && site.Next?.Phoneme is Consonant { IsObstruent: true } next && next.Voiced != consonant.Voiced,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: ((Consonant)site.Next!.Phoneme).Voiced) ?? site.Sound);

    /// <summary>A voiced obstruent loses its voicing at the end of a word, as in German or Turkish.</summary>
    public static SoundChange FinalDevoicing { get; } = new("final devoicing (kab → kap)",
        site => site.Sound is Consonant { IsObstruent: true, Voiced: true } && site.Next is null,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: false) ?? site.Sound);

    /// <summary>The first of two vowels that meet at a morpheme boundary is lost.</summary>
    public static SoundChange VowelElision { get; } = new("vowel elision (ka-a → ka, ka-o → ko)",
        site => site.Sound is Vowel && site.Next is { Phoneme: Vowel } next && next.Morpheme != site.Segment.Morpheme,
        Nothing);
}
