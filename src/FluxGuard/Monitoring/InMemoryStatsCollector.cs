using System.Collections.Concurrent;
using FluxGuard.Core;

namespace FluxGuard.Monitoring;

/// <summary>
/// In-memory statistics collector
/// </summary>
public sealed class InMemoryStatsCollector : IGuardStatsCollector
{
    private long _totalChecks;
    private long _passedCount;
    private long _blockedCount;
    private long _flaggedCount;
    private long _escalatedCount;
    private long _errorCount;
    private long _inputChecks;
    private long _outputChecks;
    private double _totalLatencyMs;
    private readonly ConcurrentBag<double> _latencies = [];
    private readonly ConcurrentDictionary<string, GuardStats> _guardStats = new();
    private DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly object _latencyLock = new();

    /// <inheritdoc />
    public void RecordCheck(GuardResult result, bool isInput)
    {
        Interlocked.Increment(ref _totalChecks);

        if (isInput)
        {
            Interlocked.Increment(ref _inputChecks);
        }
        else
        {
            Interlocked.Increment(ref _outputChecks);
        }

        switch (result.Decision)
        {
            case GuardDecision.Pass:
                Interlocked.Increment(ref _passedCount);
                break;
            case GuardDecision.Blocked:
                Interlocked.Increment(ref _blockedCount);
                break;
            case GuardDecision.Flagged:
                Interlocked.Increment(ref _flaggedCount);
                break;
            case GuardDecision.NeedsEscalation:
                Interlocked.Increment(ref _escalatedCount);
                break;
        }

        // Record latency
        lock (_latencyLock)
        {
            _totalLatencyMs += result.LatencyMs;
            _latencies.Add(result.LatencyMs);
        }
    }

    /// <inheritdoc />
    public void RecordGuardExecution(string guardName, string layer, double latencyMs, bool triggered)
    {
        var key = $"{layer}:{guardName}";

        _guardStats.AddOrUpdate(
            key,
            _ => new GuardStats
            {
                TotalChecks = 1,
                PassedCount = triggered ? 0 : 1,
                BlockedCount = triggered ? 1 : 0,
                AverageLatencyMs = latencyMs,
                StartTime = DateTimeOffset.UtcNow
            },
            (_, existing) => existing with
            {
                TotalChecks = existing.TotalChecks + 1,
                PassedCount = existing.PassedCount + (triggered ? 0 : 1),
                BlockedCount = existing.BlockedCount + (triggered ? 1 : 0),
                AverageLatencyMs = (existing.AverageLatencyMs * existing.TotalChecks + latencyMs) /
                                   (existing.TotalChecks + 1)
            });
    }

    /// <inheritdoc />
    public void RecordGuardError(string guardName, string layer)
    {
        Interlocked.Increment(ref _errorCount);

        var key = $"{layer}:{guardName}";
        _guardStats.AddOrUpdate(
            key,
            _ => new GuardStats { ErrorCount = 1, StartTime = DateTimeOffset.UtcNow },
            (_, existing) => existing with { ErrorCount = existing.ErrorCount + 1 });
    }

    /// <inheritdoc />
    public GuardStats GetStats()
    {
        var totalChecks = Interlocked.Read(ref _totalChecks);
        var (p95, p99) = CalculatePercentiles();

        var byGuard = _guardStats.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var parts = kvp.Key.Split(':');
                return new GuardLayerStats
                {
                    GuardName = parts.Length > 1 ? parts[1] : kvp.Key,
                    Layer = parts.Length > 0 ? parts[0] : "L1",
                    TotalChecks = kvp.Value.TotalChecks,
                    TriggeredCount = kvp.Value.BlockedCount,
                    AverageLatencyMs = kvp.Value.AverageLatencyMs,
                    ErrorCount = kvp.Value.ErrorCount
                };
            });

        return new GuardStats
        {
            TotalChecks = totalChecks,
            PassedCount = Interlocked.Read(ref _passedCount),
            BlockedCount = Interlocked.Read(ref _blockedCount),
            FlaggedCount = Interlocked.Read(ref _flaggedCount),
            EscalatedCount = Interlocked.Read(ref _escalatedCount),
            ErrorCount = Interlocked.Read(ref _errorCount),
            InputChecks = Interlocked.Read(ref _inputChecks),
            OutputChecks = Interlocked.Read(ref _outputChecks),
            AverageLatencyMs = totalChecks > 0 ? _totalLatencyMs / totalChecks : 0,
            P95LatencyMs = p95,
            P99LatencyMs = p99,
            ByGuard = byGuard,
            StartTime = _startTime
        };
    }

    /// <inheritdoc />
    public void Reset()
    {
        Interlocked.Exchange(ref _totalChecks, 0);
        Interlocked.Exchange(ref _passedCount, 0);
        Interlocked.Exchange(ref _blockedCount, 0);
        Interlocked.Exchange(ref _flaggedCount, 0);
        Interlocked.Exchange(ref _escalatedCount, 0);
        Interlocked.Exchange(ref _errorCount, 0);
        Interlocked.Exchange(ref _inputChecks, 0);
        Interlocked.Exchange(ref _outputChecks, 0);

        lock (_latencyLock)
        {
            _totalLatencyMs = 0;
            _latencies.Clear();
        }

        _guardStats.Clear();
        _startTime = DateTimeOffset.UtcNow;
    }

    private (double p95, double p99) CalculatePercentiles()
    {
        double[] sorted;
        lock (_latencyLock)
        {
            if (_latencies.IsEmpty)
            {
                return (0, 0);
            }
            sorted = [.. _latencies.OrderBy(x => x)];
        }

        var p95Index = (int)(sorted.Length * 0.95);
        var p99Index = (int)(sorted.Length * 0.99);

        return (
            sorted[Math.Min(p95Index, sorted.Length - 1)],
            sorted[Math.Min(p99Index, sorted.Length - 1)]
        );
    }
}
