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
internal sealed partial class FluxGuardCore : IFluxGuard
{
    private readonly FluxGuardOptions _options;
    private readonly IReadOnlyList<IInputGuard> _inputGuards;
    private readonly IReadOnlyList<IOutputGuard> _outputGuards;
    private readonly IReadOnlyList<IRemoteGuard> _remoteGuards;
    private readonly IFluxGuardHooks _hooks;
    private readonly ILogger<FluxGuardCore> _logger;
    private readonly UnicodeNormalizer _normalizer;

    public FluxGuardCore(
        FluxGuardOptions options,
        IReadOnlyList<IInputGuard> inputGuards,
        IReadOnlyList<IOutputGuard> outputGuards,
        IReadOnlyList<IRemoteGuard> remoteGuards,
        IFluxGuardHooks hooks,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _inputGuards = [.. inputGuards.OrderBy(g => g.Order)];
        _outputGuards = [.. outputGuards.OrderBy(g => g.Order)];
        _remoteGuards = [.. remoteGuards.Where(g => g.IsEnabled).OrderBy(g => g.Order)];
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
                LogCheckSkippedByHook(_logger, context.RequestId);
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
                        LogGuardFailedClosedMode(_logger, ex, guard.Name, context.RequestId);
                        blockReason = $"Guard error: {guard.Name}";
                        break;
                    }

                    LogGuardFailedOpenMode(_logger, ex, guard.Name, context.RequestId);
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

            // L3 escalation: execute remote guards when escalation is needed
            if (guardResult.NeedsEscalation && _remoteGuards.Count > 0)
            {
                guardResult = await ExecuteInputEscalationAsync(
                    context, guardResult, stopwatch);
            }

            // Custom decision hook
            var customDecision = await _hooks.OnCustomDecisionAsync(context, guardResult);
            if (customDecision is not null)
            {
                guardResult = customDecision.Type switch
                {
                    FailDecisionType.Override when customDecision.OverriddenResult is not null
                        => customDecision.OverriddenResult,
                    FailDecisionType.AllowPass
                        => GuardResult.Pass(guardResult.RequestId, guardResult.LatencyMs),
                    FailDecisionType.ForceBlock
                        => GuardResult.Block(guardResult.RequestId,
                            customDecision.Reason ?? "Forced block",
                            1.0, Severity.Critical, [], guardResult.LatencyMs),
                    _ => guardResult
                };
            }

            // Call result-specific hooks
            await CallResultHooksAsync(context, guardResult);

            // After check hook
            await _hooks.OnAfterCheckAsync(context, guardResult);

