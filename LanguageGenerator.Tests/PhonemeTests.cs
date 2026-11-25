using System.Reflection;
using System.Text;

namespace LanguageGenerator.Tests;

/*
  the static phonemes are named by their IPA symbols, which the compiler checks only in the
  property name, not in the IpaSymbol string beside it. These catch the two drifting apart,
  lookalike characters (Greek ε for IPA ɛ, ASCII g for IPA ɡ), decomposed diacritics, and
  phonemes left out of their class's lists, All or, for a sound that's only ever an allophone,
  AllophonesOnly
 */
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
    public void StaticPhoneme_IsInItsClassesLists(string id)
    {
        var (_, phoneme) = Find(id);

        Assert.Contains(phoneme, phoneme is Vowel ? (List<Phoneme>)[.. Vowel.All, .. Vowel.AllophonesOnly] : [.. Consonant.All, .. Consonant.AllophonesOnly]);
    }

    [Fact]
    public void Lists_HaveEveryStaticPhonemeExactlyOnce()
    {
        List<Vowel> vowels = [.. Vowel.All, .. Vowel.AllophonesOnly];
        List<Consonant> consonants = [.. Consonant.All, .. Consonant.AllophonesOnly];

        Assert.Equal(StaticPhonemes(typeof(Vowel)).Count(), vowels.Distinct().Count());
        Assert.Equal(vowels.Count, vowels.Distinct().Count());
        Assert.Equal(StaticPhonemes(typeof(Consonant)).Count(), consonants.Distinct().Count());
        Assert.Equal(consonants.Count, consonants.Distinct().Count());
    }

    [Fact]
    public void IpaSymbols_AreUniqueAcrossAllPhonemes()
    {
        var symbols = Phoneme.Every.Select(p => p.IpaSymbol).ToList();

        Assert.Equal(symbols.Count, symbols.Distinct(StringComparer.Ordinal).Count());
    }
}
