using System.Text.Json;

namespace LanguageGenerator;

/// <summary>
/// Something a spec can fix about a language: one property of what a stage of Generate draws.
/// </summary>
//everything about a spec is built from these: reading it from JSON, checking it, drawing a stage
//to match it, and reading it back off a finished language
public interface ISetting
{
    /// <summary>The name a spec gives it, as in "constituent-order".</summary>
    string Name { get; }
    /// <summary>What it's grouped with, for showing settings together, as in "Word order".</summary>
    string Group { get; }
    /// <summary>The values a spec can choose from, by name, or null for a list or a number.</summary>
    IReadOnlyList<string>? ValueNames { get; }
    /// <summary>The values a spec can choose from, or null for a list or a number.</summary>
    IReadOnlyList<object>? AllValues { get; }

    /// <summary>The values a spec allows, from JSON: one, several, or for a list, the list itself.</summary>
    IReadOnlyList<object> Parse(JsonElement json);
    /// <summary>The allowed values as JSON, the way Parse reads them.</summary>
    object? Format(IReadOnlyList<object> allowed);
    /// <summary>What's wrong with a value, or null if nothing is.</summary>
    string? Problem(object value);
    /// <summary>This setting's value in a finished language.</summary>
    object Read(Language language);
}

/// <summary>A setting of a stage whose result is a T.</summary>
public interface ISetting<T> : ISetting
{
    /// <summary>
    /// Whether drawing the stage again can find a value: false for a list or a number, which a
    /// draw would hardly ever match, and which is only ever set directly.
    /// </summary>
    bool Redrawable { get; }
    bool Matches(T drawn, IReadOnlyList<object> allowed);
    /// <summary>The stage's result with the first allowed value in place of what was drawn.</summary>
    T Write(T drawn, IReadOnlyList<object> allowed);
}

/// <summary>
/// A setting whose value, a TValue, is part of what a stage draws, a TStage: read from it, and
/// written into it when no draw gives it.
/// </summary>
public sealed class Setting<TStage, TValue> : ISetting<TStage> where TValue : notnull
{
    public required string Name { get; init; }
    public required string Group { get; init; }
    public required Func<Language, TStage> From { get; init; }
    public required Func<TStage, TValue> Get { get; init; }
    public required Func<TStage, TValue, TStage> Set { get; init; }
    public required ISettingValues<TValue> Values { get; init; }

    public IReadOnlyList<string>? ValueNames => Values.Names;
    public IReadOnlyList<object>? AllValues => Values.All is { } all ? [.. all.Select(v => (object)v)] : null;
    public bool Redrawable => Values.All is not null;

    public IReadOnlyList<object> Parse(JsonElement json) => [.. Values.Parse(json, Name).Select(v => (object)v)];
    public object? Format(IReadOnlyList<object> allowed) => Values.Format([.. allowed.Cast<TValue>()]);
    public string? Problem(object value) => Values.Problem((TValue)value);
    public object Read(Language language) => Get(From(language));

    public bool Matches(TStage drawn, IReadOnlyList<object> allowed) => allowed.Any(a => Values.Same(Get(drawn), (TValue)a));
    public TStage Write(TStage drawn, IReadOnlyList<object> allowed) => Set(drawn, (TValue)allowed[0]);
}

/// <summary>What values a setting can take, and how a spec writes them.</summary>
public interface ISettingValues<T>
{
    /// <summary>The names of the values to choose from, or null where they're open, as for a list.</summary>
    IReadOnlyList<string>? Names { get; }
    /// <summary>The values to choose from, in the order of their names, or null where they're open.</summary>
    IReadOnlyList<T>? All { get; }
    IReadOnlyList<T> Parse(JsonElement json, string setting);
    object? Format(IReadOnlyList<T> allowed);
    string? Problem(T value);
    bool Same(T a, T b);
}

/// <summary>
/// A choice among named values, as an enum's. A spec gives one name, or an array of the names it
/// allows any of.
/// </summary>
public sealed class Choice<T>(IReadOnlyList<(string Name, T Value)> values) : ISettingValues<T> where T : notnull
{
    public IReadOnlyList<string> Names { get; } = [.. values.Select(v => v.Name)];
    public IReadOnlyList<T> All { get; } = [.. values.Select(v => v.Value)];

