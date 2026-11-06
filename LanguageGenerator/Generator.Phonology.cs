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

    //more sound changes, applied after the first ones, in this order. What a language does when vowels
    //meet, elision, contraction or nothing, comes last, so that the vowels it removes still separate
    //consonants for the changes before it, and still set a word's harmony. The odds are guesses, final
    //devoicing is for languages whose syllables can end in a consonant, and harmony is for those with
    //vowels to pair. Harmony is drawn last, so that the changes before it stay as they were for each seed
    public static List<SoundChange> SelectLaterSoundChanges(Random rand, SyllableRule rule, IReadOnlyCollection<Vowel> vowels)
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
        if (rand.NextDouble() < 0.3 && rule.Template.Slots.Any(s => s is Slot.Final))
        {
            changes.Add(SoundChange.FinalDevoicing);
        }
        var hiatus = rand.NextDouble() switch
        {
            < 0.4 => SoundChange.VowelElision,
            < 0.75 => SoundChange.VowelContraction,
            _ => null,
        };
        var harmony = rand.NextDouble() switch
        {
            < 0.15 => SoundChange.FrontBackHarmony,
            < 0.23 => SoundChange.TongueRootHarmony,
            _ => null,
        };
        if (harmony is not null && harmony.CanApplyTo(vowels))
        {
            changes.Add(harmony);
        }
        if (hiatus is not null)
        {
            changes.Add(hiatus);
        }
        return changes;
    }

    //changes that depend on syllables, for languages whose syllables can end in a consonant. Devoicing
    //and weakening need obstruents in codas, which a coda restriction rules out, and lengthening needs
    //long vowels. It's drawn last, so that the others stay as they were. The odds are guesses
    public static List<SoundChange> SelectSyllableChanges(Random rand, SyllableRule rule, IReadOnlyCollection<Vowel> vowels)
    {
        var changes = new List<SoundChange>();
        var codas = rule.Template.Slots.Any(s => s is Slot.Final);
        if (rand.NextDouble() < 0.2 && codas && rule.Codas == CodaRestriction.Any)
        {
            changes.Add(SoundChange.CodaDevoicing);
        }
        if (rand.NextDouble() < 0.15 && codas && rule.Codas == CodaRestriction.Any)
        {
            changes.Add(SoundChange.CodaWeakening);
        }
        var laxes = rand.NextDouble() < 0.15 && codas;
        if (rand.NextDouble() < 0.12 && codas && vowels.Any(v => v.Long))
        {
            changes.Add(SoundChange.CompensatoryLengthening);
        }
        if (laxes)
        {
            changes.Add(SoundChange.ClosedSyllableLaxing);
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
    //IPA is the fallback, which never clashes
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
            if (Orthography.AreDistinct(spellings.Values))
            {
                return new Orthography(spellings, style);
            }
        }
        return new Orthography(inventory.ToDictionary(p => p, p => p.IpaSymbol));
    }

    public static Syllable GenerateSyllable(Random rand, SyllableRule rule, double dropoff)
    {
        var onset = new List<Consonant>();
        Vowel? nucleus = null;
        Consonant? coda = null;

        foreach (var slot in rule.Template.Slots)
        {
            if (slot is Slot.Vowel)
            {
                nucleus = Pick(rand, rule.Vowels, 1, dropoff) ?? throw new InvalidOperationException("The inventory has no vowels.");
            }
            else if (Pick(rand, rule.Resolve(slot), FillChance(rule.Template, slot), dropoff) is { } consonant)
            {
                if (nucleus is null)
                {
                    onset.Add(consonant);
                }
                else
                {
                    coda = consonant;
                }
            }
        }

        //the template guarantees exactly one vowel slot
        return ElideDuplicates(new Syllable(new ConsonantCluster(onset), nucleus!, coda));
    }

    //how often a consonantal slot is filled. Every language has syllables with onsets, and most
    //syllables have one: onsets are filled 85% of the time, or about that in templates with two
    //consonants that can start one. A guess, but onsetless syllables are dispreferred everywhere, and
    //at even odds, a quarter of all syllables were a bare vowel, which made homophones of most of the
    //short words
    private static double FillChance(SyllableTemplate template, Slot slot)
    {
        var sibilantOnset = template.Slots.Contains(Slot.Sibilant);
        return slot switch
        {
            Slot.Consonant => sibilantOnset ? 0.7 : 0.85,
            Slot.Sibilant => 0.5,
            Slot.Liquid => sibilantOnset ? 0.2 : 0.3,
            Slot.Final => 0.5,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
    }

    private static T? Pick<T>(Random rand, NaturalClass<T> phonemes, double chance, double dropoff) where T : Phoneme
    {
        //pruning can empty a category (say, both sibilants); then the slot just goes unfilled
        if (!phonemes.Any() || rand.NextDouble() >= chance)
        {
            return null;
        }
        return phonemes[Sampling.TruncatedGeometric(rand, phonemes.Count(), dropoff)];
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
        var codas = template.Slots.Any(s => s is Slot.Final) ? Sampling.Categorical(rand, CodaRestrictionWeights) : CodaRestriction.Any;
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
