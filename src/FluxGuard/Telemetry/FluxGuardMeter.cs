using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FluxGuard.Telemetry;

/// <summary>
/// OpenTelemetry meter for FluxGuard metrics
/// </summary>
public sealed class FluxGuardMeter : IDisposable
{
    /// <summary>
    /// Meter name
    /// </summary>
    public const string MeterName = "FluxGuard";

    private readonly Meter _meter;
    private readonly Counter<long> _checksTotal;
    private readonly Counter<long> _blockedTotal;
    private readonly Counter<long> _flaggedTotal;
    private readonly Counter<long> _errorsTotal;
    private readonly Histogram<double> _checkDuration;

    /// <summary>
    /// Create FluxGuard meter
    /// </summary>
    public FluxGuardMeter()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _checksTotal = _meter.CreateCounter<long>(
            "fluxguard.checks.total",
            unit: "{check}",
            description: "Total number of guard checks performed");

        _blockedTotal = _meter.CreateCounter<long>(
            "fluxguard.blocked.total",
            unit: "{request}",
            description: "Total number of blocked requests");

        _flaggedTotal = _meter.CreateCounter<long>(
            "fluxguard.flagged.total",
            unit: "{request}",
            description: "Total number of flagged requests");

        _errorsTotal = _meter.CreateCounter<long>(
            "fluxguard.errors.total",
            unit: "{error}",
            description: "Total number of guard errors");

        _checkDuration = _meter.CreateHistogram<double>(
            "fluxguard.check.duration",
            unit: "ms",
            description: "Duration of guard checks in milliseconds");
    }

    /// <summary>
    /// Record a check
    /// </summary>
    /// <param name="checkType">Type of check (input/output)</param>
    /// <param name="decision">Guard decision</param>
    /// <param name="layer">Guard layer that made the decision</param>
    public void RecordCheck(string checkType, string decision, string layer)
    {
        var tags = new TagList
        {
            { "check_type", checkType },
            { "decision", decision },
            { "layer", layer }
        };

        _checksTotal.Add(1, tags);

        if (decision == "blocked")
        {
            _blockedTotal.Add(1, tags);
        }
        else if (decision == "flagged")
        {
            _flaggedTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Record check duration
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds</param>
    /// <param name="checkType">Type of check</param>
    /// <param name="layer">Guard layer</param>
    public void RecordDuration(double durationMs, string checkType, string layer)
    {
        var tags = new TagList
        {
            { "check_type", checkType },
            { "layer", layer }
        };

        _checkDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Record an error
    /// </summary>
    /// <param name="guardName">Name of the guard that errored</param>
    /// <param name="layer">Guard layer</param>
    public void RecordError(string guardName, string layer)
    {
        var tags = new TagList
        {
            { "guard", guardName },
            { "layer", layer }
        };

        _errorsTotal.Add(1, tags);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}
