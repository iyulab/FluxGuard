namespace FluxGuard.Core;

/// <summary>
/// Guard check result
/// </summary>
public sealed record GuardResult
{
    /// <summary>
    /// Final decision
    /// </summary>
    public required GuardDecision Decision { get; init; }

    /// <summary>
    /// Whether blocked
    /// </summary>
    public bool IsBlocked => Decision == GuardDecision.Blocked;

    /// <summary>
    /// Whether flagged
    /// </summary>
    public bool IsFlagged => Decision == GuardDecision.Flagged;

    /// <summary>
    /// Whether escalation is needed
    /// </summary>
    public bool NeedsEscalation => Decision == GuardDecision.NeedsEscalation;

    /// <summary>
    /// Aggregate risk score (0.0 ~ 1.0)
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Maximum severity
    /// </summary>
    public Severity MaxSeverity { get; init; } = Severity.Info;

    /// <summary>
    /// List of triggered guards
    /// </summary>
    public IReadOnlyList<TriggeredGuard> TriggeredGuards { get; init; } = [];

    /// <summary>
    /// Block reason (when blocked)
    /// </summary>
    public string? BlockReason { get; init; }

    /// <summary>
    /// Processing time (milliseconds)
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Request ID
    /// </summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>
    /// Create safe pass result
    /// </summary>
    public static GuardResult Pass(string requestId, double latencyMs) => new()
    {
        Decision = GuardDecision.Pass,
        Score = 0.0,
        RequestId = requestId,
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create block result
    /// </summary>
    public static GuardResult Block(
        string requestId,
        string reason,
        double score,
        Severity severity,
        IReadOnlyList<TriggeredGuard> triggeredGuards,
        double latencyMs) => new()
    {
        Decision = GuardDecision.Blocked,
        Score = score,
        MaxSeverity = severity,
        BlockReason = reason,
        TriggeredGuards = triggeredGuards,
        RequestId = requestId,
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create flag result
    /// </summary>
    public static GuardResult Flag(
        string requestId,
        double score,
        Severity severity,
        IReadOnlyList<TriggeredGuard> triggeredGuards,
        double latencyMs) => new()
    {
        Decision = GuardDecision.Flagged,
        Score = score,
        MaxSeverity = severity,
        TriggeredGuards = triggeredGuards,
        RequestId = requestId,
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create escalation result
    /// </summary>
    public static GuardResult Escalate(
        string requestId,
        double score,
        IReadOnlyList<TriggeredGuard> triggeredGuards,
        double latencyMs) => new()
    {
        Decision = GuardDecision.NeedsEscalation,
        Score = score,
        TriggeredGuards = triggeredGuards,
        RequestId = requestId,
        LatencyMs = latencyMs
    };
}

/// <summary>
/// Triggered guard information
/// </summary>
public sealed record TriggeredGuard
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
    /// Detected pattern/rule
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Matched text
    /// </summary>
    public string? MatchedText { get; init; }

    /// <summary>
    /// Confidence score
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Severity
    /// </summary>
    public Severity Severity { get; init; }

    /// <summary>
    /// Additional details
    /// </summary>
    public string? Details { get; init; }
}