            return guardResult;
        }
        catch (OperationCanceledException)
        {
            LogCheckCancelled(_logger, context.RequestId);
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedInputCheckError(_logger, ex, context.RequestId);

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
            var needsEscalation = false;
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
                        if (result.NeedsEscalation) needsEscalation = true;

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

                    LogOutputGuardFailed(_logger, ex, guard.Name, context.RequestId);
                }
            }

            var guardResult = DetermineResult(
                context.RequestId,
                maxScore,
                maxSeverity,
                triggeredGuards,
                needsEscalation,
                blockReason,
                stopwatch.Elapsed.TotalMilliseconds);

            // L3 escalation: execute remote guards when escalation is needed
            if (guardResult.NeedsEscalation && _remoteGuards.Count > 0)
            {
                guardResult = await ExecuteOutputEscalationAsync(
                    context, output, guardResult, stopwatch);
            }

            // Custom decision hook
            var customDecision = await _hooks.OnCustomDecisionAsync(context, guardResult);
            if (customDecision is not null)
            {
                guardResult = customDecision.Type switch
                {
                    FailDecisionType.Override when customDecision.OverriddenResult is not null
                        => customDecision.OverriddenResult,
                    FailDecisionType.AllowPass
                        => GuardResult.Pass(guardResult.RequestId, guardResult.LatencyMs),
                    FailDecisionType.ForceBlock
                        => GuardResult.Block(guardResult.RequestId,
                            customDecision.Reason ?? "Forced block",
                            1.0, Severity.Critical, [], guardResult.LatencyMs),
                    _ => guardResult
                };
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
            LogUnexpectedOutputCheckError(_logger, ex, context.RequestId);

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

    private async Task<GuardResult> ExecuteInputEscalationAsync(
        GuardContext context,
        GuardResult l2Result,
        Stopwatch stopwatch)
    {
        // Before escalation hook
        if (!await _hooks.OnBeforeEscalationAsync(context, l2Result))
        {
            LogEscalationSkippedByHook(_logger, context.RequestId);
            return l2Result;
        }

        LogEscalationStarted(_logger, context.RequestId, _remoteGuards.Count);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(_options.EscalationTimeoutMs);

            return await ExecuteRemoteInputGuardsAsync(context, l2Result, stopwatch, cts.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            // Escalation timeout (not user cancellation)
            LogEscalationTimeout(_logger, context.RequestId, _options.EscalationTimeoutMs);
            return await _hooks.OnEscalationTimeoutAsync(context, l2Result);
        }
    }

    private async Task<GuardResult> ExecuteOutputEscalationAsync(
        GuardContext context,
        string output,
        GuardResult l2Result,
        Stopwatch stopwatch)
    {
        if (!await _hooks.OnBeforeEscalationAsync(context, l2Result))
        {
            LogEscalationSkippedByHook(_logger, context.RequestId);
            return l2Result;
        }

        LogEscalationStarted(_logger, context.RequestId, _remoteGuards.Count);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(_options.EscalationTimeoutMs);

            return await ExecuteRemoteOutputGuardsAsync(context, output, l2Result, stopwatch, cts.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            LogEscalationTimeout(_logger, context.RequestId, _options.EscalationTimeoutMs);
            return await _hooks.OnEscalationTimeoutAsync(context, l2Result);
        }
    }

    private async Task<GuardResult> ExecuteRemoteInputGuardsAsync(
        GuardContext context,
        GuardResult l2Result,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var l3TriggeredGuards = new List<TriggeredGuard>(l2Result.TriggeredGuards);
        var maxScore = l2Result.Score;
        var maxSeverity = l2Result.MaxSeverity;
        string? blockReason = null;

        foreach (var guard in _remoteGuards)
        {
            try
            {
                var result = await guard.CheckInputAsync(context, l2Result, cancellationToken);

                l3TriggeredGuards.Add(new TriggeredGuard
                {
                    GuardName = guard.Name,
                    Layer = guard.Layer,
                    Confidence = result.Score,
                    Severity = result.Severity,
                    Details = result.Reasoning
                });

                if (result.Score > maxScore) maxScore = result.Score;
                if (result.Severity > maxSeverity) maxSeverity = result.Severity;

                if (!result.Passed)
                {
                    blockReason = $"{guard.Name}: {result.Reasoning ?? "Blocked by L3 judge"}";
                    break;
                }

                LogRemoteGuardCompleted(_logger, context.RequestId, guard.Name,
                    result.Passed, result.Score);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogRemoteGuardFailed(_logger, ex, guard.Name, context.RequestId);

                if (_options.FailMode == FailMode.Closed)
                {
                    blockReason = $"L3 guard error: {guard.Name}";
                    break;
                }
            }
        }

        return MergeL3Result(context.RequestId, maxScore, maxSeverity,
            l3TriggeredGuards, blockReason, stopwatch.Elapsed.TotalMilliseconds);
    }

    private async Task<GuardResult> ExecuteRemoteOutputGuardsAsync(
        GuardContext context,
        string output,
        GuardResult l2Result,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var l3TriggeredGuards = new List<TriggeredGuard>(l2Result.TriggeredGuards);
        var maxScore = l2Result.Score;
        var maxSeverity = l2Result.MaxSeverity;
        string? blockReason = null;

        foreach (var guard in _remoteGuards)
        {
            try
            {
                var result = await guard.CheckOutputAsync(context, output, l2Result, cancellationToken);

                l3TriggeredGuards.Add(new TriggeredGuard
                {
                    GuardName = guard.Name,
                    Layer = guard.Layer,
                    Confidence = result.Score,
                    Severity = result.Severity,
                    Details = result.Reasoning
                });

                if (result.Score > maxScore) maxScore = result.Score;
                if (result.Severity > maxSeverity) maxSeverity = result.Severity;

                if (!result.Passed)
                {
                    blockReason = $"{guard.Name}: {result.Reasoning ?? "Blocked by L3 judge"}";
                    break;
                }

                LogRemoteGuardCompleted(_logger, context.RequestId, guard.Name,
                    result.Passed, result.Score);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogRemoteGuardFailed(_logger, ex, guard.Name, context.RequestId);

                if (_options.FailMode == FailMode.Closed)
                {
                    blockReason = $"L3 guard error: {guard.Name}";
                    break;
                }
            }
        }

        return MergeL3Result(context.RequestId, maxScore, maxSeverity,
            l3TriggeredGuards, blockReason, stopwatch.Elapsed.TotalMilliseconds);
    }

    private GuardResult MergeL3Result(
        string requestId,
        double maxScore,
        Severity maxSeverity,
        List<TriggeredGuard> triggeredGuards,
        string? blockReason,
        double latencyMs)
    {
        if (blockReason is not null)
        {
            return GuardResult.Block(requestId, blockReason, maxScore, maxSeverity,
                triggeredGuards, latencyMs);
        }

        if (maxScore >= _options.BlockThreshold)
        {
            return GuardResult.Block(requestId,
                triggeredGuards.LastOrDefault()?.Details ?? "L3 threshold exceeded",
                maxScore, maxSeverity, triggeredGuards, latencyMs);
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
            case GuardDecision.NeedsEscalation:
                // NeedsEscalation without remote guards — no specific hook
                break;
            case GuardDecision.Pass:
                await _hooks.OnPassedAsync(context, result);
                break;
        }
    }

    [LoggerMessage(LogLevel.Debug, "Check skipped by OnBeforeCheck hook for request {RequestId}")]
    private static partial void LogCheckSkippedByHook(ILogger logger, string requestId);

    [LoggerMessage(LogLevel.Error, "Guard {GuardName} failed in Closed mode, blocking request {RequestId}")]
    private static partial void LogGuardFailedClosedMode(ILogger logger, Exception ex, string guardName, string requestId);

    [LoggerMessage(LogLevel.Warning, "Guard {GuardName} failed in Open mode for request {RequestId}, continuing")]
    private static partial void LogGuardFailedOpenMode(ILogger logger, Exception ex, string guardName, string requestId);

    [LoggerMessage(LogLevel.Debug, "Check cancelled for request {RequestId}")]
    private static partial void LogCheckCancelled(ILogger logger, string requestId);

    [LoggerMessage(LogLevel.Error, "Unexpected error during input check for request {RequestId}")]
    private static partial void LogUnexpectedInputCheckError(ILogger logger, Exception ex, string requestId);

    [LoggerMessage(LogLevel.Warning, "Output guard {GuardName} failed for request {RequestId}")]
    private static partial void LogOutputGuardFailed(ILogger logger, Exception ex, string guardName, string requestId);

    [LoggerMessage(LogLevel.Error, "Unexpected error during output check for request {RequestId}")]
    private static partial void LogUnexpectedOutputCheckError(ILogger logger, Exception ex, string requestId);

    [LoggerMessage(LogLevel.Debug, "L3 escalation skipped by hook for request {RequestId}")]
    private static partial void LogEscalationSkippedByHook(ILogger logger, string requestId);

    [LoggerMessage(LogLevel.Information, "L3 escalation started for request {RequestId}, {GuardCount} remote guard(s)")]
    private static partial void LogEscalationStarted(ILogger logger, string requestId, int guardCount);

    [LoggerMessage(LogLevel.Warning, "L3 escalation timed out for request {RequestId} after {TimeoutMs}ms")]
    private static partial void LogEscalationTimeout(ILogger logger, string requestId, int timeoutMs);

    [LoggerMessage(LogLevel.Debug, "L3 remote guard {GuardName} completed for request {RequestId}: Passed={Passed}, Score={Score}")]
    private static partial void LogRemoteGuardCompleted(ILogger logger, string requestId, string guardName, bool passed, double score);

    [LoggerMessage(LogLevel.Warning, "L3 remote guard {GuardName} failed for request {RequestId}")]
    private static partial void LogRemoteGuardFailed(ILogger logger, Exception ex, string guardName, string requestId);
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
