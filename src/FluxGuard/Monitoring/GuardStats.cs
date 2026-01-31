namespace FluxGuard.Monitoring;

/// <summary>
/// Guard statistics container
/// </summary>
public sealed record GuardStats
{
    /// <summary>
    /// Total number of checks performed
    /// </summary>
    public long TotalChecks { get; init; }

    /// <summary>
    /// Number of passed checks
    /// </summary>
    public long PassedCount { get; init; }

    /// <summary>
    /// Number of blocked checks
    /// </summary>
    public long BlockedCount { get; init; }

    /// <summary>
    /// Number of flagged checks
    /// </summary>
    public long FlaggedCount { get; init; }

    /// <summary>
    /// Number of escalated checks
    /// </summary>
    public long EscalatedCount { get; init; }

    /// <summary>
    /// Number of errors
    /// </summary>
    public long ErrorCount { get; init; }

    /// <summary>
    /// Total input checks
    /// </summary>
    public long InputChecks { get; init; }

    /// <summary>
    /// Total output checks
    /// </summary>
    public long OutputChecks { get; init; }

    /// <summary>
    /// Pass rate (0.0 ~ 1.0)
    /// </summary>
    public double PassRate => TotalChecks > 0
        ? (double)PassedCount / TotalChecks
        : 1.0;

    /// <summary>
    /// Block rate (0.0 ~ 1.0)
    /// </summary>
    public double BlockRate => TotalChecks > 0
        ? (double)BlockedCount / TotalChecks
        : 0.0;

    /// <summary>
    /// Average latency in milliseconds
    /// </summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>
    /// 95th percentile latency in milliseconds
    /// </summary>
    public double P95LatencyMs { get; init; }

    /// <summary>
    /// 99th percentile latency in milliseconds
    /// </summary>
    public double P99LatencyMs { get; init; }

    /// <summary>
    /// Statistics per guard
    /// </summary>
    public IReadOnlyDictionary<string, GuardLayerStats> ByGuard { get; init; } =
        new Dictionary<string, GuardLayerStats>();

    /// <summary>
    /// Statistics collection start time
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Time span covered by these statistics
    /// </summary>
    public TimeSpan Duration => DateTimeOffset.UtcNow - StartTime;

    /// <summary>
    /// Create empty stats
    /// </summary>
    public static GuardStats Empty => new();
}

/// <summary>
/// Statistics for a specific guard layer
/// </summary>
public sealed record GuardLayerStats
{
    /// <summary>
    /// Guard name
    /// </summary>
    public required string GuardName { get; init; }

    /// <summary>
    /// Guard layer (L1, L2, L3)
    /// </summary>
    public required string Layer { get; init; }

    /// <summary>
    /// Total checks by this guard
    /// </summary>
    public long TotalChecks { get; init; }

    /// <summary>
    /// Number of times this guard triggered (detected threat)
    /// </summary>
    public long TriggeredCount { get; init; }

    /// <summary>
    /// Trigger rate (0.0 ~ 1.0)
    /// </summary>
    public double TriggerRate => TotalChecks > 0
        ? (double)TriggeredCount / TotalChecks
        : 0.0;

    /// <summary>
    /// Average latency for this guard
    /// </summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>
    /// Number of errors in this guard
    /// </summary>
    public long ErrorCount { get; init; }
}
