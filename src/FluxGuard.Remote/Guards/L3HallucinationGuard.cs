using System.Diagnostics;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.Hallucination;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxGuard.Remote.Guards;

/// <summary>
/// L3 Hallucination guard for output validation
/// Detects hallucinations in LLM outputs using groundedness verification
/// </summary>
public sealed class L3HallucinationGuard : IOutputGuard
{
    private readonly IHallucinationDetector _detector;
    private readonly RemoteGuardOptions _options;
    private readonly ILogger<L3HallucinationGuard> _logger;

    /// <summary>
    /// Metadata key for grounding context
    /// </summary>
    public const string GroundingContextKey = "GroundingContext";

    /// <inheritdoc />
    public string Name => "L3Hallucination";

    /// <inheritdoc />
    public string Layer => "L3";

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <inheritdoc />
    public int Order => 310;

    /// <summary>
    /// Create L3 Hallucination guard
    /// </summary>
    public L3HallucinationGuard(
        IHallucinationDetector detector,
        IOptions<RemoteGuardOptions> options,
        ILogger<L3HallucinationGuard> logger)
    {
        _detector = detector;
        _options = options.Value;
        _logger = logger;
        IsEnabled = _options.Enabled;
    }

    /// <inheritdoc />
    public async ValueTask<GuardCheckResult> CheckAsync(
        GuardContext context,
        string output)
    {
        var stopwatch = Stopwatch.StartNew();

        // Get grounding context from metadata
        string? groundingContext = null;
        if (context.Metadata.TryGetValue(GroundingContextKey, out var groundingObj))
        {
            groundingContext = groundingObj as string;
        }

        if (string.IsNullOrEmpty(groundingContext))
        {
            // No grounding context - skip hallucination check
            _logger.LogDebug(
                "No grounding context for hallucination check, skipping for request {RequestId}",
                context.RequestId);
            return GuardCheckResult.Pass(Name);
        }

        try
        {
            var result = await _detector.DetectAsync(
                context,
                output,
                groundingContext,
                context.CancellationToken);

            stopwatch.Stop();

            if (result.IsGrounded)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = true,
                    Score = result.HallucinationScore,
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            var severity = result.HallucinationScore switch
            {
                >= 0.9 => Severity.Critical,
                >= 0.7 => Severity.High,
                >= 0.5 => Severity.Medium,
                _ => Severity.Low
            };

            return new GuardCheckResult
            {
                GuardName = Name,
                Passed = result.HallucinationScore < 0.7,
                Score = result.HallucinationScore,
                Severity = severity,
                Pattern = result.Type.ToString(),
                Details = result.Reasoning,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Hallucination check failed for request {RequestId}",
                context.RequestId);

            // Fail open
            return GuardCheckResult.Pass(Name, "Hallucination check unavailable");
        }
    }
}
