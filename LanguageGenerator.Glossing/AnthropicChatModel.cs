using Anthropic;
using Anthropic.Models.Beta.Messages;

namespace LanguageGenerator.Glossing;

/// <summary>
/// Claude, through Anthropic's SDK, which finds its credentials itself: ANTHROPIC_API_KEY, or a
/// profile from ant auth login.
/// </summary>
//the system prompt is marked for caching, so a long text's batches read it from the cache after the
//first, if it's long enough for the model to cache. Fallbacks let the API retry a policy refusal on
//another model, though glossing should never draw one
public sealed class AnthropicChatModel(string model, string? effort = null) : IChatModel
{
    private readonly AnthropicClient client = new();

    public string Name => model;

    public async Task<ChatReply> Complete(string system, string user, CancellationToken cancellationToken = default)
    {
        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            System = new List<BetaTextBlockParam> { new() { Text = system, CacheControl = new BetaCacheControlEphemeral() } },
            Messages = [new BetaUserMessageParam { Content = user }],
            OutputConfig = effort is null ? null : new BetaOutputConfig { Effort = ParseEffort(effort) },
        }, cancellationToken);

        if (response.StopReason == "refusal")
        {
            throw new ChatModelException($"{model} declined to gloss this batch.");
        }
        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        var usage = response.Usage;
        return new ChatReply(text, new TokenUsage(
            usage.InputTokens + (usage.CacheCreationInputTokens ?? 0),
            usage.CacheReadInputTokens ?? 0,
            usage.OutputTokens));
    }

    private static Effort ParseEffort(string effort) => effort switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => throw new ArgumentException($"'{effort}' isn't an effort. Use low, medium, high or max.", nameof(effort)),
    };
}
