namespace LanguageGenerator;

//this class, I think, should have a method or methods that take in
//words or maybe syllables and modifies them according to some hard-
//coded rules about how sounds meet.
//all of this will be internal sandhi
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

        var result = new Syllable(new ConsonantCluster(onset), syllable.Nucleus, syllable.Coda);
        return result;
    }

    //convert unvoiced in onset to voiced
    public static Syllable VoiceOnset(Syllable syllable)
    {
        var onset = syllable.Onset;
        var onsetResult = new List<Consonant>();
        if (onset.Any(c => c.Voiced))
        {
            foreach (var o in onset)
            {
                var voicedOnset = onset.FirstOrDefault(c => c.Voiced && o.Manner == c.Manner && o.Place == c.Place);
                if (voicedOnset != null)
                {
                    onsetResult.Add(voicedOnset);
                }
            }
        }
        var result = new Syllable(new ConsonantCluster(onsetResult), syllable.Nucleus, syllable.Coda);
        return result;
    }
}
