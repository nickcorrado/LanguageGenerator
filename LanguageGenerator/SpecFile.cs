using System.Text.Json;

namespace LanguageGenerator;

/// <summary>
/// A spec as JSON: an object naming each fixed setting, with its value, or an array of the values
/// it allows any of. A list, as an inventory, is an array of IPA symbols, commonest first.
/// </summary>
//  {
//    "constituent-order": "SOV",
//    "adpositions": ["Prepositions", "Postpositions"],
//    "consonants": ["t", "k", "n", "s", "m"],
//    "mirrors-adjectives": true,
//    "dropoff": 0.3
//  }
public static class SpecFile
{
    public static LanguageSpec Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new SpecException($"The spec isn't valid JSON: {e.Message}");
        }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new SpecException("A spec is a JSON object naming settings, as in { \"constituent-order\": \"SOV\" }.");
            }
            var spec = LanguageSpec.Free;
            HashSet<string> seen = [];
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var setting = Settings.Named(property.Name)
                    ?? throw new SpecException($"'{property.Name}' isn't a setting. The settings are {string.Join(", ", Settings.All.Where(s => Settings.InventoryOf(s) is null).Select(s => s.Name))}.");
                if (!seen.Add(setting.Name))
                {
                    throw new SpecException($"{setting.Name} is in the spec more than once.");
                }
                spec = Settings.SoundsOf(setting) is [_, ..] sounds && property.Value.ValueKind == JsonValueKind.Object
                    ? ParseSounds(spec, setting, sounds, property.Value)
                    : spec.With(setting, setting.Parse(property.Value));
            }
            return spec;
        }
    }

    //an inventory given as the sounds it requires and forbids, each fixing the setting for its sound
    private static LanguageSpec ParseSounds(LanguageSpec spec, ISetting inventory, IReadOnlyList<(Phoneme Sound, ISetting Has)> sounds, JsonElement json)
    {
        foreach (var part in json.EnumerateObject())
        {
            var on = part.Name switch
            {
                "require" => true,
                "forbid" => false,
                _ => throw new SpecException($"{inventory.Name} takes \"require\" and \"forbid\", not \"{part.Name}\"."),
            };
            if (part.Value.ValueKind != JsonValueKind.Array)
            {
                throw new SpecException($"{inventory.Name}'s \"{part.Name}\" is an array of IPA symbols.");
            }
            foreach (var element in part.Value.EnumerateArray())
            {
                var symbol = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.ToString();
                var has = sounds.FirstOrDefault(s => s.Sound.IpaSymbol == symbol).Has
                    ?? throw new SpecException($"{inventory.Name} can't have '{symbol}'. The symbols are {string.Join(" ", sounds.Select(s => s.Sound.IpaSymbol))}.");
                if (spec.Allowed(has) is [bool before])
                {
                    throw new SpecException(before == on ? $"{symbol} is in {inventory.Name}'s \"{part.Name}\" more than once." : $"{inventory.Name} can't both require and forbid {symbol}.");
                }
                spec = spec.With(has, [on]);
            }
        }
        return spec;
    }

    /// <summary>
    /// The spec as JSON that Parse reads back, its settings in the order they're declared. An
    /// inventory given exactly is written as its list, which says everything about its sounds.
    /// </summary>
    public static string Format(LanguageSpec spec)
    {
        var json = new Dictionary<string, object?>();
        foreach (var setting in Settings.All.Where(s => spec.Allowed(s) is not null))
        {
            if (Settings.InventoryOf(setting) is not { } inventory)
            {
                json[setting.Name] = setting.Format(spec.Allowed(setting)!);
            }
            else if (spec.Allowed(inventory) is null && !json.ContainsKey(inventory.Name))
            {
                var sounds = Settings.SoundsOf(inventory);
                List<string> Fixed(bool on) => [.. sounds.Where(s => spec.Allowed(s.Has) is [bool b] && b == on).Select(s => s.Sound.IpaSymbol)];
                var parts = new Dictionary<string, List<string>> { ["require"] = Fixed(true), ["forbid"] = Fixed(false) };
                json[inventory.Name] = parts.Where(p => p.Value.Count > 0).ToDictionary();
            }
        }
        return JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
