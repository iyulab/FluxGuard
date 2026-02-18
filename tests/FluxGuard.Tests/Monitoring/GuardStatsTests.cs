using FluentAssertions;
using FluxGuard.Monitoring;
using Xunit;

namespace FluxGuard.Tests.Monitoring;

public class GuardStatsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var stats = new GuardStats();

        stats.TotalChecks.Should().Be(0);
        stats.PassedCount.Should().Be(0);
        stats.BlockedCount.Should().Be(0);
        stats.FlaggedCount.Should().Be(0);
        stats.EscalatedCount.Should().Be(0);
        stats.ErrorCount.Should().Be(0);
        stats.InputChecks.Should().Be(0);
        stats.OutputChecks.Should().Be(0);
        stats.AverageLatencyMs.Should().Be(0);
        stats.P95LatencyMs.Should().Be(0);
        stats.P99LatencyMs.Should().Be(0);
        stats.ByGuard.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 0, 1.0)]
    [InlineData(100, 80, 0.8)]
    [InlineData(100, 100, 1.0)]
    [InlineData(100, 0, 0.0)]
    [InlineData(10, 7, 0.7)]
    public void PassRate_ShouldComputeCorrectly(long total, long passed, double expected)
    {
        var stats = new GuardStats { TotalChecks = total, PassedCount = passed };
        stats.PassRate.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(100, 20, 0.2)]
    [InlineData(100, 0, 0.0)]
    [InlineData(100, 100, 1.0)]
    [InlineData(10, 3, 0.3)]
    public void BlockRate_ShouldComputeCorrectly(long total, long blocked, double expected)
    {
        var stats = new GuardStats { TotalChecks = total, BlockedCount = blocked };
        stats.BlockRate.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void Duration_ShouldBePositive()
    {
        var stats = new GuardStats();
        stats.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Empty_ShouldReturnDefaultStats()
    {
        var stats = GuardStats.Empty;

        stats.TotalChecks.Should().Be(0);
        stats.PassRate.Should().Be(1.0);
        stats.BlockRate.Should().Be(0.0);
    }

    [Fact]
    public void ShouldInitialize_WithByGuard()
    {
        var byGuard = new Dictionary<string, GuardLayerStats>
        {
            ["PIIGuard"] = new GuardLayerStats
            {
                GuardName = "PIIGuard",
                Layer = "L1",
                TotalChecks = 50,
                TriggeredCount = 5
            }
        };
        var stats = new GuardStats
        {
            TotalChecks = 100,
            PassedCount = 80,
            BlockedCount = 10,
            FlaggedCount = 10,
            ByGuard = byGuard
        };

        stats.ByGuard.Should().ContainKey("PIIGuard");
    }
}

public class GuardLayerStatsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var stats = new GuardLayerStats
        {
            GuardName = "TestGuard",
            Layer = "L1"
        };

        stats.TotalChecks.Should().Be(0);
        stats.TriggeredCount.Should().Be(0);
        stats.AverageLatencyMs.Should().Be(0);
        stats.ErrorCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(100, 25, 0.25)]
    [InlineData(100, 0, 0.0)]
    [InlineData(100, 100, 1.0)]
    [InlineData(10, 3, 0.3)]
    public void TriggerRate_ShouldComputeCorrectly(long total, long triggered, double expected)
    {
        var stats = new GuardLayerStats
        {
            GuardName = "Guard",
            Layer = "L1",
            TotalChecks = total,
            TriggeredCount = triggered
        };
        stats.TriggerRate.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var stats = new GuardLayerStats
        {
            GuardName = "L2PromptInjection",
            Layer = "L2",
            TotalChecks = 1000,
            TriggeredCount = 15,
            AverageLatencyMs = 25.5,
            ErrorCount = 2
        };

        stats.GuardName.Should().Be("L2PromptInjection");
        stats.Layer.Should().Be("L2");
        stats.AverageLatencyMs.Should().Be(25.5);
    }
}
