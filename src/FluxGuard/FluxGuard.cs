using System.Diagnostics;
using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Hooks;
using FluxGuard.L1.Normalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FluxGuard;

/// <summary>
/// FluxGuard core implementation
/// </summary>
internal sealed class FluxGuardCore : IFluxGuard
{
    private readonly FluxGuardOptions _options;
    private readonly IReadOnlyList<IInputGuard> _inputGuards;
    private readonly IReadOnlyList<IOutputGuard> _outputGuards;
    private readonly IFluxGuardHooks _hooks;
    private readonly ILogger<FluxGuardCore> _logger;
    private readonly UnicodeNormalizer _normalizer;

    public FluxGuardCore(
        FluxGuardOptions options,
        IReadOnlyList<IInputGuard> inputGuards,
        IReadOnlyList<IOutputGuard> outputGuards,
        IFluxGuardHooks hooks,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _inputGuards = [.. inputGuards.OrderBy(g => g.Order)];
        _outputGuards = [.. outputGuards.OrderBy(g => g.Order)];
        _hooks = hooks;
        _logger = loggerFactory.CreateLogger<FluxGuardCore>();
        _normalizer = new UnicodeNormalizer(
            options.InputGuards.EnableUnicodeNormalization,
            options.InputGuards.EnableZeroWidthFiltering,
            options.InputGuards.EnableHomoglyphDetection);
    }

