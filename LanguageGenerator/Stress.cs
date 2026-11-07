namespace LanguageGenerator;

/// <summary>
/// Which syllable of a word is stressed, counted over the whole word, affixes and all, so stress can
/// move as a word is inflected.
/// </summary>
//a stress accent, made by loudness and length, as in Spanish or Russian, not a pitch accent like
//Japanese's, which is out of scope with tone. Stress here is always predictable from the word's
//syllables, either at a fixed place or drawn to heavy syllables, ones with a long vowel or a coda;
//lexical stress, which each word would have to store, as Russian's does, is left out
public enum Stress
{
    Initial,
    Second,
    Third,
    Antepenultimate,
    Penultimate,
    Ultimate,
    //the last syllable if it's heavy, and the penultimate otherwise, as in many Arabic dialects
    UltimateIfHeavy,
    //the penultimate if it's heavy, and the antepenultimate otherwise, as in Latin
    PenultimateIfHeavy,
    //the first syllable if it's heavy, and the second otherwise
    InitialIfHeavy,
    //the last heavy syllable, and the first if there's none, as in Eastern Mari
    LastHeavy,
}

public static class StressExtensions
{
    extension(Stress stress)
    {
        /// <summary>The index of the stressed syllable among these.</summary>
        //a word too short for the place falls back on the nearest syllable it has
        public int StressedSyllable(IReadOnlyList<Syllable> syllables)
        {
            var last = syllables.Count - 1;
            int FromEnd(int back) => Math.Max(0, last - back);
            int FromStart(int index) => Math.Min(last, index);
            bool Heavy(int index) => index >= 0 && index <= last && (syllables[index].Coda is not null || syllables[index].Nucleus.Long);

            return stress switch
            {
                Stress.Initial => 0,
                Stress.Second => FromStart(1),
                Stress.Third => FromStart(2),
                Stress.Antepenultimate => FromEnd(2),
                Stress.Penultimate => FromEnd(1),
                Stress.Ultimate => last,
                Stress.UltimateIfHeavy => Heavy(last) ? last : FromEnd(1),
                Stress.PenultimateIfHeavy => Heavy(last - 1) ? FromEnd(1) : FromEnd(2),
                Stress.InitialIfHeavy => Heavy(0) ? 0 : FromStart(1),
                Stress.LastHeavy => Enumerable.Range(0, syllables.Count).LastOrDefault(Heavy, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(stress), stress, null),
            };
        }

        public bool IsWeightSensitive => stress is Stress.UltimateIfHeavy or Stress.PenultimateIfHeavy or Stress.InitialIfHeavy or Stress.LastHeavy;

        public string Description => stress switch
        {
            Stress.Initial => "on the first syllable",
            Stress.Second => "on the second syllable",
            Stress.Third => "on the third syllable",
            Stress.Antepenultimate => "on the third-to-last syllable",
            Stress.Penultimate => "on the second-to-last syllable",
            Stress.Ultimate => "on the last syllable",
            Stress.UltimateIfHeavy => "on the last syllable if it's heavy, otherwise the second-to-last",
            Stress.PenultimateIfHeavy => "on the second-to-last syllable if it's heavy, otherwise the third-to-last",
            Stress.InitialIfHeavy => "on the first syllable if it's heavy, otherwise the second",
            Stress.LastHeavy => "on the last heavy syllable, otherwise the first",
            _ => throw new ArgumentOutOfRangeException(nameof(stress), stress, null),
        };
    }
}
