using System.Collections;

namespace LanguageGenerator;

//syllables can also carry tones, but there's no
//way I'm including that, at least for now
//apparently nuclei can be a syllabic consonant, but we're not doing that
public record Syllable(ConsonantCluster Onset, Vowel Nucleus, Consonant Coda)
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

public class SyllableRule
{
    //nullable
    public SyllableCategory<Consonant> Onset1 { get; set; }
    //nullable
    public SyllableCategory<Consonant> Onset2 { get; set; }
    //nullable
    public SyllableCategory<Consonant> Onset3 { get; set; }
    public SyllableCategory<Vowel> Nucleus { get; set; }
    public SyllableCategory<Consonant> Coda { get; set; }

    public EnumeratedSyllableRule EnumeratedSyllableRule { get; set; }

    public SyllableRule(List<Vowel> vowels, List<Consonant> consonants, EnumeratedSyllableRule rule)
    {
        //this code is complete and utter garbage
        //at the very least, we might as well pass sibilants and liquids, just so this is all more readable
        //even if it duplicates consonants
        Onset1 = rule switch
        {
            EnumeratedSyllableRule.CV or EnumeratedSyllableRule.CVF or EnumeratedSyllableRule.CLV or EnumeratedSyllableRule.CLVF => new SyllableCategory<Consonant>(new Category<Consonant>(consonants)),
            EnumeratedSyllableRule.SCV or EnumeratedSyllableRule.SCVF or EnumeratedSyllableRule.SCLVF => new SyllableCategory<Consonant>(new Category<Consonant>(consonants.Where(c => PhonemeManager.Sibilants.Any(s => s == c)).ToList())),
            _ => null
        };
        Onset2 = rule switch
        {
            EnumeratedSyllableRule.CLV or EnumeratedSyllableRule.CLVF => new SyllableCategory<Consonant>(new Category<Consonant>(consonants.Where(c => PhonemeManager.Liquids.Any(l => l == c)).ToList())),
            EnumeratedSyllableRule.SCV or EnumeratedSyllableRule.SCVF or EnumeratedSyllableRule.SCLVF => new SyllableCategory<Consonant>(new Category<Consonant>(consonants)),
            _ => null
        };
        Onset3 = rule switch
        {
            EnumeratedSyllableRule.SCLVF => new SyllableCategory<Consonant>(new Category<Consonant>(consonants.Where(c => PhonemeManager.Liquids.Any(l => l == c)).ToList())),
            _ => null
        };
        Nucleus = new SyllableCategory<Vowel>(new Category<Vowel>(vowels));
        Coda = new SyllableCategory<Consonant>(new Category<Consonant>(consonants));

        EnumeratedSyllableRule = rule;
    }

    //TODO rename; concrete sounds weird
    public List<Consonant> GetConcreteOnset(double dropoff, double onset1Chance, double onset2Chance, double onset3Chance)
    {
        var onset = new List<Consonant>();
        var rand = new Random();
        if (Onset1 is not null && rand.NextDouble() < onset1Chance)
        {
            onset.Add(Onset1[Utilities.PowerLaw(Onset1.Count(), dropoff)]);
        }
        if (Onset2 is not null && rand.NextDouble() < onset2Chance)
        {
            onset.Add(Onset2[Utilities.PowerLaw(Onset2.Count(), dropoff)]);
        }
        if (Onset3 is not null && rand.NextDouble() < onset3Chance)
        {
            onset.Add(Onset3[Utilities.PowerLaw(Onset3.Count(), dropoff)]);
        }
        return onset;
    }

    public Vowel GetConcreteNucleus(double dropoff) => Nucleus[Utilities.PowerLaw(Nucleus.Count(), dropoff)];

    public Consonant GetConcreteCoda(double dropoff, double codaChance)
    {
        var rand = new Random();
        if (rand.NextDouble() < codaChance)
        {
            return Coda[Utilities.PowerLaw(Coda.Count(), dropoff)];
        }
        else
        {
            return null;
        }
    }

    public override string ToString()
    {
        var result = "Onset: ";
        if (Onset1 is not null)
        {
            result += $"{Onset1.Phonemes}";
        }
        if (Onset2 is not null)
        {
            result += $"{Onset2.Phonemes}";
        }
        if (Onset3 is not null)
        {
            result += $"{Onset3.Phonemes}";
        }
        //foreach (var category in Onset)
        //{
        //    result += $"{category}";
        //}

        result += $", nucleus: {Nucleus.Phonemes}";

        if (Coda is not null)
        {
            result += $", coda: {Coda.Phonemes}";
        }
        //foreach (var category in Coda)
        //{
        //    result += $"{category}";
        //}

        return result;
    }
}

public class SyllableCategory<T> : IEnumerable<T> where T : Phoneme
{
    public string Name { get; set; }
    public SyllableCategory EnumeratedCategory { get; init; }
    public Category<T> Phonemes { get; set; }

    public SyllableCategory(Category<T> phonemes)
    {
        Phonemes = phonemes;
    }

    public T this[int index]
    {
        get => Phonemes[index];
        set => Phonemes[index] = value;
    }

    public IEnumerator<T> GetEnumerator() => Phonemes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Phonemes.GetEnumerator();
}

public enum SyllableCategory
{
    Consonant, Vowel, Sibilant, Liquid, Final
}

public enum EnumeratedSyllableRule
{
    CV, CVF, VF, CLV, CLVF, SCV, SCVF, SCLVF
}

//I think Consonant, Vowel, Sibilant, Liquid, and Final actually
//need to be *types*; it's not like we would be giving the SyllableRule
//here the correct phonemes for the language, since those haven't been
//determined when this assignment is made
public class SyllableRuleManager
{
    //how to initialize these??
    public static SyllableRule CV { get; private set; }
    public static SyllableRule CVF { get; private set; }
    public static SyllableRule VF { get; private set; }
    public static SyllableRule CLV { get; private set; }
    public static SyllableRule CLVF { get; private set; }
    public static SyllableRule SCV { get; private set; }
    public static SyllableRule SCVF { get; private set; }
    public static SyllableRule SCLVF { get; private set; }
}
