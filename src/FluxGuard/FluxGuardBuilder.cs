using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Hooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FluxGuard;

/// <summary>
/// FluxGuard builder
/// Configure FluxGuard instance with fluent API
/// </summary>
public sealed class FluxGuardBuilder
{
    private readonly FluxGuardOptions _options = new();
    private readonly List<IInputGuard> _inputGuards = [];
    private readonly List<IOutputGuard> _outputGuards = [];
    private IFluxGuardHooks _hooks = new FluxGuardHooks();
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    /// <summary>
    /// Create new builder instance
    /// </summary>
    public static FluxGuardBuilder Create() => new();

    /// <summary>
    /// Set preset
    /// </summary>
    /// <param name="preset">Guard preset</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithPreset(GuardPreset preset)
    {
        _options.Preset = preset;
        return this;
    }

    /// <summary>
    /// Set fail mode
    /// </summary>
    /// <param name="failMode">Fail mode</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithFailMode(FailMode failMode)
    {
        _options.FailMode = failMode;
        return this;
    }

    /// <summary>
    /// Configure options
    /// </summary>
    /// <param name="configure">Options configuration action</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder Configure(Action<FluxGuardOptions> configure)
    {
        configure(_options);
        return this;
    }

    /// <summary>
    /// Configure input guard options
    /// </summary>
    /// <param name="configure">Input guard options configuration action</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder ConfigureInputGuards(Action<InputGuardOptions> configure)
    {
        configure(_options.InputGuards);
        return this;
    }

    /// <summary>
    /// Configure output guard options
    /// </summary>
    /// <param name="configure">Output guard options configuration action</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder ConfigureOutputGuards(Action<OutputGuardOptions> configure)
    {
        configure(_options.OutputGuards);
        return this;
    }

    /// <summary>
    /// Add input guard
    /// </summary>
    /// <param name="guard">Input guard</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder AddInputGuard(IInputGuard guard)
    {
        _inputGuards.Add(guard);
        return this;
    }

    /// <summary>
    /// Add output guard
    /// </summary>
    /// <param name="guard">Output guard</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder AddOutputGuard(IOutputGuard guard)
    {
        _outputGuards.Add(guard);
        return this;
    }

    /// <summary>
    /// Set hooks
    /// </summary>
    /// <param name="hooks">Hooks instance</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithHooks(IFluxGuardHooks hooks)
    {
        _hooks = hooks;
        return this;
    }

    /// <summary>
    /// Set hooks (lambda)
    /// </summary>
    /// <param name="configure">Hooks configuration action</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithHooks(Action<LambdaHooksBuilder> configure)
    {
        var builder = new LambdaHooksBuilder();
        configure(builder);
        _hooks = builder.Build();
        return this;
    }

