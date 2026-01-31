using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.Remote.Abstractions;

/// <summary>
/// L3 Remote guard interface
/// LLM-based analysis for uncertain or escalated cases
/// </summary>
public interface IRemoteGuard
{
    /// <summary>
    /// Guard name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Guard layer (always "L3")
    /// </summary>
    string Layer => "L3";

    /// <summary>
    /// Whether guard is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Execution order (lower executes first)
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Perform remote input check
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="l2Result">L2 guard result to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Remote guard result</returns>
    ValueTask<RemoteGuardResult> CheckInputAsync(
        GuardContext context,
        GuardResult l2Result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform remote output check
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="output">LLM output text</param>
    /// <param name="l2Result">L2 guard result to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Remote guard result</returns>
    ValueTask<RemoteGuardResult> CheckOutputAsync(
        GuardContext context,
        string output,
        GuardResult l2Result,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Remote guard result
/// </summary>
public sealed record RemoteGuardResult
{
    /// <summary>
    /// Whether check passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Risk score (0.0 ~ 1.0)
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Severity level
    /// </summary>
    public Severity Severity { get; init; } = Severity.None;

    /// <summary>
    /// LLM's reasoning/explanation
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Detected categories
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// Latency in milliseconds
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Whether result was from cache
    /// </summary>
    public bool FromCache { get; init; }

    /// <summary>
    /// Model used for analysis
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Create a pass result
    /// </summary>
    public static RemoteGuardResult Pass(double latencyMs, string? reasoning = null) => new()
    {
        Passed = true,
        Score = 0.0,
        Reasoning = reasoning,
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create a block result
    /// </summary>
    public static RemoteGuardResult Block(
        double score,
        Severity severity,
        string reasoning,
        IReadOnlyList<string>? categories = null,
        double latencyMs = 0) => new()
    {
        Passed = false,
        Score = score,
        Severity = severity,
        Reasoning = reasoning,
        Categories = categories ?? [],
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create a cached result
    /// </summary>
    public static RemoteGuardResult FromCacheEntry(RemoteGuardResult cached, double latencyMs) => cached with
    {
        FromCache = true,
        LatencyMs = latencyMs
    };
}
