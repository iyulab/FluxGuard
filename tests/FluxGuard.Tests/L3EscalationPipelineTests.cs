using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Hooks;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

#pragma warning disable CA2012 // Use ValueTasks correctly — test mocking requires storing ValueTask from NSubstitute
using Xunit;

namespace FluxGuard.Tests;

public class L3EscalationPipelineTests
{
    private static IInputGuard CreateEscalateGuard(double score = 0.6)
    {
        var guard = Substitute.For<IInputGuard>();
        guard.Name.Returns("L2Ambiguous");
        guard.Layer.Returns("L2");
        guard.IsEnabled.Returns(true);
        guard.Order.Returns(0);
        guard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(GuardCheckResult.Escalate(score, "ambiguous", "uncertain content"));
        return guard;
    }

    private static IRemoteGuard CreateRemoteGuard(bool passed = true, double score = 0.1)
    {
        var guard = Substitute.For<IRemoteGuard>();
        guard.Name.Returns("L3Judge");
        guard.Layer.Returns("L3");
        guard.IsEnabled.Returns(true);
        guard.Order.Returns(300);
        guard.CheckInputAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>())
            .Returns(new RemoteGuardResult
            {
                Passed = passed,
                Score = score,
                Reasoning = passed ? "Content is safe" : "Content is unsafe",
                Severity = passed ? Severity.None : Severity.High
            });
        guard.CheckOutputAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>())
            .Returns(new RemoteGuardResult
            {
                Passed = passed,
                Score = score,
                Reasoning = passed ? "Output is safe" : "Output is unsafe",
                Severity = passed ? Severity.None : Severity.High
            });
        return guard;
    }

    #region Input Escalation

    [Fact]
    public async Task CheckInputAsync_WithRemoteGuard_PassResult_ReturnsPassed()
    {
        var remoteGuard = CreateRemoteGuard(passed: true, score: 0.1);

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test input");

        result.Decision.Should().Be(GuardDecision.Pass);
        result.IsBlocked.Should().BeFalse();
        await remoteGuard.Received(1).CheckInputAsync(
            Arg.Any<GuardContext>(),
            Arg.Is<GuardResult>(r => r.NeedsEscalation),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckInputAsync_WithRemoteGuard_BlockResult_ReturnsBlocked()
    {
        var remoteGuard = CreateRemoteGuard(passed: false, score: 0.95);

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test input");

        result.Decision.Should().Be(GuardDecision.Blocked);
        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain("L3Judge");
    }

    [Fact]
    public async Task CheckInputAsync_NoRemoteGuards_ReturnsEscalation()
    {
        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.NeedsEscalation);
    }

    [Fact]
    public async Task CheckInputAsync_RemoteGuardDisabled_NotCalled()
    {
        var remoteGuard = Substitute.For<IRemoteGuard>();
        remoteGuard.IsEnabled.Returns(false);
        remoteGuard.Order.Returns(300);

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.NeedsEscalation);
        await remoteGuard.DidNotReceive().CheckInputAsync(
            Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckInputAsync_WithRemoteGuard_FlagScore_ReturnsFlagged()
    {
        // L3 returns a score between flag and block thresholds
        var remoteGuard = CreateRemoteGuard(passed: true, score: 0.75);

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
                opts.FlagThreshold = 0.7;
                opts.BlockThreshold = 0.9;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Flagged);
    }

    #endregion

    #region Output Escalation

    [Fact]
    public async Task CheckOutputAsync_WithRemoteGuard_PassResult_ReturnsPassed()
    {
        var outputGuard = Substitute.For<IOutputGuard>();
        outputGuard.Name.Returns("L2OutputCheck");
        outputGuard.Layer.Returns("L2");
        outputGuard.IsEnabled.Returns(true);
        outputGuard.Order.Returns(0);
        outputGuard.CheckAsync(Arg.Any<GuardContext>(), Arg.Any<string>())
            .Returns(GuardCheckResult.Escalate(0.6, "ambiguous output"));

        var remoteGuard = CreateRemoteGuard(passed: true, score: 0.1);

        var guard = FluxGuardBuilder.Create()
            .AddOutputGuard(outputGuard)
            .AddRemoteGuard(remoteGuard)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckOutputAsync("input", "test output");

        result.Decision.Should().Be(GuardDecision.Pass);
        await remoteGuard.Received(1).CheckOutputAsync(
            Arg.Any<GuardContext>(), Arg.Is("test output"),
            Arg.Any<GuardResult>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Hooks

    [Fact]
    public async Task OnBeforeEscalation_ReturnsFalse_SkipsEscalation()
    {
        var remoteGuard = CreateRemoteGuard(passed: true);
        var hooks = Substitute.For<IFluxGuardHooks>();
        hooks.OnBeforeCheckAsync(Arg.Any<GuardContext>()).Returns(true);
        hooks.OnBeforeEscalationAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>())
            .Returns(false);
        hooks.OnCustomDecisionAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>())
            .Returns(ValueTask.FromResult<FailDecision?>(null));
        hooks.OnGuardErrorAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<Exception>())
            .Returns(FailDecision.Continue);

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .WithHooks(hooks)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.NeedsEscalation);
        await remoteGuard.DidNotReceive().CheckInputAsync(
            Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnEscalationTimeout_CalledWhenTimeout()
    {
        var remoteGuard = Substitute.For<IRemoteGuard>();
        remoteGuard.Name.Returns("SlowGuard");
        remoteGuard.Layer.Returns("L3");
        remoteGuard.IsEnabled.Returns(true);
        remoteGuard.Order.Returns(300);
        remoteGuard.CheckInputAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                var tcs = new TaskCompletionSource<RemoteGuardResult>();
                ct.Register(() => tcs.TrySetCanceled(ct));
                return new ValueTask<RemoteGuardResult>(tcs.Task);
            });

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
                opts.EscalationTimeoutMs = 100;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        // OnEscalationTimeoutAsync default returns the local result
        result.Decision.Should().Be(GuardDecision.NeedsEscalation);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task RemoteGuard_ThrowsException_FailModeOpen_ReturnsPassed()
    {
        var remoteGuard = Substitute.For<IRemoteGuard>();
        remoteGuard.Name.Returns("FailingGuard");
        remoteGuard.Layer.Returns("L3");
        remoteGuard.IsEnabled.Returns(true);
        remoteGuard.Order.Returns(300);
        remoteGuard.CheckInputAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Service unavailable"));

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .WithFailMode(FailMode.Open)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    [Fact]
    public async Task RemoteGuard_ThrowsException_FailModeClosed_ReturnsBlocked()
    {
        var remoteGuard = Substitute.For<IRemoteGuard>();
        remoteGuard.Name.Returns("FailingGuard");
        remoteGuard.Layer.Returns("L3");
        remoteGuard.IsEnabled.Returns(true);
        remoteGuard.Order.Returns(300);
        remoteGuard.CheckInputAsync(Arg.Any<GuardContext>(), Arg.Any<GuardResult>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Service unavailable"));

        var guard = FluxGuardBuilder.Create()
            .AddInputGuard(CreateEscalateGuard())
            .AddRemoteGuard(remoteGuard)
            .WithFailMode(FailMode.Closed)
            .Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            })
            .Build();

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Blocked);
        result.BlockReason.Should().Contain("L3 guard error");
    }

    #endregion
}
