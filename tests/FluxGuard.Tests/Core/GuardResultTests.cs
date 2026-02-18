using FluxGuard.Core;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.Core;

public class GuardResultTests
{
    #region Pass

    [Fact]
    public void Pass_ReturnsCorrectDefaults()
    {
        var result = GuardResult.Pass("req-1", 1.5);

        result.Decision.Should().Be(GuardDecision.Pass);
        result.IsBlocked.Should().BeFalse();
        result.IsFlagged.Should().BeFalse();
        result.NeedsEscalation.Should().BeFalse();
        result.Score.Should().Be(0.0);
        result.RequestId.Should().Be("req-1");
        result.LatencyMs.Should().Be(1.5);
        result.BlockReason.Should().BeNull();
        result.TriggeredGuards.Should().BeEmpty();
        result.MaxSeverity.Should().Be(Severity.Info);
    }

    #endregion

    #region Block

    [Fact]
    public void Block_SetsAllProperties()
    {
        var triggers = new List<TriggeredGuard>
        {
            new()
            {
                GuardName = "TestGuard",
                Layer = "L1",
                Pattern = "pattern-1",
                MatchedText = "matched",
                Confidence = 0.95,
                Severity = Severity.Critical,
                Details = "detail"
            }
        };

        var result = GuardResult.Block("req-2", "Blocked for testing", 0.95, Severity.Critical, triggers, 2.3);

        result.Decision.Should().Be(GuardDecision.Blocked);
        result.IsBlocked.Should().BeTrue();
        result.IsFlagged.Should().BeFalse();
        result.NeedsEscalation.Should().BeFalse();
        result.Score.Should().Be(0.95);
        result.MaxSeverity.Should().Be(Severity.Critical);
        result.BlockReason.Should().Be("Blocked for testing");
        result.TriggeredGuards.Should().HaveCount(1);
        result.RequestId.Should().Be("req-2");
        result.LatencyMs.Should().Be(2.3);
    }

    #endregion

    #region Flag

    [Fact]
    public void Flag_SetsCorrectDecision()
    {
        var triggers = new List<TriggeredGuard>
        {
            new() { GuardName = "TestGuard", Layer = "L1", Severity = Severity.Medium }
        };

        var result = GuardResult.Flag("req-3", 0.75, Severity.Medium, triggers, 1.0);

        result.Decision.Should().Be(GuardDecision.Flagged);
        result.IsBlocked.Should().BeFalse();
        result.IsFlagged.Should().BeTrue();
        result.NeedsEscalation.Should().BeFalse();
        result.Score.Should().Be(0.75);
        result.MaxSeverity.Should().Be(Severity.Medium);
        result.BlockReason.Should().BeNull();
        result.TriggeredGuards.Should().HaveCount(1);
    }

    #endregion

    #region Escalate

    [Fact]
    public void Escalate_SetsCorrectDecision()
    {
        var triggers = new List<TriggeredGuard>
        {
            new() { GuardName = "TestGuard", Layer = "L1" }
        };

        var result = GuardResult.Escalate("req-4", 0.6, triggers, 3.0);

        result.Decision.Should().Be(GuardDecision.NeedsEscalation);
        result.IsBlocked.Should().BeFalse();
        result.IsFlagged.Should().BeFalse();
        result.NeedsEscalation.Should().BeTrue();
        result.Score.Should().Be(0.6);
        result.TriggeredGuards.Should().HaveCount(1);
    }

    #endregion

    #region TriggeredGuard

    [Fact]
    public void TriggeredGuard_AllProperties()
    {
        var tg = new TriggeredGuard
        {
            GuardName = "PIIGuard",
            Layer = "L1",
            Pattern = "SSN",
            MatchedText = "123-45-6789",
            Confidence = 0.99,
            Severity = Severity.Critical,
            Details = "SSN detected"
        };

        tg.GuardName.Should().Be("PIIGuard");
        tg.Layer.Should().Be("L1");
        tg.Pattern.Should().Be("SSN");
        tg.MatchedText.Should().Be("123-45-6789");
        tg.Confidence.Should().Be(0.99);
        tg.Severity.Should().Be(Severity.Critical);
        tg.Details.Should().Be("SSN detected");
    }

    [Fact]
    public void TriggeredGuard_Defaults_OptionalFieldsAreNull()
    {
        var tg = new TriggeredGuard { GuardName = "TestGuard", Layer = "L1" };

        tg.Pattern.Should().BeNull();
        tg.MatchedText.Should().BeNull();
        tg.Confidence.Should().Be(0.0);
        tg.Severity.Should().Be(Severity.None);
        tg.Details.Should().BeNull();
    }

