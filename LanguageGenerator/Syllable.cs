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

/// <summary>
/// One of the C, S, L, V and F of a template like CLVF: a kind of sound, whose phonemes aren't
/// known until it's resolved against a language's inventory.
/// </summary>
//consonantal slots are optional; every syllable has exactly one nucleus, so the vowel never is
public enum Slot
{
    Consonant,
    Sibilant,
    Liquid,
    Vowel,
    Final,
}

public static class SlotExtensions
{
    extension(Slot slot)
    {
        public char Letter => slot switch
        {
            Slot.Consonant => 'C',
            Slot.Sibilant => 'S',
            Slot.Liquid => 'L',
            Slot.Vowel => 'V',
            Slot.Final => 'F',
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
    }
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

        var nuclei = Slots.Count(s => s is Slot.Vowel);
        if (nuclei != 1)
        {
            throw new ArgumentException($"{Name} has {nuclei} vowel slots, but a syllable needs exactly one.", nameof(slots));
        }

        var codaSlots = Slots.Count - Slots.ToList().FindIndex(s => s is Slot.Vowel) - 1;
        if (codaSlots > 1)
        {
            throw new ArgumentException($"{Name} has {codaSlots} slots after the vowel, but a syllable only has room for one coda.", nameof(slots));
        }
    }

    public override string ToString() => Name;
}

//templates don't depend on the inventory, so unlike rules they can be defined up front
public static class SyllableTemplates
{
    public static SyllableTemplate CV { get; } = new(Slot.Consonant, Slot.Vowel);
    public static SyllableTemplate CVF { get; } = new(Slot.Consonant, Slot.Vowel, Slot.Final);
    public static SyllableTemplate CLV { get; } = new(Slot.Consonant, Slot.Liquid, Slot.Vowel);
    public static SyllableTemplate CLVF { get; } = new(Slot.Consonant, Slot.Liquid, Slot.Vowel, Slot.Final);
    public static SyllableTemplate SCV { get; } = new(Slot.Sibilant, Slot.Consonant, Slot.Vowel);
    public static SyllableTemplate SCVF { get; } = new(Slot.Sibilant, Slot.Consonant, Slot.Vowel, Slot.Final);
    public static SyllableTemplate SCLVF { get; } = new(Slot.Sibilant, Slot.Consonant, Slot.Liquid, Slot.Vowel, Slot.Final);

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
    public NaturalClass<Vowel> Vowels { get; }

    private readonly NaturalClass<Consonant> consonants;
    private readonly NaturalClass<Consonant> sibilants;
    private readonly NaturalClass<Consonant> liquids;
    private readonly NaturalClass<Consonant> finals;

    public SyllableRule(SyllableTemplate template, List<Vowel> vowels, List<Consonant> consonants, CodaRestriction codas = CodaRestriction.Any)
    {
        Template = template;
        Codas = codas;
        //Where() keeps the inventory's order, which matters because picks are biased toward the front
        Vowels = new(vowels);
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

    //whether these consonants can open, or close, a syllable of this shape
    public bool IsLegalOnset(IReadOnlyList<Consonant> consonants) =>
        Fits(consonants, [.. Template.Slots.TakeWhile(s => s is not Slot.Vowel)]);
    public bool IsLegalCoda(IReadOnlyList<Consonant> consonants) =>
        Fits(consonants, [.. Template.Slots.SkipWhile(s => s is not Slot.Vowel).Skip(1)]);

    //every slot is optional, so the consonants only have to fill some of the slots, in order
    private bool Fits(IReadOnlyList<Consonant> consonants, IReadOnlyList<Slot> slots)
    {
        if (consonants.Count == 0)
        {
            return true;
        }
        for (int i = 0; i < slots.Count; i++)
        {
            if (Resolve(slots[i]).Contains(consonants[0]) && Fits([.. consonants.Skip(1)], [.. slots.Skip(i + 1)]))
            {
                return true;
            }
        }
        return false;
    }

    //which phonemes can fill a consonantal slot
    public NaturalClass<Consonant> Resolve(Slot slot) => slot switch
    {
        Slot.Consonant => consonants,
        Slot.Sibilant => sibilants,
        Slot.Liquid => liquids,
        Slot.Final => finals,
        Slot.Vowel => throw new ArgumentException("A vowel slot is the nucleus, not a consonant.", nameof(slot)),
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    public override string ToString() =>
        $"{Template.Name}: " + string.Join(" ", Template.Slots.Select(s => s is Slot.Vowel ? $"V{Vowels}" : $"{s.Letter}{Resolve(s)}"))
        + (Codas != CodaRestriction.Any && Template.Slots.Any(s => s is Slot.Final) ? $" ({Codas} codas)" : "");
}
