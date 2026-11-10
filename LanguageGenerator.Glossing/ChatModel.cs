namespace LanguageGenerator.Glossing;

/// <summary>
/// What the glosser needs from a language model: a system prompt and one user message in, text out,
/// and what it cost.
/// </summary>
//each provider gets a small adapter, so models can be swapped and compared
public interface IChatModel
{
    string Name { get; }
    Task<ChatReply> Complete(string system, string user, CancellationToken cancellationToken = default);
}

public record ChatReply(string Text, TokenUsage Usage);

/// <summary>
/// What a request cost. Input counts what was read in full, and CachedInput what was read from the
/// provider's cache, which is much cheaper where providers cache at all.
/// </summary>
public record TokenUsage(long Input, long CachedInput, long Output)
{
    public static TokenUsage None { get; } = new(0, 0, 0);

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.Input + b.Input, a.CachedInput + b.CachedInput, a.Output + b.Output);

    public override string ToString() => $"{Input:N0} input tokens ({CachedInput:N0} more cached), {Output:N0} output";
}

/// <summary>
/// A reply that can't be used at all, like a refusal, as opposed to one with lines that don't
/// parse.
/// </summary>
public class ChatModelException(string message) : Exception(message);