    /// <inheritdoc />
    public GuardResult CheckInput(string input)
    {
        return CheckInputAsync(input, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task<GuardResult> CheckInputAsync(string input, CancellationToken cancellationToken = default)
    {
        var context = new GuardContext
        {
            OriginalInput = input,
            CancellationToken = cancellationToken
        };
        return CheckInputAsync(context);
    }

    /// <inheritdoc />
    public async Task<GuardResult> CheckInputAsync(GuardContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Before check hook
            if (!await _hooks.OnBeforeCheckAsync(context))
            {
                _logger.LogDebug("Check skipped by OnBeforeCheck hook for request {RequestId}",
                    context.RequestId);
                return GuardResult.Pass(context.RequestId, stopwatch.Elapsed.TotalMilliseconds);
            }

            // Normalize input
            context.NormalizedInput = _normalizer.Normalize(context.OriginalInput);

            // Execute guards
            var triggeredGuards = new List<TriggeredGuard>();
            var maxSeverity = Severity.Info;
            var maxScore = 0.0;
            var needsEscalation = false;
            string? blockReason = null;

            foreach (var guard in _inputGuards.Where(g => g.IsEnabled))
            {
                try
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    var result = await guard.CheckAsync(context);

                    if (!result.Passed || result.Score > 0)
                    {
                        triggeredGuards.Add(new TriggeredGuard
                        {
                            GuardName = guard.Name,
                            Layer = guard.Layer,
                            Pattern = result.Pattern,
                            MatchedText = result.MatchedText,
                            Confidence = result.Score,
                            Severity = result.Severity,
                            Details = result.Details
                        });

                        if (result.Score > maxScore) maxScore = result.Score;
                        if (result.Severity > maxSeverity) maxSeverity = result.Severity;
                        if (result.NeedsEscalation) needsEscalation = true;

                        if (!result.Passed && result.Severity >= Severity.High)
                        {
                            blockReason = $"{guard.Name}: {result.Details ?? result.Pattern}";
                            break; // Immediate block
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var decision = await _hooks.OnGuardErrorAsync(context, guard.Name, ex);

                    if (_options.FailMode == FailMode.Closed &&
                        decision.Type == FailDecisionType.Continue)
                    {
                        _logger.LogError(ex,
                            "Guard {GuardName} failed in Closed mode, blocking request {RequestId}",
                            guard.Name, context.RequestId);
                        blockReason = $"Guard error: {guard.Name}";
                        break;
                    }

                    _logger.LogWarning(ex,
                        "Guard {GuardName} failed in Open mode for request {RequestId}, continuing",
                        guard.Name, context.RequestId);
                }
            }

            // Determine result
            var guardResult = DetermineResult(
                context.RequestId,
                maxScore,
                maxSeverity,
                triggeredGuards,
                needsEscalation,
                blockReason,
                stopwatch.Elapsed.TotalMilliseconds);

            // Custom decision hook
            var customDecision = await _hooks.OnCustomDecisionAsync(context, guardResult);
            if (customDecision?.Type == FailDecisionType.Override &&
                customDecision.OverriddenResult is not null)
            {
                guardResult = customDecision.OverriddenResult;
            }

            // Call result-specific hooks
            await CallResultHooksAsync(context, guardResult);

            // After check hook
            await _hooks.OnAfterCheckAsync(context, guardResult);

            return guardResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Check cancelled for request {RequestId}", context.RequestId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during input check for request {RequestId}",
                context.RequestId);

            return _options.FailMode == FailMode.Open
                ? GuardResult.Pass(context.RequestId, stopwatch.Elapsed.TotalMilliseconds)
                : GuardResult.Block(context.RequestId, "Internal error", 1.0, Severity.Critical, [],
                    stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc />
    public GuardResult CheckOutput(string input, string output)
    {
        return CheckOutputAsync(input, output, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task<GuardResult> CheckOutputAsync(
        string input, string output, CancellationToken cancellationToken = default)
    {
        var context = new GuardContext
        {
            OriginalInput = input,
            CancellationToken = cancellationToken
        };
        return CheckOutputAsync(context, output);
    }

    /// <inheritdoc />
    public async Task<GuardResult> CheckOutputAsync(GuardContext context, string output)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Before check hook
            if (!await _hooks.OnBeforeCheckAsync(context))
            {
                return GuardResult.Pass(context.RequestId, stopwatch.Elapsed.TotalMilliseconds);
            }

            // Execute guards
            var triggeredGuards = new List<TriggeredGuard>();
            var maxSeverity = Severity.Info;
            var maxScore = 0.0;
            string? blockReason = null;

            foreach (var guard in _outputGuards.Where(g => g.IsEnabled))
            {
                try
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    var result = await guard.CheckAsync(context, output);

                    if (!result.Passed || result.Score > 0)
                    {
                        triggeredGuards.Add(new TriggeredGuard
                        {
                            GuardName = guard.Name,
                            Layer = guard.Layer,
                            Pattern = result.Pattern,
                            MatchedText = result.MatchedText,
                            Confidence = result.Score,
                            Severity = result.Severity,
                            Details = result.Details
                        });

                        if (result.Score > maxScore) maxScore = result.Score;
                        if (result.Severity > maxSeverity) maxSeverity = result.Severity;

                        if (!result.Passed && result.Severity >= Severity.High)
                        {
                            blockReason = $"{guard.Name}: {result.Details ?? result.Pattern}";
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var decision = await _hooks.OnGuardErrorAsync(context, guard.Name, ex);

                    if (_options.FailMode == FailMode.Closed &&
                        decision.Type == FailDecisionType.Continue)
                    {
                        blockReason = $"Guard error: {guard.Name}";
                        break;
                    }

                    _logger.LogWarning(ex,
                        "Output guard {GuardName} failed for request {RequestId}",
                        guard.Name, context.RequestId);
                }
            }

            var guardResult = DetermineResult(
                context.RequestId,
                maxScore,
                maxSeverity,
                triggeredGuards,
                needsEscalation: false,
                blockReason,
                stopwatch.Elapsed.TotalMilliseconds);

            // Custom decision hook
            var customDecision = await _hooks.OnCustomDecisionAsync(context, guardResult);
            if (customDecision?.Type == FailDecisionType.Override &&
                customDecision.OverriddenResult is not null)
            {
                guardResult = customDecision.OverriddenResult;
            }

            await CallResultHooksAsync(context, guardResult);
            await _hooks.OnAfterCheckAsync(context, guardResult);

            return guardResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during output check for request {RequestId}",
                context.RequestId);

            return _options.FailMode == FailMode.Open
                ? GuardResult.Pass(context.RequestId, stopwatch.Elapsed.TotalMilliseconds)
                : GuardResult.Block(context.RequestId, "Internal error", 1.0, Severity.Critical, [],
                    stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private GuardResult DetermineResult(
        string requestId,
        double maxScore,
        Severity maxSeverity,
        List<TriggeredGuard> triggeredGuards,
        bool needsEscalation,
        string? blockReason,
        double latencyMs)
    {
        // Explicit block reason
        if (blockReason is not null)
        {
            return GuardResult.Block(requestId, blockReason, maxScore, maxSeverity,
                triggeredGuards, latencyMs);
        }

        // Score-based decision
        if (maxScore >= _options.BlockThreshold)
        {
            return GuardResult.Block(
                requestId,
                triggeredGuards.FirstOrDefault()?.Details ?? "Threshold exceeded",
                maxScore,
                maxSeverity,
                triggeredGuards,
                latencyMs);
        }

        if (needsEscalation && _options.EnableL3Escalation &&
            maxScore >= _options.EscalationThreshold)
        {
            return GuardResult.Escalate(requestId, maxScore, triggeredGuards, latencyMs);
        }

        if (maxScore >= _options.FlagThreshold)
        {
            return GuardResult.Flag(requestId, maxScore, maxSeverity, triggeredGuards, latencyMs);
        }

        return GuardResult.Pass(requestId, latencyMs);
    }

    private async ValueTask CallResultHooksAsync(GuardContext context, GuardResult result)
    {
        switch (result.Decision)
        {
            case GuardDecision.Blocked:
                await _hooks.OnBlockedAsync(context, result);
                break;
            case GuardDecision.Flagged:
                await _hooks.OnFlaggedAsync(context, result);
                break;
            case GuardDecision.Pass:
                await _hooks.OnPassedAsync(context, result);
                break;
        }
    }
}

/// <summary>
/// FluxGuard static factory
/// </summary>
public static class FluxGuard
{
    /// <summary>
    /// Create FluxGuard instance with default settings
    /// Standard preset, FailMode.Open
    /// </summary>
    /// <returns>FluxGuard instance</returns>
    public static IFluxGuard Create()
    {
        return FluxGuardBuilder.Create().Build();
    }

    /// <summary>
    /// Create FluxGuard instance with builder
    /// </summary>
    /// <param name="configure">Builder configuration action</param>
    /// <returns>FluxGuard instance</returns>
    public static IFluxGuard Create(Action<FluxGuardBuilder> configure)
    {
        var builder = FluxGuardBuilder.Create();
        configure(builder);
        return builder.Build();
    }
}
