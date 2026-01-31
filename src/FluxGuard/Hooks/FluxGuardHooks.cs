using FluxGuard.Core;

namespace FluxGuard.Hooks;

/// <summary>
/// FluxGuard hooks default implementation
/// Override only the methods you need
/// </summary>
public class FluxGuardHooks : IFluxGuardHooks
{
    /// <inheritdoc />
    public virtual ValueTask<bool> OnBeforeCheckAsync(GuardContext context)
    {
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public virtual ValueTask OnAfterCheckAsync(GuardContext context, GuardResult result)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual ValueTask OnBlockedAsync(GuardContext context, GuardResult result)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual ValueTask OnPassedAsync(GuardContext context, GuardResult result)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual ValueTask OnFlaggedAsync(GuardContext context, GuardResult result)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual ValueTask<FailDecision?> OnCustomDecisionAsync(
        GuardContext context,
        GuardResult result)
    {
        return ValueTask.FromResult<FailDecision?>(null);
    }

    /// <inheritdoc />
    public virtual ValueTask<FailDecision> OnGuardErrorAsync(
        GuardContext context,
        string guardName,
        Exception exception)
    {
        return ValueTask.FromResult(FailDecision.Continue);
    }

    /// <inheritdoc />
    public virtual ValueTask<bool> OnBeforeEscalationAsync(
        GuardContext context,
        GuardResult localResult)
    {
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public virtual ValueTask<GuardResult> OnEscalationTimeoutAsync(
        GuardContext context,
        GuardResult localResult)
    {
        return ValueTask.FromResult(localResult);
    }
}
