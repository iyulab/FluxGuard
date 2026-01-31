using FluxGuard.Core;
using FluxGuard.Monitoring;

namespace FluxGuard.Telemetry;

/// <summary>
/// FluxGuard metrics facade
/// Combines in-memory stats with OpenTelemetry metrics
/// </summary>
public sealed class FluxGuardMetrics : IGuardStatsCollector, IDisposable
{
    private readonly InMemoryStatsCollector _statsCollector = new();
    private readonly FluxGuardMeter _meter = new();

    /// <inheritdoc />
    public void RecordCheck(GuardResult result, bool isInput)
    {
        _statsCollector.RecordCheck(result, isInput);

        var checkType = isInput ? "input" : "output";
        var decision = result.Decision switch
        {
            GuardDecision.Pass => "passed",
            GuardDecision.Blocked => "blocked",
            GuardDecision.Flagged => "flagged",
            GuardDecision.NeedsEscalation => "escalated",
            _ => "unknown"
        };

        var layer = result.TriggeredGuards.Count > 0
            ? result.TriggeredGuards[0].Layer
            : "L1";

        _meter.RecordCheck(checkType, decision, layer);
        _meter.RecordDuration(result.LatencyMs, checkType, layer);
    }

    /// <inheritdoc />
    public void RecordGuardExecution(string guardName, string layer, double latencyMs, bool triggered)
    {
        _statsCollector.RecordGuardExecution(guardName, layer, latencyMs, triggered);
        _meter.RecordDuration(latencyMs, "guard", layer);
    }

    /// <inheritdoc />
    public void RecordGuardError(string guardName, string layer)
    {
        _statsCollector.RecordGuardError(guardName, layer);
        _meter.RecordError(guardName, layer);
    }

    /// <inheritdoc />
    public GuardStats GetStats() => _statsCollector.GetStats();

    /// <inheritdoc />
    public void Reset() => _statsCollector.Reset();

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}
