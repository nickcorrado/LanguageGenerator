using System.Collections;

namespace LanguageGenerator;

public record Syllable(ConsonantCluster Onset, Vowel Nucleus, Consonant? Coda)
{
    public string Spell(Orthography orthography) => orthography.Spell(Phonemes);

    public IEnumerable<Phoneme> Phonemes
    {
        get
        {
            foreach (var consonant in Onset)
            {
                yield return consonant;
            }
            yield return Nucleus;
            if (Coda is not null)
            {
                yield return Coda;
            }
        }
    }
}

//is this necessary? it can't be used in SyllableRule, which is annoying
public class ConsonantCluster : IEnumerable<Consonant>
{
    private List<Consonant> Consonants { get; init; }

    public ConsonantCluster(IEnumerable<Consonant> consonants)
    {
        Consonants = consonants.ToList();
    }

    public IEnumerator<Consonant> GetEnumerator() => Consonants.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Consonants.GetEnumerator();
}

public class NaturalClass<T> : IEnumerable<T> where T : Phoneme
{
    private List<T> Phonemes { get; init; } = [];
    public override string ToString()
    {
        var result = "[";
        foreach (var p in Phonemes)
        {
            result += p.IpaSymbol;
        }
        result += "]";
        return result;
    }

    public NaturalClass(IEnumerable<T> phonemes)
    {
        Phonemes = phonemes.ToList();
    }

    public T this[int index]
    {
        get => Phonemes[index];
        set => Phonemes[index] = value;
    }

    public IEnumerator<T> GetEnumerator() => Phonemes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Phonemes.GetEnumerator();
}

//consonantal slots are optional and get filled with the given chance
public record ConsonantSlot(double Chance);
public record SibilantSlot(double Chance);
public record LiquidSlot(double Chance);
public record FinalSlot(double Chance);
//every syllable has exactly one nucleus, so this one is never optional
public record VowelSlot;

/// <summary>
/// One of the C, S, L, V and F of a template like CLVF: a kind of sound, whose phonemes aren't
/// known until it's resolved against a language's inventory.
/// </summary>
public union Slot(ConsonantSlot, SibilantSlot, LiquidSlot, VowelSlot, FinalSlot)
{
    public char Letter => this switch
    {
        ConsonantSlot => 'C',
        SibilantSlot => 'S',
        LiquidSlot => 'L',
        VowelSlot => 'V',
        FinalSlot => 'F',
    };
}

/// <summary>
/// A syllable's shape, like CLVF. Slots before the vowel make up the onset, and at most one slot
/// may follow it, as the coda.
/// </summary>
public class SyllableTemplate
{
    public IReadOnlyList<Slot> Slots { get; }
    public string Name => string.Concat(Slots.Select(s => s.Letter));

    public SyllableTemplate(params Slot[] slots)
    {
        Slots = slots;

        var nuclei = Slots.Count(s => s is VowelSlot);
        if (nuclei != 1)
        {
            throw new ArgumentException($"{Name} has {nuclei} vowel slots, but a syllable needs exactly one.", nameof(slots));
        }

        var codaSlots = Slots.Count - Slots.ToList().FindIndex(s => s is VowelSlot) - 1;
        if (codaSlots > 1)
        {
            throw new ArgumentException($"{Name} has {codaSlots} slots after the vowel, but a syllable only has room for one coda.", nameof(slots));
        }
    }

    public override string ToString() => Name;
}

