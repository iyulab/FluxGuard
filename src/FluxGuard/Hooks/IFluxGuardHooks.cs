using FluxGuard.Core;

namespace FluxGuard.Hooks;

/// <summary>
/// FluxGuard hooks interface
/// Enables intervention at all decision points
/// </summary>
public interface IFluxGuardHooks
{
    /// <summary>
    /// Called before check starts
    /// </summary>
    /// <param name="context">Check context</param>
    /// <returns>Whether to continue (false skips check)</returns>
    ValueTask<bool> OnBeforeCheckAsync(GuardContext context);

    /// <summary>
    /// Called after check completes
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="result">Check result</param>
    ValueTask OnAfterCheckAsync(GuardContext context, GuardResult result);

    /// <summary>
    /// Called when request is blocked
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="result">Block result</param>
    ValueTask OnBlockedAsync(GuardContext context, GuardResult result);

    /// <summary>
    /// Called when request passes
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="result">Pass result</param>
    ValueTask OnPassedAsync(GuardContext context, GuardResult result);

    /// <summary>
    /// Called when request is flagged
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="result">Flag result</param>
    ValueTask OnFlaggedAsync(GuardContext context, GuardResult result);

    /// <summary>
    /// Called when custom decision is needed
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="result">Current result</param>
    /// <returns>Custom decision (null for default processing)</returns>
    ValueTask<FailDecision?> OnCustomDecisionAsync(GuardContext context, GuardResult result);

    /// <summary>
    /// Called when guard error occurs
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="guardName">Name of failed guard</param>
    /// <param name="exception">Exception</param>
    /// <returns>Fail decision</returns>
    ValueTask<FailDecision> OnGuardErrorAsync(
        GuardContext context,
        string guardName,
        Exception exception);

    /// <summary>
    /// Called before L3 escalation
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="localResult">Local check result</param>
    /// <returns>Whether to proceed with escalation</returns>
    ValueTask<bool> OnBeforeEscalationAsync(GuardContext context, GuardResult localResult);

    /// <summary>
    /// Called when L3 escalation times out
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="localResult">Local check result</param>
    /// <returns>Fallback result</returns>
    ValueTask<GuardResult> OnEscalationTimeoutAsync(GuardContext context, GuardResult localResult);
}
