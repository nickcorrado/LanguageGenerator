using System.Collections;

namespace LanguageGenerator;

//syllables can also carry tones, but there's no
//way I'm including that, at least for now
//apparently nuclei can be a syllabic consonant, but we're not doing that
public record Syllable(ConsonantCluster Onset, Vowel Nucleus, Consonant? Coda)
{
    public string Transcription => string.Concat(Onset?.Transcription ?? "", Nucleus.Transcription, Coda?.Transcription ?? "");
}

//is this necessary? it can't be used in SyllableRule, which is annoying
public class ConsonantCluster : IEnumerable<Consonant>
{
    private List<Consonant> Consonants { get; init; }
    //SO says Select() preserves original order
    //https://stackoverflow.com/a/14380453/8429180
    public string Transcription => string.Concat(Consonants.Select(c => c.Transcription));

    public ConsonantCluster(IEnumerable<Consonant> consonants)
    {
        Consonants = consonants.ToList();
    }

    public IEnumerator<Consonant> GetEnumerator() => Consonants.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Consonants.GetEnumerator();
}

public class Category<T> : IEnumerable<T> where T : Phoneme
{
    private List<T> Phonemes { get; init; } = [];
    public override string ToString()
    {
        var result = "[";
        foreach (var p in Phonemes)
        {
            result += p.Transcription;
        }
        result += "]";
        return result;
    }

    public Category(IEnumerable<T> phonemes)
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

//the C, S, L, V and F of templates like CLVF. A slot is only a kind of sound;
//which phonemes can fill it isn't known until it's resolved against a language's
//inventory. Consonantal slots are optional and get filled with the given chance
public record ConsonantSlot(double Chance);
public record SibilantSlot(double Chance);
public record LiquidSlot(double Chance);
public record FinalSlot(double Chance);
//every syllable has exactly one nucleus, so this one is never optional
public record VowelSlot;

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

//slots before the vowel make up the onset; at most one slot may follow it, as the coda
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
//the chances are carried over from when every rule shared one set of positional chances
public static class SyllableTemplates
{
    public static SyllableTemplate CV { get; } = new(new ConsonantSlot(0.5), new VowelSlot());
    public static SyllableTemplate CVF { get; } = new(new ConsonantSlot(0.5), new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate VF { get; } = new(new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate CLV { get; } = new(new ConsonantSlot(0.5), new LiquidSlot(0.3), new VowelSlot());
    public static SyllableTemplate CLVF { get; } = new(new ConsonantSlot(0.5), new LiquidSlot(0.3), new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate SCV { get; } = new(new SibilantSlot(0.5), new ConsonantSlot(0.3), new VowelSlot());
    public static SyllableTemplate SCVF { get; } = new(new SibilantSlot(0.5), new ConsonantSlot(0.3), new VowelSlot(), new FinalSlot(0.5));
    public static SyllableTemplate SCLVF { get; } = new(new SibilantSlot(0.5), new ConsonantSlot(0.3), new LiquidSlot(0.2), new VowelSlot(), new FinalSlot(0.5));

    public static IReadOnlyList<SyllableTemplate> All { get; } = [CV, CVF, VF, CLV, CLVF, SCV, SCVF, SCLVF];
}

//a syllable template resolved against a language's phoneme inventory
public class SyllableRule
{
    public SyllableTemplate Template { get; }

    private readonly Category<Vowel> vowels;
    private readonly Category<Consonant> consonants;
    private readonly Category<Consonant> sibilants;
    private readonly Category<Consonant> liquids;
    private readonly Category<Consonant> finals;

    public SyllableRule(SyllableTemplate template, List<Vowel> vowels, List<Consonant> consonants)
    {
        Template = template;
        //Where() keeps the inventory's order, which matters because picks are biased toward the front
        this.vowels = new(vowels);
        this.consonants = new(consonants);
        sibilants = new(consonants.Where(PhonemeManager.Sibilants.Contains));
        liquids = new(consonants.Where(PhonemeManager.Liquids.Contains));
        finals = new(consonants.Where(PhonemeManager.Finals.Contains));
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

    //which phonemes can fill a consonantal slot, and how often it's filled
    private (Category<Consonant> Phonemes, double Chance) Resolve(Slot slot) => slot switch
    {
        ConsonantSlot c => (consonants, c.Chance),
        SibilantSlot s => (sibilants, s.Chance),
        LiquidSlot l => (liquids, l.Chance),
        FinalSlot f => (finals, f.Chance),
        VowelSlot => throw new ArgumentException("A vowel slot is the nucleus, not a consonant.", nameof(slot)),
    };

    private static T? Pick<T>(Random rand, Category<T> phonemes, double chance, double dropoff) where T : Phoneme
    {
        //pruning can empty a category (say, both sibilants); then the slot just goes unfilled
        if (!phonemes.Any() || rand.NextDouble() >= chance)
        {
            return null;
        }
        return phonemes[Utilities.PowerLaw(rand, phonemes.Count(), dropoff)];
    }

    public override string ToString() =>
        $"{Template.Name}: " + string.Join(" ", Template.Slots.Select(s => s is VowelSlot ? $"V{vowels}" : $"{s.Letter}{Resolve(s).Phonemes}"));
}
