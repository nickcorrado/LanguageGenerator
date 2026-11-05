using System.Runtime.InteropServices;

namespace LanguageGenerator;

//inventories, syllables, sandhi and spelling
public static partial class Generator
{
    //each rule is independent of the others, and they apply in this order
    public static List<SoundChange> SelectSoundChanges(Random rand)
    {
        var rules = new List<SoundChange>();
        if (rand.NextDouble() < 0.5)
        {
            rules.Add(SoundChange.NasalAssimilation);
        }
        if (rand.NextDouble() < 0.5)
        {
            rules.Add(SoundChange.Degemination);
        }
        if (rand.NextDouble() < 0.3)
        {
            rules.Add(SoundChange.IntervocalicVoicing);
        }
        return rules;
    }

    //more sound changes, applied after the first ones, in this order. Elision comes last so that the
    //vowels it removes still separate consonants for the changes before it. The odds are guesses, and
    //final devoicing is for languages whose syllables can end in a consonant
    public static List<SoundChange> SelectLaterSoundChanges(Random rand, SyllableRule rule)
    {
        var changes = new List<SoundChange>();
        if (rand.NextDouble() < 0.25)
        {
            changes.Add(SoundChange.Lenition);
        }
        if (rand.NextDouble() < 0.3)
        {
            changes.Add(SoundChange.VoicingAssimilation);
        }
        if (rand.NextDouble() < 0.3 && rule.Template.Slots.Any(s => s is FinalSlot))
        {
            changes.Add(SoundChange.FinalDevoicing);
        }
        if (rand.NextDouble() < 0.4)
        {
            changes.Add(SoundChange.VowelElision);
        }
        return changes;
    }

    //schwa and i are the usual epenthetic vowels; failing those, the language's commonest vowel,
    //which is the first one since picks are biased toward the front
    public static Vowel SelectEpentheticVowel(List<Vowel> vowels) =>
        vowels.Contains(Vowel.ə) ? Vowel.ə : vowels.Contains(Vowel.i) ? Vowel.i : vowels[0];

    //a set of conventions, applied to the whole inventory. Conventions that clash for this
    //inventory (acute long vowels beside acute close vowels both give ó, say) are drawn again as
    //a set, so a language never ends up with a convention applied to only some of its sounds.
    //IPA is the fallback, and always unambiguous
    public static Orthography SelectOrthography(Random rand, List<Vowel> vowels, List<Consonant> consonants)
    {
        var inventory = vowels.Concat<Phoneme>(consonants).ToList();
        T Pick<T>(IReadOnlyList<T> options) => options[rand.Next(options.Count)];
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var style = new OrthographicStyle(
                Pick(Enum.GetValues<PostalveolarSpelling>()),
                Pick(Enum.GetValues<LongVowelSpelling>()),
                Pick(Enum.GetValues<MidVowelSpelling>()),
                Pick(OrthographicStyle.SchwaOptions),
                Pick(OrthographicStyle.NearOpenFrontOptions),
                Pick(OrthographicStyle.VelarStopOptions),
                Pick(OrthographicStyle.PalatalGlideOptions),
                Pick(Enum.GetValues<DentalFricativeSpelling>()),
                Pick(OrthographicStyle.VelarNasalOptions),
                Pick(OrthographicStyle.VelarFricativeOptions));
            var spellings = style.Spell(inventory);
            if (Orthography.IsUnambiguous(spellings.Values))
            {
                return new Orthography(spellings, style);
            }
        }
        return new Orthography(inventory.ToDictionary(p => p, p => p.IpaSymbol));
    }

    public static Syllable GenerateSyllable(Random rand, SyllableRule rule, double dropoff)
    {
        var syllable = rule.GenerateSyllable(rand, dropoff);

        syllable = ElideDuplicates(syllable);
        return syllable;
    }

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

    public static SyllableRule SelectSyllableRule(Random rand, List<Vowel> vowels, List<Consonant> consonants)
    {
        var template = SyllableTemplates.All[rand.Next(SyllableTemplates.All.Count)];
        //a restriction only matters for templates with a coda
        var codas = template.Slots.Any(s => s is FinalSlot) ? Sampling.Categorical(rand, CodaRestrictionWeights) : CodaRestriction.Any;
        return new SyllableRule(template, vowels, consonants, codas);
    }

    //weights, and a guess: WALS's chapter on syllable structure counts how many consonants may close
    //a syllable but not which ones, and no survey of that turned up
    private static readonly (CodaRestriction Value, int Weight)[] CodaRestrictionWeights =
    [
        (CodaRestriction.Any, 50),
        (CodaRestriction.Sonorants, 25),
        (CodaRestriction.Nasals, 15),
        (CodaRestriction.SingleNasal, 10),
    ];

    public static List<Vowel> GenerateVowels(Random rand, double chanceOfPhonemePruning, double chanceOfCategoryPruning)
    {
        var vowels = new List<Vowel>(Vowel.All);

        for (int i = vowels.Count - 1; i >= 0; i--)
        {
            Vowel vowel = vowels[i];
            if (rand.NextDouble() < chanceOfPhonemePruning)
            {
                vowels.Remove(vowel);
            }
        }

        //chance of removing longs
        //TODO make this a little more sophisticated? meh
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            vowels.RemoveAll(v => v.Long);
        }

        //the order is a frequency ranking: Pick draws earlier phonemes more often
        rand.Shuffle(CollectionsMarshal.AsSpan(vowels));

        return vowels;
    }

    public static List<Consonant> GenerateConsonants(Random rand, double chanceofPhonemePruning, double chanceOfCategoryPruning)
    {
        var consonants = new List<Consonant>(Consonant.All);

        for (int i = consonants.Count - 1; i >= 0; i--)
        {
            Consonant consonant = consonants[i];
            if (rand.NextDouble() < chanceofPhonemePruning)
            {
                consonants.Remove(consonant);
            }
        }

        //picked some more disposable categories; this could use work
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            consonants.RemoveAll(c => c.Place == ConsonantPlace.Labial);
        }
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            consonants.RemoveAll(c => c.Place == ConsonantPlace.Palatal);
        }
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            consonants.RemoveAll(c => c.Place == ConsonantPlace.Retroflex);
        }

        rand.Shuffle(CollectionsMarshal.AsSpan(consonants));

        return consonants;
    }
}
