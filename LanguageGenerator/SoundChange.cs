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
}