    /// <summary>
    /// Set logger factory
    /// </summary>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Set block threshold
    /// </summary>
    /// <param name="threshold">Threshold (0.0 ~ 1.0)</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithBlockThreshold(double threshold)
    {
        _options.BlockThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Set flag threshold
    /// </summary>
    /// <param name="threshold">Threshold (0.0 ~ 1.0)</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithFlagThreshold(double threshold)
    {
        _options.FlagThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Disable L2 ML guards
    /// </summary>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder DisableL2Guards()
    {
        _options.EnableL2Guards = false;
        return this;
    }

    /// <summary>
    /// Build FluxGuard instance
    /// </summary>
    /// <returns>FluxGuard instance</returns>
    public IFluxGuard Build()
    {
        return new FluxGuardCore(_options, _inputGuards, _outputGuards, _hooks, _loggerFactory);
    }

    internal FluxGuardOptions GetOptions() => _options;
    internal IReadOnlyList<IInputGuard> GetInputGuards() => _inputGuards;
    internal IReadOnlyList<IOutputGuard> GetOutputGuards() => _outputGuards;
    internal IFluxGuardHooks GetHooks() => _hooks;
    internal ILoggerFactory GetLoggerFactory() => _loggerFactory;
}

/// <summary>
/// Lambda-based hooks builder
/// </summary>
public sealed class LambdaHooksBuilder
{
    private Func<GuardContext, ValueTask<bool>>? _onBeforeCheck;
    private Func<GuardContext, GuardResult, ValueTask>? _onAfterCheck;
    private Func<GuardContext, GuardResult, ValueTask>? _onBlocked;
    private Func<GuardContext, GuardResult, ValueTask>? _onPassed;
    private Func<GuardContext, GuardResult, ValueTask>? _onFlagged;
    private Func<GuardContext, GuardResult, ValueTask<FailDecision?>>? _onCustomDecision;
    private Func<GuardContext, string, Exception, ValueTask<FailDecision>>? _onGuardError;

    /// <summary>
    /// Set before check hook
    /// </summary>
    public LambdaHooksBuilder OnBeforeCheck(Func<GuardContext, ValueTask<bool>> handler)
    {
        _onBeforeCheck = handler;
        return this;
    }

    /// <summary>
    /// Set after check hook
    /// </summary>
    public LambdaHooksBuilder OnAfterCheck(Func<GuardContext, GuardResult, ValueTask> handler)
    {
        _onAfterCheck = handler;
        return this;
    }

    /// <summary>
    /// Set blocked hook
    /// </summary>
    public LambdaHooksBuilder OnBlocked(Func<GuardContext, GuardResult, ValueTask> handler)
    {
        _onBlocked = handler;
        return this;
    }

    /// <summary>
    /// Set passed hook
    /// </summary>
    public LambdaHooksBuilder OnPassed(Func<GuardContext, GuardResult, ValueTask> handler)
    {
        _onPassed = handler;
        return this;
    }

    /// <summary>
    /// Set flagged hook
    /// </summary>
    public LambdaHooksBuilder OnFlagged(Func<GuardContext, GuardResult, ValueTask> handler)
    {
        _onFlagged = handler;
        return this;
    }

    /// <summary>
    /// Set custom decision hook
    /// </summary>
    public LambdaHooksBuilder OnCustomDecision(
        Func<GuardContext, GuardResult, ValueTask<FailDecision?>> handler)
    {
        _onCustomDecision = handler;
        return this;
    }

    /// <summary>
    /// Set guard error hook
    /// </summary>
    public LambdaHooksBuilder OnGuardError(
        Func<GuardContext, string, Exception, ValueTask<FailDecision>> handler)
    {
        _onGuardError = handler;
        return this;
    }

    internal IFluxGuardHooks Build() => new LambdaHooks(
        _onBeforeCheck,
        _onAfterCheck,
        _onBlocked,
        _onPassed,
        _onFlagged,
        _onCustomDecision,
        _onGuardError);
}

internal sealed class LambdaHooks(
    Func<GuardContext, ValueTask<bool>>? onBeforeCheck,
    Func<GuardContext, GuardResult, ValueTask>? onAfterCheck,
    Func<GuardContext, GuardResult, ValueTask>? onBlocked,
    Func<GuardContext, GuardResult, ValueTask>? onPassed,
    Func<GuardContext, GuardResult, ValueTask>? onFlagged,
    Func<GuardContext, GuardResult, ValueTask<FailDecision?>>? onCustomDecision,
    Func<GuardContext, string, Exception, ValueTask<FailDecision>>? onGuardError)
    : FluxGuardHooks
{
    public override ValueTask<bool> OnBeforeCheckAsync(GuardContext context)
        => onBeforeCheck?.Invoke(context) ?? base.OnBeforeCheckAsync(context);

    public override ValueTask OnAfterCheckAsync(GuardContext context, GuardResult result)
        => onAfterCheck?.Invoke(context, result) ?? base.OnAfterCheckAsync(context, result);

    public override ValueTask OnBlockedAsync(GuardContext context, GuardResult result)
        => onBlocked?.Invoke(context, result) ?? base.OnBlockedAsync(context, result);

    public override ValueTask OnPassedAsync(GuardContext context, GuardResult result)
        => onPassed?.Invoke(context, result) ?? base.OnPassedAsync(context, result);

    public override ValueTask OnFlaggedAsync(GuardContext context, GuardResult result)
        => onFlagged?.Invoke(context, result) ?? base.OnFlaggedAsync(context, result);

    public override ValueTask<FailDecision?> OnCustomDecisionAsync(
        GuardContext context, GuardResult result)
        => onCustomDecision?.Invoke(context, result) ?? base.OnCustomDecisionAsync(context, result);

    public override ValueTask<FailDecision> OnGuardErrorAsync(
        GuardContext context, string guardName, Exception exception)
        => onGuardError?.Invoke(context, guardName, exception)
            ?? base.OnGuardErrorAsync(context, guardName, exception);
}
