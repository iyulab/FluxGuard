using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Presets;
using AwesomeAssertions;
using NSubstitute;

#pragma warning disable CA2012 // Use ValueTasks correctly — test mocking requires storing ValueTask from NSubstitute
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// A guard that passes a text reports how sure it is of what it found, not how risky the text is. Its score may flag a
/// check but must not block it through <c>BlockThreshold</c>; only a refusal or an escalation request blocks by score.
/// </summary>
public class PassedVerdictThresholdTests
{
    private static IInputGuard CreateInputGuard(GuardCheckResult result, string name = "Stub")
    {
        var guard = Substitute.For<IInputGuard>();
        guard.Name.Returns(name);
        guard.Layer.Returns("L1");
        guard.IsEnabled.Returns(true);
        guard.Order.Returns(0);
        guard.CheckAsync(Arg.Any<GuardContext>()).Returns(result);
        return guard;
    }

    private static GuardCheckResult PassedWithScore(double score) => new()
    {
        Passed = true,
        Score = score,
        Severity = Severity.Low,
        Pattern = "Tracked",
        Details = "Tracked, not refused"
    };

    [Theory]
    [InlineData("서버 192.168.0.10 에 접속이 안 돼")]
    [InlineData("ClusterPlex 5.0.5.6 베타에서 바뀐 점을 세 줄로 알려줘")]
    [InlineData("버전 1.2.3.4 릴리스 노트 요약")]
    [InlineData("Please reply to jane.doe@example.com with the summary")]
    public async Task StandardPreset_InputWithTrackedPii_IsFlaggedNotBlocked(string input)
    {
        var guard = FluxGuardBuilder.Create(b => b.ApplyStandardPreset()).Build();

        var result = await guard.CheckInputAsync(input, TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeFalse();
        result.Decision.Should().Be(GuardDecision.Flagged);
        result.TriggeredGuards.Should().Contain(t => t.GuardName.Contains("PII"));
    }

    [Fact]
    public async Task StandardPreset_InputWithCreditCard_IsStillBlocked()
    {
        var guard = FluxGuardBuilder.Create(b => b.ApplyStandardPreset()).Build();

        var result = await guard.CheckInputAsync("My card is 4111-1111-1111-1111", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task StandardPreset_OutputWithIpAddress_IsFlaggedNotBlocked()
    {
        var guard = FluxGuardBuilder.Create(b => b.ApplyStandardPreset()).Build();

        var result = await guard.CheckOutputAsync(
            "where is the server?", "The server is at 192.168.1.100", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeFalse();
        result.Decision.Should().Be(GuardDecision.Flagged);
    }

    [Fact]
    public async Task PassedResult_AboveBlockThreshold_FlagsInsteadOfBlocking()
    {
        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateInputGuard(PassedWithScore(0.99)))
            .Build();

        var result = await guard.CheckInputAsync("anything", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Flagged);
        result.Score.Should().Be(0.99);
    }

    [Fact]
    public async Task RefusedResult_AboveBlockThreshold_Blocks()
    {
        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateInputGuard(GuardCheckResult.Block(0.95, Severity.Medium, "Refused")))
            .Build();

        var result = await guard.CheckInputAsync("anything", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Blocked);
    }

    [Fact]
    public async Task EscalationRequest_AboveBlockThreshold_WithoutL3_StillBlocks()
    {
        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateInputGuard(GuardCheckResult.Escalate(0.95, "Suspicious")))
            .Build();

        var result = await guard.CheckInputAsync("anything", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Blocked);
    }

    [Fact]
    public async Task Escalation_RemotePasses_PassedLocalScoreAboveThreshold_DoesNotBlock()
    {
        var remote = Substitute.For<IRemoteGuard>();
        remote.Name.Returns("L3Judge");
        remote.Layer.Returns("L3");
        remote.IsEnabled.Returns(true);
        remote.Order.Returns(300);
        remote.CheckInputAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>())
            .Returns(new RemoteGuardResult { Passed = true, Score = 0.1, Reasoning = "safe", Severity = Severity.None });

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateInputGuard(PassedWithScore(0.95), "Tracker"))
            .AddInputGuard(CreateInputGuard(GuardCheckResult.Escalate(0.6, "ambiguous"), "Ambiguous"))
            .AddRemoteGuard(remote)
            .Configure(o =>
            {
                o.EnableL3Escalation = true;
                o.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("anything", TestContext.Current.CancellationToken);

        await remote.Received(1).CheckInputAsync(
            Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>());
        result.IsBlocked.Should().BeFalse();
        result.Decision.Should().Be(GuardDecision.Flagged);
    }
}
