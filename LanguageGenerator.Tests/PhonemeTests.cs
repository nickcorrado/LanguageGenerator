using System.Reflection;
using System.Text;

namespace LanguageGenerator.Tests;

//the static phonemes are named by their IPA symbols, which the compiler checks only in the
//property name, not in the IpaSymbol string beside it. These catch the two drifting apart,
//lookalike characters (Greek ε for IPA ɛ, ASCII g for IPA ɡ), decomposed diacritics, and
//phonemes left out of their class's All list
public class PhonemeTests
{
    private static IEnumerable<PropertyInfo> StaticPhonemes(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == type);

    //each static phoneme by class and name, like "Vowel.iː"
    public static TheoryData<string> Phonemes()
    {
        var data = new TheoryData<string>();
        foreach (var property in StaticPhonemes(typeof(Vowel)).Concat(StaticPhonemes(typeof(Consonant))))
        {
            data.Add($"{property.DeclaringType!.Name}.{property.Name}");
        }
        return data;
    }

    private static (PropertyInfo Property, Phoneme Phoneme) Find(string id)
    {
        var type = id.StartsWith("Vowel.") ? typeof(Vowel) : typeof(Consonant);
        var property = type.GetProperty(id[(id.IndexOf('.') + 1)..], BindingFlags.Public | BindingFlags.Static)!;
        return (property, (Phoneme)property.GetValue(null)!);
    }

    [Theory]
    [MemberData(nameof(Phonemes))]
    public void StaticPhoneme_IsNamedByExactlyItsIpaSymbol(string id)
    {
        var (property, phoneme) = Find(id);

        Assert.Equal(property.Name, phoneme.IpaSymbol);
    }

    [Theory]
    [MemberData(nameof(Phonemes))]
    public void StaticPhoneme_IpaSymbolIsInNormalizationFormC(string id)
    {
        var (_, phoneme) = Find(id);

        Assert.True(phoneme.IpaSymbol.IsNormalized(NormalizationForm.FormC), $"{id} is spelled with decomposed characters");
    }

    [Theory]
    [MemberData(nameof(Phonemes))]
    public void StaticPhoneme_IsInItsClassesAllList(string id)
    {
        var (_, phoneme) = Find(id);

        Assert.Contains(phoneme, phoneme is Vowel ? Vowel.All : Consonant.All);
    }

    [Fact]
    public void All_ListsEveryStaticPhonemeExactlyOnce()
    {
        Assert.Equal(StaticPhonemes(typeof(Vowel)).Count(), Vowel.All.Distinct().Count());
        Assert.Equal(Vowel.All.Count, Vowel.All.Distinct().Count());
        Assert.Equal(StaticPhonemes(typeof(Consonant)).Count(), Consonant.All.Distinct().Count());
        Assert.Equal(Consonant.All.Count, Consonant.All.Distinct().Count());
    }

    [Fact]
    public void IpaSymbols_AreUniqueAcrossAllPhonemes()
    {
        var symbols = Vowel.All.Select(v => v.IpaSymbol).Concat(Consonant.All.Select(c => c.IpaSymbol)).ToList();

        Assert.Equal(symbols.Count, symbols.Distinct(StringComparer.Ordinal).Count());
    }
}
