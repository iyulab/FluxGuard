namespace FluxGuard.Core;

/// <summary>
/// Guard decision result
/// </summary>
public enum GuardDecision
{
    /// <summary>Safe - passed</summary>
    Pass,

    /// <summary>Flagged - attention needed but passed</summary>
    Flagged,

    /// <summary>Needs escalation - L3 judgment required</summary>
    NeedsEscalation,

    /// <summary>Blocked - request denied</summary>
    Blocked
}
