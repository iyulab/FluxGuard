using FluentAssertions;
using FluxGuard.Core;
using FluxGuard.Monitoring;
using Xunit;

namespace FluxGuard.Tests.Monitoring;

public class InMemoryStatsCollectorTests
{
    private readonly InMemoryStatsCollector _collector = new();

    [Fact]
    public void RecordCheck_Pass_IncrementsPassedCount()
    {
        // Arrange
        var result = GuardResult.Pass("test", 10.0);

        // Act
        _collector.RecordCheck(result, isInput: true);
        var stats = _collector.GetStats();

        // Assert
        stats.TotalChecks.Should().Be(1);
        stats.PassedCount.Should().Be(1);
        stats.InputChecks.Should().Be(1);
    }

    [Fact]
    public void RecordCheck_Block_IncrementsBlockedCount()
    {
        // Arrange
        var result = GuardResult.Block("test", "blocked", 0.9, Severity.High, [], 15.0);

        // Act
        _collector.RecordCheck(result, isInput: false);
        var stats = _collector.GetStats();

        // Assert
        stats.TotalChecks.Should().Be(1);
        stats.BlockedCount.Should().Be(1);
        stats.OutputChecks.Should().Be(1);
    }

    [Fact]
    public void RecordCheck_Flag_IncrementsFlaggedCount()
    {
        // Arrange
        var result = GuardResult.Flag("test", 0.7, Severity.Medium, [], 12.0);

        // Act
        _collector.RecordCheck(result, isInput: true);
        var stats = _collector.GetStats();

        // Assert
        stats.FlaggedCount.Should().Be(1);
    }

    [Fact]
    public void RecordGuardExecution_TracksPerGuardStats()
    {
        // Arrange & Act
        _collector.RecordGuardExecution("L1PIIGuard", "L1", 5.0, triggered: true);
        _collector.RecordGuardExecution("L1PIIGuard", "L1", 3.0, triggered: false);
        _collector.RecordGuardExecution("L2MLGuard", "L2", 20.0, triggered: false);

        var stats = _collector.GetStats();

        // Assert
        stats.ByGuard.Should().HaveCount(2);
        stats.ByGuard["L1:L1PIIGuard"].TotalChecks.Should().Be(2);
        stats.ByGuard["L1:L1PIIGuard"].TriggeredCount.Should().Be(1);
    }

    [Fact]
    public void RecordGuardError_IncrementsErrorCount()
    {
        // Act
        _collector.RecordGuardError("L2MLGuard", "L2");
        _collector.RecordGuardError("L2MLGuard", "L2");

        var stats = _collector.GetStats();

        // Assert
        stats.ErrorCount.Should().Be(2);
        stats.ByGuard["L2:L2MLGuard"].ErrorCount.Should().Be(2);
    }

    [Fact]
    public void GetStats_CalculatesRates()
    {
        // Arrange
        _collector.RecordCheck(GuardResult.Pass("t1", 1), true);
        _collector.RecordCheck(GuardResult.Pass("t2", 1), true);
        _collector.RecordCheck(GuardResult.Block("t3", "r", 0.9, Severity.High, [], 1), true);
        _collector.RecordCheck(GuardResult.Pass("t4", 1), true);

        // Act
        var stats = _collector.GetStats();

        // Assert
        stats.PassRate.Should().Be(0.75);
        stats.BlockRate.Should().Be(0.25);
    }

    [Fact]
    public void GetStats_CalculatesAverageLatency()
    {
        // Arrange
        _collector.RecordCheck(GuardResult.Pass("t1", 10.0), true);
        _collector.RecordCheck(GuardResult.Pass("t2", 20.0), true);
        _collector.RecordCheck(GuardResult.Pass("t3", 30.0), true);

        // Act
        var stats = _collector.GetStats();

        // Assert
        stats.AverageLatencyMs.Should().Be(20.0);
    }

    [Fact]
    public void Reset_ClearsAllStats()
    {
        // Arrange
        _collector.RecordCheck(GuardResult.Pass("t1", 10.0), true);
        _collector.RecordGuardExecution("G1", "L1", 5.0, true);
        _collector.RecordGuardError("G1", "L1");

        // Act
        _collector.Reset();
        var stats = _collector.GetStats();

        // Assert
        stats.TotalChecks.Should().Be(0);
        stats.PassedCount.Should().Be(0);
        stats.ErrorCount.Should().Be(0);
        stats.ByGuard.Should().BeEmpty();
    }

    [Fact]
    public void GetStats_Empty_ReturnsValidStats()
    {
        // Act
        var stats = _collector.GetStats();

        // Assert
        stats.TotalChecks.Should().Be(0);
        stats.PassRate.Should().Be(1.0); // No checks means 100% pass
        stats.BlockRate.Should().Be(0.0);
    }
}
