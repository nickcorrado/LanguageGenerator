namespace LanguageGenerator;

/// <summary>Sandhi: the changes sounds undergo where they meet.</summary>
public static class Sandhi
{
    //if a word has doubled phonemes in the onset, like ppa or nokks,
    //collapse them to one phoneme
    //n.b. this has no effect on geminates, which occur between syllables
    public static Syllable ElideDuplicates(Syllable syllable)
    {
        var onset = syllable.Onset;
        var onsetResult = new List<Consonant>();
        foreach (var c in onset)
        {
            if (onsetResult.Count == 0 || onsetResult.Last() != c)
            {
                onsetResult.Add(c);
            }
        }

        var result = new Syllable(new ConsonantCluster(onsetResult), syllable.Nucleus, syllable.Coda);
        return result;
    }
}

//rules a language applies across a whole word, so they see morpheme boundaries and the
//syllables on either side of them. Changes only ever use phonemes from the language's
//own inventory; where it has no suitable phoneme, the sound is left alone
/// <summary>Simplifies a doubled consonant to a single one.</summary>
public record Degemination;
public record NasalAssimilation;
public record IntervocalicVoicing;

/// <summary>A sound change a language applies across a whole word, where morphemes meet.</summary>
public union SandhiRule(Degemination, NasalAssimilation, IntervocalicVoicing)
{
    public List<Segment> Apply(List<Segment> segments, IReadOnlyList<Consonant> inventory) => this switch
    {
        Degemination => Degeminate(segments),
        NasalAssimilation => AssimilateNasals(segments, inventory),
        IntervocalicVoicing => VoiceBetweenVowels(segments, inventory),
    };

    public override string ToString() => this switch
    {
        Degemination => "degemination (tt → t)",
        NasalAssimilation => "nasal assimilation (np → mp)",
        IntervocalicVoicing => "intervocalic voicing (apa → aba)",
    };

    //the surviving consonant keeps the first one's morpheme, so across a boundary
    //the second morpheme can be left with no sound of its own
    private static List<Segment> Degeminate(List<Segment> segments)
    {
        var result = new List<Segment>();
        foreach (var segment in segments)
        {
            if (result.Count == 0 || segment.Phoneme is not Consonant || result[^1].Phoneme != segment.Phoneme)
            {
                result.Add(segment);
            }
        }
        return result;
    }

    //a nasal takes the place of articulation of a following stop or nasal
    private static List<Segment> AssimilateNasals(List<Segment> segments, IReadOnlyList<Consonant> inventory)
    {
        var result = new List<Segment>(segments);
        for (int i = 0; i < result.Count - 1; i++)
        {
            if (result[i].Phoneme is Consonant { Manner: ConsonantManner.Nasal } nasal
                && result[i + 1].Phoneme is Consonant { Manner: ConsonantManner.Plosive or ConsonantManner.Nasal } next
                && next.Place != nasal.Place
                && inventory.FirstOrDefault(c => c.Manner == ConsonantManner.Nasal && c.Place == next.Place) is { } assimilated)
            {
                result[i] = result[i] with { Phoneme = assimilated };
            }
        }
        return result;
    }

    //an unvoiced consonant between vowels takes its voiced counterpart
    private static List<Segment> VoiceBetweenVowels(List<Segment> segments, IReadOnlyList<Consonant> inventory)
    {
        var result = new List<Segment>(segments);
        for (int i = 1; i < result.Count - 1; i++)
        {
            if (result[i].Phoneme is Consonant { Voiced: false } consonant
                && result[i - 1].Phoneme is Vowel && result[i + 1].Phoneme is Vowel
                && inventory.FirstOrDefault(c => c.Voiced && c.Manner == consonant.Manner && c.Place == consonant.Place) is { } voiced)
            {
                result[i] = result[i] with { Phoneme = voiced };
            }
        }
        return result;
    }
}
