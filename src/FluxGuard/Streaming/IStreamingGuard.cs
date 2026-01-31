using FluxGuard.Core;

namespace FluxGuard.Streaming;

/// <summary>
/// Streaming guard interface for real-time token validation
/// </summary>
public interface IStreamingGuard
{
    /// <summary>
    /// Guard name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Guard layer (L1, L2, L3)
    /// </summary>
    string Layer { get; }

    /// <summary>
    /// Whether guard is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Validate a streaming chunk
    /// </summary>
    /// <param name="context">Guard context</param>
    /// <param name="chunk">Current chunk to validate</param>
    /// <param name="buffer">Accumulated buffer content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token validation result</returns>
    ValueTask<TokenValidation> ValidateChunkAsync(
        GuardContext context,
        string chunk,
        string buffer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate final accumulated output
    /// </summary>
    /// <param name="context">Guard context</param>
    /// <param name="fullOutput">Complete accumulated output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final validation result</returns>
    ValueTask<TokenValidation> ValidateFinalAsync(
        GuardContext context,
        string fullOutput,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Token validation result for streaming
/// </summary>
public sealed record TokenValidation
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Whether to immediately terminate the stream
    /// </summary>
    public bool ShouldTerminate { get; init; }

    /// <summary>
    /// Whether this chunk should be suppressed/filtered
    /// </summary>
    public bool ShouldSuppress { get; init; }

    /// <summary>
    /// Replacement text (if suppression replaces content)
    /// </summary>
    public string? ReplacementText { get; init; }

    /// <summary>
    /// Risk score (0.0 ~ 1.0)
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Severity level
    /// </summary>
    public Severity Severity { get; init; } = Severity.None;

    /// <summary>
    /// Detected pattern/reason
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Matched text
    /// </summary>
    public string? MatchedText { get; init; }

    /// <summary>
    /// Guard that produced this result
    /// </summary>
    public string? GuardName { get; init; }

    /// <summary>
    /// Create a safe pass result
    /// </summary>
    public static TokenValidation Safe { get; } = new() { Passed = true };

    /// <summary>
    /// Create a pass result
    /// </summary>
    public static TokenValidation Pass(string? guardName = null) => new()
    {
        Passed = true,
        GuardName = guardName
    };

    /// <summary>
    /// Create a suppress result (filter out the content)
    /// </summary>
    public static TokenValidation Suppress(
        string guardName,
        string? replacement = null,
        string? pattern = null) => new()
    {
        Passed = false,
        ShouldSuppress = true,
        ReplacementText = replacement,
        Pattern = pattern,
        GuardName = guardName
    };

    /// <summary>
    /// Create a terminate result (stop the stream)
    /// </summary>
    public static TokenValidation Terminate(
        string guardName,
        double score,
        Severity severity,
        string? pattern = null,
        string? matchedText = null) => new()
    {
        Passed = false,
        ShouldTerminate = true,
        Score = score,
        Severity = severity,
        Pattern = pattern,
        MatchedText = matchedText,
        GuardName = guardName
    };
}
