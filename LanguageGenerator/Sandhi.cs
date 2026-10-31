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

        var result = new Syllable(new ConsonantCluster(onsetResult), syllable.Nucleus, syllable.Coda);
        return result;
    }

    //convert unvoiced in onset to voiced, using the language's own inventory so that
    //voicing never introduces a phoneme the language doesn't have. A consonant with no
    //voiced counterpart in the inventory is left as it is
    //TODO this voices every onset unconditionally, which is unrealistic. Real languages
    //mostly voice in context, e.g. intervocalically (apa -> aba) or after a nasal
    //(anta -> anda), where one syllable meets the next. That wants a word-level rule
    //that looks at the preceding syllable and calls this for the onsets it applies to
    public static Syllable VoiceOnset(Syllable syllable, IEnumerable<Consonant> inventory)
    {
        var onsetResult = new List<Consonant>();
        foreach (var o in syllable.Onset)
        {
            var voiced = o.Voiced ? o : inventory.FirstOrDefault(c => c.Voiced && c.Manner == o.Manner && c.Place == o.Place);
            onsetResult.Add(voiced ?? o);
        }
        var result = new Syllable(new ConsonantCluster(onsetResult), syllable.Nucleus, syllable.Coda);
        return result;
    }
}
