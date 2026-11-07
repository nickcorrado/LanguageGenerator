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

    //schwa and the close vowels are the usual epenthetic vowels, i more than u, as in Japanese;
    //failing those, the language's commonest vowel, which is the first one since picks are biased
    //toward the front
    public static Vowel SelectEpentheticVowel(IReadOnlyList<Vowel> vowels) =>
        new[] { Vowel.ə, Vowel.i, Vowel.u }.FirstOrDefault(vowels.Contains) ?? vowels[0];

    //a set of conventions, applied to the whole inventory. Conventions that clash for this
    //inventory (acute long vowels beside acute close vowels both give ó, say) are drawn again as
    //a set, so a language never ends up with a convention applied to only some of its sounds.
    //IPA is the fallback, which never clashes
    public static Orthography SelectOrthography(Random rand, IReadOnlyList<Vowel> vowels, IReadOnlyList<Consonant> consonants)
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
        return ElideDuplicates(new Syllable([.. onset], nucleus!, coda));
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
        return phonemes[Sampling.TruncatedGeometric(rand, phonemes.Count, dropoff)];
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

        var result = new Syllable([.. onsetResult], syllable.Nucleus, syllable.Coda);
        return result;
    }

    public static SyllableRule SelectSyllableRule(Random rand, IReadOnlyList<Vowel> vowels, IReadOnlyList<Consonant> consonants)
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

    //a vowel system is built, not pruned: how many qualities it has, then which, then whether they
    //come long too. Vowels spread out to fill the space (Liljencrants and Lindblom's dispersion), so
    //each size has its usual shapes, and a three-vowel language has a i u rather than any three
    public static List<Vowel> GenerateVowels(Random rand)
    {
        var qualities = Sampling.Categorical(rand, Sampling.Categorical(rand, VowelSystemBands));

        //length is a property of the system, as in Finnish or Japanese, where it doubles every
        //vowel, so a long vowel never comes without its short one. Mid vowels are the likeliest to
        //miss out, as in Classical Arabic's long ā ī ū. Both odds are guesses: WALS has no chapter
        //on length
        var longs = new List<Vowel>();
        if (rand.NextDouble() < 0.35)
        {
            var dropLongMids = rand.NextDouble() < 0.25;
            longs.AddRange(qualities
                .Where(v => !(dropLongMids && v.Height is VowelHeight.CloseMid))
                .Select(v => v.Lengthened)
                .OfType<Vowel>());
        }

        //the order is a frequency ranking: Pick draws earlier phonemes more often. Unmarked vowels
        //are the commonest in words as they are across languages, so the ranking follows
        //VowelMarkedness, jittered so that languages don't all share it, and long vowels, which
        //are rarer in words than short ones, come after them
        return [.. qualities.Concat(longs)
            .Select(v => (Vowel: v, Rank: Array.IndexOf(VowelMarkedness, v) + (v.Long ? VowelMarkedness.Length : 0) + rand.NextDouble() * 2))
            .OrderBy(x => x.Rank)
            .Select(x => x.Vowel)];
    }

    //short vowels from commonest to rarest across languages, roughly as PHOIBLE counts them
    private static readonly Vowel[] VowelMarkedness = [Vowel.a, Vowel.i, Vowel.u, Vowel.e, Vowel.o, Vowel.ə, Vowel.ɛ, Vowel.ɔ, Vowel.æ];

    //WALS 2A counts languages by how many vowel qualities they have: 93 with 2 to 4, 287 with 5 or
    //6, and 184 with 7 to 14. Within each band, the systems and their weights are guesses at how
    //common each shape is, five vowels being much the commonest size. Seven is as many as this
    //inventory allows besides schwa and æ, so the large band tops out at nine
    private static readonly (Vowel[] Qualities, int Weight)[] SmallVowelSystems =
    [
        ([Vowel.a, Vowel.ə], 3),                                 //vertical, as in Kabardian
        ([Vowel.a, Vowel.i, Vowel.u], 50),                       //Arabic, Quechua
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.e], 15),
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.o], 15),
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.ə], 17),
    ];

    private static readonly (Vowel[] Qualities, int Weight)[] AverageVowelSystems =
    [
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u], 70),     //Spanish, Japanese, Swahili
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ə], 20),
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ɛ], 5),
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.æ], 5),
    ];

    private static readonly (Vowel[] Qualities, int Weight)[] LargeVowelSystems =
    [
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u], 50),                   //Italian, Yoruba
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ə, Vowel.æ], 15),
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u, Vowel.ə], 25),          //Catalan
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u, Vowel.ə, Vowel.æ], 10),
    ];

    private static readonly ((Vowel[] Qualities, int Weight)[] Systems, int Weight)[] VowelSystemBands =
        [(SmallVowelSystems, 93), (AverageVowelSystems, 287), (LargeVowelSystems, 184)];

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
