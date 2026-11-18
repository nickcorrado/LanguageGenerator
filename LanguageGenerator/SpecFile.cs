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
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var setting = Settings.Named(property.Name)
                    ?? throw new SpecException($"'{property.Name}' isn't a setting. The settings are {string.Join(", ", Settings.All.Select(s => s.Name))}.");
                if (spec.Allowed(setting) is not null)
                {
                    throw new SpecException($"{setting.Name} is in the spec more than once.");
                }
                spec = spec.With(setting, setting.Parse(property.Value));
            }
            return spec;
        }
    }

    /// <summary>The spec as JSON that Parse reads back, its settings in the order they're declared.</summary>
    public static string Format(LanguageSpec spec) => JsonSerializer.Serialize(
        Settings.All.Where(s => spec.Allowed(s) is not null).ToDictionary(s => s.Name, s => s.Format(spec.Allowed(s)!)),
        new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
