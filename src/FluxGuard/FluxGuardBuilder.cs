using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Hooks;
using FluxGuard.L1.Patterns;
using FluxGuard.L2.Guards.Input;
using FluxGuard.L2.Guards.Output;
using FluxGuard.L2.ML;
using FluxGuard.Monitoring;
using FluxGuard.Presets;
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
    private readonly List<IInputGuard> _l2InputGuards = [];
    private readonly List<IOutputGuard> _l2OutputGuards = [];
    private readonly List<IRemoteGuard> _remoteGuards = [];
    private IFluxGuardHooks _hooks = new FluxGuardHooks();
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private GuardPreset? _requestedPreset;
    private IPatternRegistry? _patternRegistry;
    private IGuardStatsCollector? _stats;

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
        RequestPreset(preset);
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
    /// Add L3 remote guard
    /// </summary>
    /// <param name="guard">Remote guard</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder AddRemoteGuard(IRemoteGuard guard)
    {
        _remoteGuards.Add(guard);
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
    /// Feeds a statistics collector (<see cref="InMemoryStatsCollector"/>, or <c>FluxGuardMetrics</c> for
    /// System.Diagnostics.Metrics instruments) from the pipeline: every check, every guard execution with its latency,
    /// and every guard error. Without it nothing is recorded.
    /// </summary>
    /// <param name="stats">Statistics collector</param>
    /// <returns>Builder instance</returns>
    public FluxGuardBuilder WithStats(IGuardStatsCollector stats)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        return this;
    }

    /// <summary>
    /// Adds the L2 (local ML) guards: prompt-injection detection on input and toxicity detection on output.
    /// No preset registers them - they load ONNX models, which this library does not ship - so this call is the
    /// way to turn them on. They are added on top of whatever else the builder resolves to, the default preset included.
    /// </summary>
    /// <param name="sessionManager">Owns the ONNX sessions. The caller keeps ownership and disposes it.</param>
    /// <param name="options">Thresholds and <see cref="L2GuardOptions.ModelsBasePath"/>; defaults when null.</param>
    /// <returns>Builder instance</returns>
    /// <exception cref="InvalidOperationException">
    /// A model or vocabulary file is missing. The guards were asked for explicitly, so a missing file is an error
    /// here rather than a guard that answers "safe" to everything.
    /// </exception>
    public FluxGuardBuilder AddL2Guards(OnnxSessionManager sessionManager, L2GuardOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        options ??= new L2GuardOptions();

        var missing = new[]
            {
                ModelLoader.GetPromptInjectionModelInfo(options.ModelsBasePath),
                ModelLoader.GetToxicityModelInfo(options.ModelsBasePath),
            }
            .SelectMany(model => new[] { model.ModelPath, model.TokenizerPath })
            .Where(path => !File.Exists(path))
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "AddL2Guards needs the L2 model files, and these are missing: " + string.Join(", ", missing) +
                ". Place the models under " + ModelLoader.GetModelsDirectory(options.ModelsBasePath) +
                " or set L2GuardOptions.ModelsBasePath.");
        }

        // Kept apart from _inputGuards/_outputGuards: those lists decide whether the default preset applies, and
        // asking for L2 on top must not drop the L1 guards.
        _l2InputGuards.Add(new L2PromptInjectionGuard(sessionManager, options));
        _l2OutputGuards.Add(new L2ToxicityGuard(sessionManager, options));
        return this;
    }

    /// <summary>
    /// Build FluxGuard instance
    /// </summary>
    /// <returns>FluxGuard instance</returns>
    /// <remarks>
    /// A requested preset (<see cref="WithPreset"/> or one of the <c>Apply…Preset</c> extensions) is turned into guards
    /// here, from the options as they stand when the pipeline is built - so a switch configured before or after the
    /// preset was requested reaches its guard either way. A builder that was given no input or output guard and no
    /// preset gets the preset named by <see cref="FluxGuardOptions.Preset"/> (standard by default): the default is a
    /// guarded pipeline, never an empty one. A builder given guards and no preset gets exactly those guards.
    /// </remarks>
    public IFluxGuard Build()
    {
        var inputGuards = new List<IInputGuard>(_inputGuards);
        var outputGuards = new List<IOutputGuard>(_outputGuards);

        var preset = _requestedPreset
            ?? (_inputGuards.Count == 0 && _outputGuards.Count == 0 ? _options.Preset : (GuardPreset?)null);
        if (preset is { } requested)
        {
            var registry = _patternRegistry ?? new PatternRegistry();
            inputGuards.AddRange(PresetGuards.InputGuards(requested, registry, _options));
            outputGuards.AddRange(PresetGuards.OutputGuards(requested, registry, _options));
        }

        inputGuards.AddRange(_l2InputGuards);
        outputGuards.AddRange(_l2OutputGuards);

        return new FluxGuardCore(_options, inputGuards, outputGuards, _remoteGuards, _hooks, _loggerFactory, _stats);
    }

    /// <summary>Asks for a preset's guards; they are created in <see cref="Build"/> from the final options.</summary>
    internal FluxGuardBuilder RequestPreset(GuardPreset preset)
    {
        _options.Preset = preset;
        _requestedPreset = preset;
        return this;
    }

    /// <summary>Uses a shared pattern registry (the dependency-injection path) instead of a private one.</summary>
    internal FluxGuardBuilder WithPatternRegistry(IPatternRegistry registry)
    {
        _patternRegistry = registry;
        return this;
    }

    internal FluxGuardOptions GetOptions() => _options;
    internal IReadOnlyList<IInputGuard> GetInputGuards() => _inputGuards;
    internal IReadOnlyList<IOutputGuard> GetOutputGuards() => _outputGuards;
    internal IReadOnlyList<IRemoteGuard> GetRemoteGuards() => _remoteGuards;
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
