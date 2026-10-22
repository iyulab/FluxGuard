namespace FluxGuard.Core;

/// <summary>
/// Guard check context
/// </summary>
public sealed class GuardContext
{
    /// <summary>
    /// Unique request ID
    /// </summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Original input text
    /// </summary>
    public required string OriginalInput { get; init; }

    /// <summary>
    /// The non-text parts of the user turn (attached images, …), in the order the model sees them. An input check judges
    /// each through a registered <see cref="Abstractions.IInputPartTextExtractor"/> with the same input guards as the text,
    /// and reports a part no extractor reads in <see cref="GuardResult.UncheckedParts"/>. Output checks ignore them.
    /// Empty by default.
    /// </summary>
    public IReadOnlyList<GuardInputPart> Parts { get; init; } = [];

    /// <summary>
    /// Normalized input text
    /// </summary>
    public string NormalizedInput { get; internal set; } = string.Empty;

    /// <summary>
    /// User ID (optional)
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Session ID (optional)
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Conversation context (previous messages)
    /// </summary>
    public IReadOnlyList<string> ConversationHistory { get; init; } = [];

    /// <summary>
    /// Additional metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Check start timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}
