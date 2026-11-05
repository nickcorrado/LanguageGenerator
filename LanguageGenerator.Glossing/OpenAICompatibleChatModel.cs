using System.Net.Http.Json;
using System.Text.Json;

namespace LanguageGenerator.Glossing;

/// <summary>
/// Any server with an OpenAI-style chat completions endpoint: OpenAI itself, or a local model run
/// by Ollama (http://localhost:11434/v1), LM Studio or llama.cpp, which need no key.
/// </summary>
//plain HTTP, since that endpoint's shape is the one thing all of them share
public sealed class OpenAICompatibleChatModel(Uri baseUrl, string model, string? apiKey) : IChatModel, IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public string Name => model;

    public async Task<ChatReply> Complete(string system, string user, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrl.ToString().TrimEnd('/') + "/chat/completions"))
        {
            Content = JsonContent.Create(new
            {
                model,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            }),
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new("Bearer", apiKey);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ChatModelException($"{baseUrl} answered {(int)response.StatusCode}: {body}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        long Count(JsonElement element, string name) => element.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt64() : 0;
        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        if (usage.ValueKind != JsonValueKind.Object)
        {
            return new ChatReply(text, TokenUsage.None);
        }
        //cached prompt tokens, where the server reports them, are part of the prompt's count
        var cached = usage.TryGetProperty("prompt_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object ? Count(details, "cached_tokens") : 0;
        return new ChatReply(text, new TokenUsage(Count(usage, "prompt_tokens") - cached, cached, Count(usage, "completion_tokens")));
    }

    public void Dispose() => http.Dispose();
}
