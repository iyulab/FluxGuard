namespace FluxGuard.Core;

/// <summary>
/// Severity level of guard violations
/// </summary>
public enum Severity
{
    /// <summary>No violation</summary>
    None = 0,

    /// <summary>Informational (logging only)</summary>
    Info = 1,

    /// <summary>Low (warning, pass through)</summary>
    Low = 2,

    /// <summary>Medium (flagged, consider escalation)</summary>
    Medium = 3,

    /// <summary>High (block recommended)</summary>
    High = 4,

    /// <summary>Critical (immediate block)</summary>
    Critical = 5
}