    [Fact]
    public void TriggeredGuard_RecordEquality()
    {
        var tg1 = new TriggeredGuard { GuardName = "G1", Layer = "L1", Confidence = 0.5 };
        var tg2 = new TriggeredGuard { GuardName = "G1", Layer = "L1", Confidence = 0.5 };

        tg1.Should().Be(tg2);
    }

    #endregion

    #region GuardCheckResult

    [Fact]
    public void GuardCheckResult_Safe_IsCorrect()
    {
        var safe = Abstractions.GuardCheckResult.Safe;

        safe.Passed.Should().BeTrue();
        safe.Score.Should().Be(0.0);
        safe.Severity.Should().Be(Severity.None);
        safe.GuardName.Should().BeNull();
        safe.NeedsEscalation.Should().BeFalse();
    }

    [Fact]
    public void GuardCheckResult_Pass_WithGuardName()
    {
        var result = Abstractions.GuardCheckResult.Pass("TestGuard", "All clear");

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.0);
        result.GuardName.Should().Be("TestGuard");
        result.Message.Should().Be("All clear");
    }

    [Fact]
    public void GuardCheckResult_Pass_MessageDefaultsToNull()
    {
        var result = Abstractions.GuardCheckResult.Pass("TestGuard");

        result.Message.Should().BeNull();
    }

    [Fact]
    public void GuardCheckResult_Block_SetsAllProperties()
    {
        var result = Abstractions.GuardCheckResult.Block(
            score: 0.95,
            severity: Severity.High,
            pattern: "injection-pattern",
            matchedText: "DROP TABLE",
            details: "SQL injection detected");

        result.Passed.Should().BeFalse();
        result.Score.Should().Be(0.95);
        result.Severity.Should().Be(Severity.High);
        result.Pattern.Should().Be("injection-pattern");
        result.MatchedText.Should().Be("DROP TABLE");
        result.Details.Should().Be("SQL injection detected");
        result.NeedsEscalation.Should().BeFalse();
    }

    [Fact]
    public void GuardCheckResult_Block_OptionalParametersDefaultToNull()
    {
        var result = Abstractions.GuardCheckResult.Block(0.9, Severity.Critical);

        result.Pattern.Should().BeNull();
        result.MatchedText.Should().BeNull();
        result.Details.Should().BeNull();
    }

    [Fact]
    public void GuardCheckResult_Escalate_SetsProperties()
    {
        var result = Abstractions.GuardCheckResult.Escalate(0.6, "ambiguous-pattern", "needs review");

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.6);
        result.NeedsEscalation.Should().BeTrue();
        result.Pattern.Should().Be("ambiguous-pattern");
        result.Details.Should().Be("needs review");
    }

    [Fact]
    public void GuardCheckResult_Escalate_OptionalParametersDefaultToNull()
    {
        var result = Abstractions.GuardCheckResult.Escalate(0.5);

        result.Pattern.Should().BeNull();
        result.Details.Should().BeNull();
    }

    #endregion

    #region Enums

    [Theory]
    [InlineData(GuardDecision.Pass, 0)]
    [InlineData(GuardDecision.Flagged, 1)]
    [InlineData(GuardDecision.NeedsEscalation, 2)]
    [InlineData(GuardDecision.Blocked, 3)]
    public void GuardDecision_HasExpectedValues(GuardDecision decision, int expected)
    {
        ((int)decision).Should().Be(expected);
    }

    [Theory]
    [InlineData(Severity.None, 0)]
    [InlineData(Severity.Info, 1)]
    [InlineData(Severity.Low, 2)]
    [InlineData(Severity.Medium, 3)]
    [InlineData(Severity.High, 4)]
    [InlineData(Severity.Critical, 5)]
    public void Severity_HasExpectedValues(Severity severity, int expected)
    {
        ((int)severity).Should().Be(expected);
    }

    [Theory]
    [InlineData(FailMode.Open, 0)]
    [InlineData(FailMode.Closed, 1)]
    public void FailMode_HasExpectedValues(FailMode mode, int expected)
    {
        ((int)mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(GuardPreset.Minimal, 0)]
    [InlineData(GuardPreset.Standard, 1)]
    [InlineData(GuardPreset.Strict, 2)]
    public void GuardPreset_HasExpectedValues(GuardPreset preset, int expected)
    {
        ((int)preset).Should().Be(expected);
    }

    #endregion
}
