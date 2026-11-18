using System.Collections;

namespace LanguageGenerator;

public record Syllable(ValueList<Consonant> Onset, Vowel Nucleus, Consonant? Coda)
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

public class NaturalClass<T> : IReadOnlyList<T> where T : Phoneme
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

    public T this[int index] => Phonemes[index];

    public int Count => Phonemes.Count;

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

/// <summary>What a syllable can be, before the language's sounds fill it in.</summary>
public record SyllableShape(SyllableTemplate Template, CodaRestriction Codas);

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

    public SyllableRule(SyllableTemplate template, IReadOnlyList<Vowel> vowels, IReadOnlyList<Consonant> consonants, CodaRestriction codas = CodaRestriction.Any)
    {
        Template = template;
        Codas = codas;
        //Where() keeps the inventory's order, which matters because picks are biased toward the front
        Vowels = new(vowels);
        this.consonants = new(consonants);
        //natural classes, by feature: S is a voiceless sibilant, as in s-clusters like st or šp, and
        //L is any liquid or glide, as in pl, pr, kw or by
        sibilants = new(consonants.Where(c => c.Manner == ConsonantManner.Sibilant && !c.Voiced));
        liquids = new(consonants.Where(c => c.Manner is ConsonantManner.Approximant or ConsonantManner.LateralApproximant or ConsonantManner.Trill or ConsonantManner.Flap));
        //generation, legality and cluster repair all read this one class, so restricting it
        //restricts codas everywhere. A restriction can leave nothing (a nasals-only language that
        //lost both nasals to pruning), and then syllables simply have no codas. Aspiration and
        //labialization are lost at the end of a syllable, as in Thai or Korean, where a final
        //stop is never aspirated, so neither ever closes one
        var unreleased = consonants.Where(c => !c.Aspirated && !c.Labialized);
        finals = new(codas switch
        {
            CodaRestriction.Any => unreleased,
            CodaRestriction.Sonorants => consonants.Where(c => c.Manner is ConsonantManner.Nasal or ConsonantManner.Approximant or ConsonantManner.LateralApproximant or ConsonantManner.Flap or ConsonantManner.Trill),
            CodaRestriction.Nasals => consonants.Where(c => c.Manner == ConsonantManner.Nasal),
            CodaRestriction.SingleNasal => consonants.Contains(Consonant.n) ? new[] { Consonant.n } : consonants.Where(c => c.Manner == ConsonantManner.Nasal).Take(1),
            _ => throw new ArgumentOutOfRangeException(nameof(codas), codas, null),
        });
    }

    //whether these consonants can open, or close, a syllable of this shape
    public bool IsLegalOnset(IReadOnlyList<Consonant> consonants) =>
        Fits(consonants, [.. Template.Slots.TakeWhile(s => s is not Slot.Vowel)])
        && consonants.Zip(consonants.Skip(1)).All(pair => CanPrecede(pair.First, pair.Second));

    /// <summary>Whether one consonant can come right before another in an onset.</summary>
    //sonority sequencing: sounds grow more vowel-like toward the vowel, stops, then fricatives,
    //nasals, liquids and glides, and before a liquid, by at least two steps, the commonest minimum
    //distance, or before a glide by one. So pl, fr, mj, lj and kw, but not nr or nl. On top of that,
    //the commonest gaps:
    //labials before w and coronal stops before l, which English, Spanish and Greek all lack, and
    //no glottal stop or labialized consonant leading a cluster. Before anything else, only a
    //sibilant can stand, as in st, sk or sn, and only before a plain voiceless stop or m or n:
    //English has no sth or sŋ, and its sp, st and sk are never aspirated
    public static bool CanPrecede(Consonant first, Consonant second)
    {
        if (first == Consonant.ʔ || first.Labialized)
        {
            return false;
        }
        if (second.Manner is ConsonantManner.Approximant or ConsonantManner.LateralApproximant or ConsonantManner.Trill or ConsonantManner.Flap)
        {
            //glides are freer than liquids: a liquid can come before one, as in Spanish lia or
            //French loi, so glide formation has somewhere to go
            var distance = Sonority(second) == 5 ? 1 : 2;
            return Sonority(second) - Sonority(first) >= distance
                && !(second == Consonant.w && first.Place is ConsonantPlace.Labial or ConsonantPlace.LabioDental)
                && !(second == Consonant.l && first.Manner is ConsonantManner.Plosive && first.Place is ConsonantPlace.Dental);
        }
        return first.Manner == ConsonantManner.Sibilant && !first.Voiced
            && (second is { Manner: ConsonantManner.Plosive, Voiced: false, Aspirated: false } && second != Consonant.ʔ
                || second == Consonant.m || second == Consonant.n);
    }

    /// <summary>How vowel-like a consonant is, from stops at 1 to glides at 5.</summary>
    public static int Sonority(Consonant consonant) => consonant.Manner switch
    {
        ConsonantManner.Plosive or ConsonantManner.Affricate => 1,
        ConsonantManner.Fricative or ConsonantManner.Sibilant => 2,
        ConsonantManner.Nasal => 3,
        ConsonantManner.LateralApproximant or ConsonantManner.Trill or ConsonantManner.Flap => 4,
        ConsonantManner.Approximant => consonant == Consonant.ɹ ? 4 : 5,
        _ => throw new ArgumentOutOfRangeException(nameof(consonant), consonant.Manner, null),
    };
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
