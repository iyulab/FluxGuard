using FluxGuard.Core;

namespace FluxGuard.Abstractions;

/// <summary>
/// Input guard interface
/// </summary>
public interface IInputGuard
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
    /// Execution order (lower executes first)
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Perform input check
    /// </summary>
    /// <param name="context">Check context</param>
    /// <returns>Check result</returns>
    ValueTask<GuardCheckResult> CheckAsync(GuardContext context);
}

/// <summary>
/// Individual guard check result
/// </summary>
public sealed record GuardCheckResult
{
    /// <summary>
    /// Guard name that produced this result
    /// </summary>
    public string? GuardName { get; init; }

    /// <summary>
    /// Whether passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Risk score (0.0 ~ 1.0)
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Severity
    /// </summary>
    public Severity Severity { get; init; } = Severity.None;

    /// <summary>
    /// Detected pattern
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Matched text
    /// </summary>
    public string? MatchedText { get; init; }

    /// <summary>
    /// Human-readable message
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Details
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Whether escalation is needed
    /// </summary>
    public bool NeedsEscalation { get; init; }

    /// <summary>
    /// Latency in milliseconds
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Safe pass result
    /// </summary>
    public static GuardCheckResult Safe { get; } = new() { Passed = true, Score = 0.0 };

    /// <summary>
    /// Create a pass result with guard name
    /// </summary>
    public static GuardCheckResult Pass(string guardName, string? message = null) => new()
    {
        GuardName = guardName,
        Passed = true,
        Score = 0.0,
        Message = message
    };

    /// <summary>
    /// Create block result
    /// </summary>
    public static GuardCheckResult Block(
        double score,
        Severity severity,
        string? pattern = null,
        string? matchedText = null,
        string? details = null) => new()
    {
        Passed = false,
        Score = score,
        Severity = severity,
        Pattern = pattern,
        MatchedText = matchedText,
        Details = details
    };

    /// <summary>
    /// Create escalation result
    /// </summary>
    public static GuardCheckResult Escalate(
        double score,
        string? pattern = null,
        string? details = null) => new()
    {
        Passed = true,
        Score = score,
        NeedsEscalation = true,
        Pattern = pattern,
        Details = details
    };
}
