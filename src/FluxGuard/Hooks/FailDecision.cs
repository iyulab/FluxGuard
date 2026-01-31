using FluxGuard.Core;

namespace FluxGuard.Hooks;

/// <summary>
/// Decision returned by hooks
/// </summary>
public sealed record FailDecision
{
    /// <summary>
    /// Decision type
    /// </summary>
    public required FailDecisionType Type { get; init; }

    /// <summary>
    /// Reason
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Overridden result (when Override)
    /// </summary>
    public GuardResult? OverriddenResult { get; init; }

    /// <summary>
    /// Continue with default processing
    /// </summary>
    public static FailDecision Continue { get; } = new() { Type = FailDecisionType.Continue };

    /// <summary>
    /// Override to pass
    /// </summary>
    public static FailDecision AllowPass(string? reason = null) => new()
    {
        Type = FailDecisionType.Override,
        Reason = reason
    };

    /// <summary>
    /// Override to block
    /// </summary>
    public static FailDecision ForceBlock(string reason) => new()
    {
        Type = FailDecisionType.Override,
        Reason = reason
    };
}

/// <summary>
/// Decision type
/// </summary>
public enum FailDecisionType
{
    /// <summary>Continue with default processing</summary>
    Continue,

    /// <summary>Override result</summary>
    Override
}