//templates don't depend on the inventory, so unlike rules they can be defined up front.
//Every language has syllables with onsets, and most syllables have one: onsets are filled 85% of
//the time, or about that in templates with two consonants that can start one. A guess, but
//onsetless syllables are dispreferred everywhere, and at the even odds these once had, a quarter
//of all syllables were a bare vowel, which made homophones of most of the short words
public static class SyllableTemplates
{
    public static SyllableTemplate CV { get; } = new(new ConsonantSlot(0.85), new VowelSlot());
    public static SyllableTemplate CVF { get; } = new(new ConsonantSlot(0.85), new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate CLV { get; } = new(new ConsonantSlot(0.85), new LiquidSlot(0.3), new VowelSlot());
    public static SyllableTemplate CLVF { get; } = new(new ConsonantSlot(0.85), new LiquidSlot(0.3), new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate SCV { get; } = new(new SibilantSlot(0.5), new ConsonantSlot(0.7), new VowelSlot());
    public static SyllableTemplate SCVF { get; } = new(new SibilantSlot(0.5), new ConsonantSlot(0.7), new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate SCLVF { get; } = new(new SibilantSlot(0.5), new ConsonantSlot(0.7), new LiquidSlot(0.2), new VowelSlot(), new FinalSlot(0.5));

    public static IReadOnlyList<SyllableTemplate> All { get; } = [CV, CVF, CLV, CLVF, SCV, SCVF, SCLVF];
}

/// <summary>Which consonants may close a syllable.</summary>
//many languages allow only some: Mandarin allows only nasals, and Japanese only its moraic n
public enum CodaRestriction
{
    Any,
    //nasals, liquids and glides
    Sonorants,
    Nasals,
    //a single nasal: n, if the language has it
    SingleNasal,
}

/// <summary>A syllable template resolved against a language's phoneme inventory.</summary>
public class SyllableRule
{
    public SyllableTemplate Template { get; }
    public CodaRestriction Codas { get; }

    private readonly NaturalClass<Vowel> vowels;
    private readonly NaturalClass<Consonant> consonants;
    private readonly NaturalClass<Consonant> sibilants;
    private readonly NaturalClass<Consonant> liquids;
    private readonly NaturalClass<Consonant> finals;

    public SyllableRule(SyllableTemplate template, List<Vowel> vowels, List<Consonant> consonants, CodaRestriction codas = CodaRestriction.Any)
    {
        Template = template;
        Codas = codas;
        //Where() keeps the inventory's order, which matters because picks are biased toward the front
        this.vowels = new(vowels);
        this.consonants = new(consonants);
        //natural classes, by feature: S is a voiceless sibilant, as in s-clusters like st or šp, and
        //L is any approximant, liquids and glides alike, as in pl, kw or by
        sibilants = new(consonants.Where(c => c.Manner == ConsonantManner.Sibilant && !c.Voiced));
        liquids = new(consonants.Where(c => c.Manner is ConsonantManner.Approximant or ConsonantManner.LateralApproximant));
        //generation, legality and cluster repair all read this one class, so restricting it
        //restricts codas everywhere. A restriction can leave nothing (a nasals-only language that
        //lost both nasals to pruning), and then syllables simply have no codas
        finals = new(codas switch
        {
            CodaRestriction.Any => consonants,
            CodaRestriction.Sonorants => consonants.Where(c => c.Manner is ConsonantManner.Nasal or ConsonantManner.Approximant or ConsonantManner.LateralApproximant or ConsonantManner.Flap or ConsonantManner.Trill),
            CodaRestriction.Nasals => consonants.Where(c => c.Manner == ConsonantManner.Nasal),
            CodaRestriction.SingleNasal => consonants.Contains(Consonant.n) ? new[] { Consonant.n } : consonants.Where(c => c.Manner == ConsonantManner.Nasal).Take(1),
            _ => throw new ArgumentOutOfRangeException(nameof(codas), codas, null),
        });
    }

    public Syllable GenerateSyllable(Random rand, double dropoff)
    {
        var onset = new List<Consonant>();
        Vowel? nucleus = null;
        Consonant? coda = null;

        foreach (var slot in Template.Slots)
        {
            if (slot is VowelSlot)
            {
                nucleus = Pick(rand, vowels, 1, dropoff) ?? throw new InvalidOperationException("The inventory has no vowels.");
            }
            else if (Resolve(slot) is var (phonemes, chance) && Pick(rand, phonemes, chance, dropoff) is { } consonant)
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
        return new Syllable(new ConsonantCluster(onset), nucleus!, coda);
    }

    /// <summary>Roughly how many distinct syllables this rule can make.</summary>
    //each consonantal slot is empty or one of its phonemes. It overcounts a little, since in CLV an
    //onset l could come from C or from L
    public double DistinctSyllables => Template.Slots
        .Select(s => s is VowelSlot ? vowels.Count() : 1 + Resolve(s).Phonemes.Count())
        .Aggregate(1.0, (product, choices) => product * choices);

    //whether these consonants can open, or close, a syllable of this shape
    public bool IsLegalOnset(IReadOnlyList<Consonant> consonants) =>
        Fits(consonants, [.. Template.Slots.TakeWhile(s => s is not VowelSlot)]);
    public bool IsLegalCoda(IReadOnlyList<Consonant> consonants) =>
        Fits(consonants, [.. Template.Slots.SkipWhile(s => s is not VowelSlot).Skip(1)]);

    //every slot is optional, so the consonants only have to fill some of the slots, in order
    private bool Fits(IReadOnlyList<Consonant> consonants, IReadOnlyList<Slot> slots)
    {
        if (consonants.Count == 0)
        {
            return true;
        }
        for (int i = 0; i < slots.Count; i++)
        {
            if (Resolve(slots[i]).Phonemes.Contains(consonants[0]) && Fits([.. consonants.Skip(1)], [.. slots.Skip(i + 1)]))
            {
                return true;
            }
        }
        return false;
    }

    //which phonemes can fill a consonantal slot, and how often it's filled
    private (NaturalClass<Consonant> Phonemes, double Chance) Resolve(Slot slot) => slot switch
    {
        ConsonantSlot c => (consonants, c.Chance),
        SibilantSlot s => (sibilants, s.Chance),
        LiquidSlot l => (liquids, l.Chance),
        FinalSlot f => (finals, f.Chance),
        VowelSlot => throw new ArgumentException("A vowel slot is the nucleus, not a consonant.", nameof(slot)),
    };

    private static T? Pick<T>(Random rand, NaturalClass<T> phonemes, double chance, double dropoff) where T : Phoneme
    {
        //pruning can empty a category (say, both sibilants); then the slot just goes unfilled
        if (!phonemes.Any() || rand.NextDouble() >= chance)
        {
            return null;
        }
        return phonemes[Sampling.TruncatedGeometric(rand, phonemes.Count(), dropoff)];
    }

    public override string ToString() =>
        $"{Template.Name}: " + string.Join(" ", Template.Slots.Select(s => s is VowelSlot ? $"V{vowels}" : $"{s.Letter}{Resolve(s).Phonemes}"))
        + (Codas != CodaRestriction.Any && Template.Slots.Any(s => s is FinalSlot) ? $" ({Codas} codas)" : "");
}
