using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Hooks;
using FluxGuard.Presets;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

#pragma warning disable CA2012 // Use ValueTasks correctly — test mocking requires storing ValueTask from .Throws()
using Xunit;

namespace FluxGuard.Tests;

public class FluxGuardCoreTests
{
    #region Input Pipeline — Basic

    [Fact]
    public async Task CheckInputAsync_SafeInput_ReturnsPass()
    {
        var guard = FluxGuard.Create();

        var result = await guard.CheckInputAsync("Hello, world!");

        result.Decision.Should().Be(GuardDecision.Pass);
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task CheckInputAsync_WithPIIGuard_DetectsSSN()
    {
        var guard = FluxGuard.Create(builder =>
            builder.ApplyStandardPreset());

        var result = await guard.CheckInputAsync("My SSN is 123-45-6789");

        result.Score.Should().BeGreaterThan(0);
        result.TriggeredGuards.Should().NotBeEmpty();
    }

    [Fact]
    public void CheckInput_Synchronous_Works()
    {
        var guard = FluxGuard.Create();

        var result = guard.CheckInput("Hello, world!");

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    #endregion

    #region Output Pipeline — Basic

    [Fact]
    public async Task CheckOutputAsync_SafeOutput_ReturnsPass()
    {
        var guard = FluxGuard.Create();

        var result = await guard.CheckOutputAsync("Tell me about weather", "The weather is sunny today.");

        result.Decision.Should().Be(GuardDecision.Pass);
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void CheckOutput_Synchronous_Works()
    {
        var guard = FluxGuard.Create();

        var result = guard.CheckOutput("question", "safe answer");

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    [Fact]
    public async Task CheckOutputAsync_WithContext_Works()
    {
        var guard = FluxGuard.Create();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckOutputAsync(context, "safe answer");

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    #endregion

    #region FailMode.Open

    [Fact]
    public async Task CheckInputAsync_GuardThrows_FailModeOpen_ReturnsPass()
    {
        var faultyGuard = Substitute.For<IInputGuard>();
        faultyGuard.Name.Returns("FaultyGuard");
        faultyGuard.Layer.Returns("L1");
        faultyGuard.IsEnabled.Returns(true);
        faultyGuard.Order.Returns(0);
        faultyGuard.CheckAsync(Arg.Any<GuardContext>())
            .Throws(new InvalidOperationException("boom"));

        var guard = FluxGuard.Create(builder =>
        {
            builder.WithFailMode(FailMode.Open);
            builder.AddInputGuard(faultyGuard);
        });

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    #endregion

    #region FailMode.Closed

    [Fact]
    public async Task CheckInputAsync_GuardThrows_FailModeClosed_ReturnsBlock()
    {
        var faultyGuard = Substitute.For<IInputGuard>();
        faultyGuard.Name.Returns("FaultyGuard");
        faultyGuard.Layer.Returns("L1");
        faultyGuard.IsEnabled.Returns(true);
        faultyGuard.Order.Returns(0);
        faultyGuard.CheckAsync(Arg.Any<GuardContext>())
            .Throws(new InvalidOperationException("boom"));

        var guard = FluxGuard.Create(builder =>
        {
            builder.WithFailMode(FailMode.Closed);
            builder.AddInputGuard(faultyGuard);
        });

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Blocked);
        result.BlockReason.Should().Contain("FaultyGuard");
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task CheckInputAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Use a guard that will actually check the token
        var slowGuard = Substitute.For<IInputGuard>();
        slowGuard.Name.Returns("SlowGuard");
        slowGuard.Layer.Returns("L1");
        slowGuard.IsEnabled.Returns(true);
        slowGuard.Order.Returns(0);
        slowGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(callInfo =>
            {
                callInfo.Arg<GuardContext>().CancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(GuardCheckResult.Safe);
            });

        var guard = FluxGuard.Create(builder => builder.AddInputGuard(slowGuard));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => guard.CheckInputAsync("test", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Disabled Guards

    [Fact]
    public async Task CheckInputAsync_DisabledGuard_IsSkipped()
    {
        var disabledGuard = Substitute.For<IInputGuard>();
        disabledGuard.IsEnabled.Returns(false);
        disabledGuard.Order.Returns(0);

        var guard = FluxGuard.Create(builder => builder.AddInputGuard(disabledGuard));

        await guard.CheckInputAsync("test");

        await disabledGuard.DidNotReceive().CheckAsync(Arg.Any<GuardContext>());
    }

    #endregion

    #region Guard Ordering

    [Fact]
    public async Task CheckInputAsync_GuardsExecuteInOrderByOrderProperty()
    {
        var executionOrder = new List<string>();

        var guard1 = Substitute.For<IInputGuard>();
        guard1.Name.Returns("Guard1");
        guard1.Layer.Returns("L1");
        guard1.IsEnabled.Returns(true);
        guard1.Order.Returns(10);
        guard1.CheckAsync(Arg.Any<GuardContext>())
            .Returns(callInfo =>
            {
                executionOrder.Add("Guard1");
                return ValueTask.FromResult(GuardCheckResult.Safe);
            });

        var guard2 = Substitute.For<IInputGuard>();
        guard2.Name.Returns("Guard2");
        guard2.Layer.Returns("L1");
        guard2.IsEnabled.Returns(true);
        guard2.Order.Returns(1);
        guard2.CheckAsync(Arg.Any<GuardContext>())
            .Returns(callInfo =>
            {
                executionOrder.Add("Guard2");
                return ValueTask.FromResult(GuardCheckResult.Safe);
            });

        var fluxGuard = FluxGuard.Create(builder =>
        {
            builder.AddInputGuard(guard1);
            builder.AddInputGuard(guard2);
        });

        await fluxGuard.CheckInputAsync("test");

        executionOrder.Should().ContainInOrder("Guard2", "Guard1");
    }

    #endregion

    #region Score-Based Decisions

    [Fact]
    public async Task CheckInputAsync_HighScore_BlocksInput()
    {
        var blockingGuard = Substitute.For<IInputGuard>();
        blockingGuard.Name.Returns("BlockGuard");
        blockingGuard.Layer.Returns("L1");
        blockingGuard.IsEnabled.Returns(true);
        blockingGuard.Order.Returns(0);
        blockingGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(GuardCheckResult.Block(0.95, Severity.High, "test-pattern", "matched", "blocked"));

        var guard = FluxGuard.Create(builder => builder.AddInputGuard(blockingGuard));

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Blocked);
        result.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task CheckInputAsync_MediumScore_FlagsInput()
    {
        var flagGuard = Substitute.For<IInputGuard>();
        flagGuard.Name.Returns("FlagGuard");
        flagGuard.Layer.Returns("L1");
        flagGuard.IsEnabled.Returns(true);
        flagGuard.Order.Returns(0);
        flagGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(new GuardCheckResult
            {
                Passed = true,
                Score = 0.75,
                Severity = Severity.Medium
            });

        var guard = FluxGuard.Create(builder => builder.AddInputGuard(flagGuard));

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Flagged);
        result.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public async Task CheckInputAsync_LowScore_Passes()
    {
        var lowScoreGuard = Substitute.For<IInputGuard>();
        lowScoreGuard.Name.Returns("LowGuard");
        lowScoreGuard.Layer.Returns("L1");
        lowScoreGuard.IsEnabled.Returns(true);
        lowScoreGuard.Order.Returns(0);
        lowScoreGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(new GuardCheckResult
            {
                Passed = true,
                Score = 0.3,
                Severity = Severity.Low
            });

        var guard = FluxGuard.Create(builder => builder.AddInputGuard(lowScoreGuard));

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    #endregion

    #region Escalation

    [Fact]
    public async Task CheckInputAsync_EscalationEnabled_ReturnsEscalation()
    {
        var escalateGuard = Substitute.For<IInputGuard>();
        escalateGuard.Name.Returns("EscalateGuard");
        escalateGuard.Layer.Returns("L1");
        escalateGuard.IsEnabled.Returns(true);
        escalateGuard.Order.Returns(0);
        escalateGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(GuardCheckResult.Escalate(0.6, "ambiguous", "needs review"));

        var guard = FluxGuard.Create(builder =>
        {
            builder.AddInputGuard(escalateGuard);
            builder.Configure(opts =>
            {
                opts.EnableL3Escalation = true;
                opts.EscalationThreshold = 0.5;
            });
        });

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.NeedsEscalation);
        result.NeedsEscalation.Should().BeTrue();
    }

    [Fact]
    public async Task CheckInputAsync_EscalationDisabled_DoesNotEscalate()
    {
        var escalateGuard = Substitute.For<IInputGuard>();
        escalateGuard.Name.Returns("EscalateGuard");
        escalateGuard.Layer.Returns("L1");
        escalateGuard.IsEnabled.Returns(true);
        escalateGuard.Order.Returns(0);
        escalateGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(GuardCheckResult.Escalate(0.6, "ambiguous", "needs review"));

        var guard = FluxGuard.Create(builder =>
        {
            builder.AddInputGuard(escalateGuard);
            builder.Configure(opts =>
            {
                opts.EnableL3Escalation = false;
            });
        });

        var result = await guard.CheckInputAsync("test");

        // Without L3 escalation, it should NOT return NeedsEscalation
        result.Decision.Should().NotBe(GuardDecision.NeedsEscalation);
    }

    #endregion

    #region Hook Lifecycle

    [Fact]
    public async Task Hooks_OnBeforeCheck_ReturnsFalse_SkipsGuards()
    {
        var inputGuard = Substitute.For<IInputGuard>();
        inputGuard.IsEnabled.Returns(true);
        inputGuard.Order.Returns(0);

        var guard = FluxGuard.Create(builder =>
        {
            builder.AddInputGuard(inputGuard);
            builder.WithHooks(hooks => hooks
                .OnBeforeCheck(_ => ValueTask.FromResult(false)));
        });

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Pass);
        await inputGuard.DidNotReceive().CheckAsync(Arg.Any<GuardContext>());
    }

    [Fact]
    public async Task Hooks_OnBlocked_CalledWhenBlocked()
    {
        var blockedCalled = false;
        var blockingGuard = Substitute.For<IInputGuard>();
        blockingGuard.Name.Returns("BlockGuard");
        blockingGuard.Layer.Returns("L1");
        blockingGuard.IsEnabled.Returns(true);
        blockingGuard.Order.Returns(0);
        blockingGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(GuardCheckResult.Block(0.95, Severity.High, details: "blocked"));

        var guard = FluxGuard.Create(builder =>
        {
            builder.AddInputGuard(blockingGuard);
            builder.WithHooks(hooks => hooks
                .OnBlocked((_, _) =>
                {
                    blockedCalled = true;
                    return ValueTask.CompletedTask;
                }));
        });

        await guard.CheckInputAsync("test");

        blockedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Hooks_OnPassed_CalledWhenPassed()
    {
        var passedCalled = false;
        var guard = FluxGuard.Create(builder =>
        {
            builder.WithHooks(hooks => hooks
                .OnPassed((_, _) =>
                {
                    passedCalled = true;
                    return ValueTask.CompletedTask;
                }));
        });

        await guard.CheckInputAsync("Hello, world!");

        passedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Hooks_OnFlagged_CalledWhenFlagged()
    {
        var flaggedCalled = false;
        var flagGuard = Substitute.For<IInputGuard>();
        flagGuard.Name.Returns("FlagGuard");
        flagGuard.Layer.Returns("L1");
        flagGuard.IsEnabled.Returns(true);
        flagGuard.Order.Returns(0);
        flagGuard.CheckAsync(Arg.Any<GuardContext>())
            .Returns(new GuardCheckResult { Passed = true, Score = 0.75, Severity = Severity.Medium });

        var guard = FluxGuard.Create(builder =>
        {
            builder.AddInputGuard(flagGuard);
            builder.WithHooks(hooks => hooks
                .OnFlagged((_, _) =>
                {
                    flaggedCalled = true;
                    return ValueTask.CompletedTask;
                }));
        });

        await guard.CheckInputAsync("test");

        flaggedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Hooks_OnAfterCheck_AlwaysCalled()
    {
        var afterCheckCalled = false;
        var guard = FluxGuard.Create(builder =>
        {
            builder.WithHooks(hooks => hooks
                .OnAfterCheck((_, _) =>
                {
                    afterCheckCalled = true;
                    return ValueTask.CompletedTask;
                }));
        });

        await guard.CheckInputAsync("test");

        afterCheckCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Hooks_OnGuardError_CalledOnException()
    {
        var errorCalled = false;
        var faultyGuard = Substitute.For<IInputGuard>();
        faultyGuard.Name.Returns("FaultyGuard");
        faultyGuard.Layer.Returns("L1");
        faultyGuard.IsEnabled.Returns(true);
        faultyGuard.Order.Returns(0);
        faultyGuard.CheckAsync(Arg.Any<GuardContext>())
            .Throws(new InvalidOperationException("boom"));

        var guard = FluxGuard.Create(builder =>
        {
            builder.WithFailMode(FailMode.Open);
            builder.AddInputGuard(faultyGuard);
            builder.WithHooks(hooks => hooks
                .OnGuardError((_, _, _) =>
                {
                    errorCalled = true;
                    return ValueTask.FromResult(FailDecision.Continue);
                }));
        });

        await guard.CheckInputAsync("test");

        errorCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Hooks_OnCustomDecision_CanOverrideResult()
    {
        var overrideResult = GuardResult.Block("override-req", "custom block", 1.0, Severity.Critical, [], 0);
        var overrideDecision = new FailDecision
        {
            Type = FailDecisionType.Override,
            OverriddenResult = overrideResult
        };

        var guard = FluxGuard.Create(builder =>
        {
            builder.WithHooks(hooks => hooks
                .OnCustomDecision((_, _) => ValueTask.FromResult<FailDecision?>(overrideDecision)));
        });

        var result = await guard.CheckInputAsync("safe input");

        result.Decision.Should().Be(GuardDecision.Blocked);
        result.BlockReason.Should().Be("custom block");
    }

    #endregion

    #region Builder Configuration

    [Fact]
    public void Builder_FluentAPI_ChainsCorrectly()
    {
        var builder = FluxGuardBuilder.Create();

        var result = builder
            .WithPreset(GuardPreset.Strict)
            .WithFailMode(FailMode.Closed)
            .WithBlockThreshold(0.8)
            .WithFlagThreshold(0.5)
            .DisableL2Guards()
            .WithLogging(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        result.Should().BeSameAs(builder);
        builder.Build().Should().NotBeNull();
    }

    [Fact]
    public void Builder_ConfigureInputGuards_Works()
    {
        var builder = FluxGuardBuilder.Create();

        builder.ConfigureInputGuards(opts =>
        {
            opts.EnablePromptInjection = false;
            opts.MaxInputLength = 1000;
        });

        builder.Build().Should().NotBeNull();
    }

    [Fact]
    public void Builder_ConfigureOutputGuards_Works()
    {
        var builder = FluxGuardBuilder.Create();

        builder.ConfigureOutputGuards(opts =>
        {
            opts.EnablePIIMasking = true;
            opts.PIIMaskChar = '#';
        });

        builder.Build().Should().NotBeNull();
    }

    #endregion
}
