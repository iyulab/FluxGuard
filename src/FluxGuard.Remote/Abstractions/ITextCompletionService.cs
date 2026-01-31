namespace FluxGuard.Remote.Abstractions;

/// <summary>
/// Text completion service abstraction for LLM providers
/// </summary>
public interface ITextCompletionService
{
    /// <summary>
    /// Service name (e.g., "OpenAI", "Azure OpenAI")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether service is available
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Complete a prompt
    /// </summary>
    /// <param name="request">Completion request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Completion response</returns>
    Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Completion request
/// </summary>
public sealed record CompletionRequest
{
    /// <summary>
    /// System prompt
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// User prompt
    /// </summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// Model to use (null for default)
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Temperature (0.0 ~ 2.0)
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Response format (null for text, "json_object" for JSON)
    /// </summary>
    public string? ResponseFormat { get; init; }
}

/// <summary>
/// Completion response
/// </summary>
public sealed record CompletionResponse
{
    /// <summary>
    /// Whether request succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Generated text content
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Model used
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Input tokens used
    /// </summary>
    public int? InputTokens { get; init; }

    /// <summary>
    /// Output tokens used
    /// </summary>
    public int? OutputTokens { get; init; }

    /// <summary>
    /// Latency in milliseconds
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Create success response
    /// </summary>
    public static CompletionResponse Ok(
        string content,
        string? model = null,
        int? inputTokens = null,
        int? outputTokens = null,
        double latencyMs = 0) => new()
    {
        Success = true,
        Content = content,
        Model = model,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create error response
    /// </summary>
    public static CompletionResponse Fail(string error, double latencyMs = 0) => new()
    {
        Success = false,
        Error = error,
        LatencyMs = latencyMs
    };
}