    public static Choice<T> Of(params T[] values) => new([.. values.Select(v => (v.ToString()!, v))]);

    public IReadOnlyList<T> Parse(JsonElement json, string setting)
    {
        T One(JsonElement element)
        {
            var name = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.True or JsonValueKind.False => element.GetBoolean().ToString(),
                _ => throw new SpecException($"{setting} takes one of {string.Join(", ", Names)}, not {element}."),
            };
            var found = values.Where(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            return found.Count == 1 ? found[0].Value : throw new SpecException($"{setting} takes one of {string.Join(", ", Names)}, not {name}.");
        }
        if (json.ValueKind != JsonValueKind.Array)
        {
            return [One(json)];
        }
        List<T> allowed = [.. json.EnumerateArray().Select(One).Distinct()];
        return allowed.Count > 0 ? allowed : throw new SpecException($"{setting} allows nothing: leave it out to leave it free.");
    }

    //a yes or no is written as JSON's true or false, as Parse also reads it
    public object? Format(IReadOnlyList<T> allowed)
    {
        List<object> names = [.. allowed.Select(a => a is bool b ? (object)b : values.First(v => Same(v.Value, a)).Name)];
        return names.Count == 1 ? names[0] : names;
    }

    public string? Problem(T value) => values.Any(v => Same(v.Value, value)) ? null : $"{value} isn't one of {string.Join(", ", Names)}.";

    public bool Same(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);
}

/// <summary>
/// An ordered list of phonemes, written as their IPA symbols, commonest first, as a list is used:
/// earlier phonemes are drawn more often.
/// </summary>
public sealed class PhonemeList<T>(IReadOnlyList<T> all) : ISettingValues<IReadOnlyList<T>> where T : Phoneme
{
    private readonly Dictionary<string, T> bySymbol = all.ToDictionary(p => p.IpaSymbol);

    public IReadOnlyList<string>? Names => null;
    public IReadOnlyList<IReadOnlyList<T>>? All => null;

    public IReadOnlyList<IReadOnlyList<T>> Parse(JsonElement json, string setting)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            throw new SpecException($"{setting} is an array of IPA symbols, commonest first, as in [\"{all[0].IpaSymbol}\", \"{all[1].IpaSymbol}\"].");
        }
        List<T> phonemes = [];
        foreach (var element in json.EnumerateArray())
        {
            var symbol = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.ToString();
            phonemes.Add(bySymbol.TryGetValue(symbol, out var phoneme) ? phoneme
                : throw new SpecException($"{setting} can't have '{symbol}'. The symbols are {string.Join(" ", bySymbol.Keys)}."));
        }
        return [phonemes];
    }

    public object? Format(IReadOnlyList<IReadOnlyList<T>> allowed) => allowed[0].Select(p => p.IpaSymbol).ToList();

    public string? Problem(IReadOnlyList<T> value) =>
        value.Count == 0 ? "An inventory needs at least one sound."
        : value.Distinct().Count() < value.Count ? $"{string.Join(", ", value.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key.IpaSymbol))} is in the inventory more than once."
        : null;

    public bool Same(IReadOnlyList<T> a, IReadOnlyList<T> b) => a.SequenceEqual(b);
}

/// <summary>A number between two bounds, which it can't be.</summary>
public sealed class Between(double above, double below) : ISettingValues<double>
{
    public IReadOnlyList<string>? Names => null;
    public IReadOnlyList<double>? All => null;

    public IReadOnlyList<double> Parse(JsonElement json, string setting) => json.ValueKind == JsonValueKind.Number
        ? [json.GetDouble()]
        : throw new SpecException($"{setting} is a number between {above} and {below}, not {json}.");

    public object? Format(IReadOnlyList<double> allowed) => allowed[0];

    public string? Problem(double value) => value > above && value < below ? null : $"{value} isn't between {above} and {below}.";

    public bool Same(double a, double b) => a == b;
}

/// <summary>A spec that can't be read or can't be met, with what's wrong.</summary>
public class SpecException(string message) : Exception(message);
