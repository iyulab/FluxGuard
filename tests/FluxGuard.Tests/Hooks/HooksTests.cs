using FluxGuard.Core;
using FluxGuard.Hooks;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.Hooks;

public class FluxGuardHooksTests
{
    [Fact]
    public async Task OnBeforeCheckAsync_DefaultReturnsTrue()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await hooks.OnBeforeCheckAsync(context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OnAfterCheckAsync_DefaultCompletesSuccessfully()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var guardResult = GuardResult.Pass("req-1", 0);

        await hooks.OnAfterCheckAsync(context, guardResult);
    }

    [Fact]
    public async Task OnBlockedAsync_DefaultCompletesSuccessfully()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var guardResult = GuardResult.Block("req-1", "blocked", 1.0, Severity.Critical, [], 0);

        await hooks.OnBlockedAsync(context, guardResult);
    }

    [Fact]
    public async Task OnPassedAsync_DefaultCompletesSuccessfully()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var guardResult = GuardResult.Pass("req-1", 0);

        await hooks.OnPassedAsync(context, guardResult);
    }

    [Fact]
    public async Task OnFlaggedAsync_DefaultCompletesSuccessfully()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var guardResult = GuardResult.Flag("req-1", 0.7, Severity.Medium, [], 0);

        await hooks.OnFlaggedAsync(context, guardResult);
    }

    [Fact]
    public async Task OnCustomDecisionAsync_DefaultReturnsNull()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var guardResult = GuardResult.Pass("req-1", 0);

        var result = await hooks.OnCustomDecisionAsync(context, guardResult);

        result.Should().BeNull();
    }

    [Fact]
    public async Task OnGuardErrorAsync_DefaultReturnsContinue()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var ex = new InvalidOperationException("test");

        var result = await hooks.OnGuardErrorAsync(context, "TestGuard", ex);

        result.Type.Should().Be(FailDecisionType.Continue);
    }

    [Fact]
    public async Task OnBeforeEscalationAsync_DefaultReturnsTrue()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var guardResult = GuardResult.Escalate("req-1", 0.6, [], 0);

        var result = await hooks.OnBeforeEscalationAsync(context, guardResult);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task OnEscalationTimeoutAsync_DefaultReturnsLocalResult()
    {
        var hooks = new FluxGuardHooks();
        var context = new GuardContext { OriginalInput = "test" };
        var localResult = GuardResult.Escalate("req-1", 0.6, [], 0);

        var result = await hooks.OnEscalationTimeoutAsync(context, localResult);

        result.Should().BeSameAs(localResult);
    }
}

public class LambdaHooksBuilderTests
{
    [Fact]
    public async Task OnBeforeCheck_LambdaIsInvoked()
    {
        var called = false;
        var guard = FluxGuard.Create(builder =>
        {
            builder.WithHooks(hooks => hooks
                .OnBeforeCheck(_ =>
                {
                    called = true;
                    return ValueTask.FromResult(true);
                }));
        });

        await guard.CheckInputAsync("test");

        called.Should().BeTrue();
    }

    [Fact]
    public async Task OnAfterCheck_LambdaIsInvoked()
    {
        var called = false;
        var guard = FluxGuard.Create(builder =>
        {
            builder.WithHooks(hooks => hooks
                .OnAfterCheck((_, _) =>
                {
                    called = true;
                    return ValueTask.CompletedTask;
                }));
        });

        await guard.CheckInputAsync("test");

        called.Should().BeTrue();
    }

    [Fact]
    public async Task NullLambda_FallsBackToBase()
    {
        // Build hooks with no lambda set — should fall back to base class
        var guard = FluxGuard.Create(builder =>
        {
            builder.WithHooks(_ => { /* no hooks configured */ });
        });

        var result = await guard.CheckInputAsync("test");

        result.Decision.Should().Be(GuardDecision.Pass);
    }
}

public class FailDecisionTests
{
    [Fact]
    public void Continue_HasCorrectType()
    {
        var decision = FailDecision.Continue;

        decision.Type.Should().Be(FailDecisionType.Continue);
        decision.Reason.Should().BeNull();
        decision.OverriddenResult.Should().BeNull();
    }

    [Fact]
    public void Continue_IsSingleton()
    {
        var d1 = FailDecision.Continue;
        var d2 = FailDecision.Continue;

        d1.Should().BeSameAs(d2);
    }

    [Fact]
    public void AllowPass_HasOverrideType()
    {
        var decision = FailDecision.AllowPass("test reason");

        decision.Type.Should().Be(FailDecisionType.Override);
        decision.Reason.Should().Be("test reason");
    }

    [Fact]
    public void AllowPass_WithoutReason_HasNullReason()
    {
        var decision = FailDecision.AllowPass();

        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void ForceBlock_HasOverrideType()
    {
        var decision = FailDecision.ForceBlock("block reason");

        decision.Type.Should().Be(FailDecisionType.Override);
        decision.Reason.Should().Be("block reason");
    }

    [Fact]
    public void AllowPass_DoesNotSetOverriddenResult()
    {
        // Note: AllowPass/ForceBlock set Type=Override but NOT OverriddenResult.
        // FluxGuardCore only applies override when OverriddenResult is not null.
        // This is a known design limitation.
        var decision = FailDecision.AllowPass();

        decision.OverriddenResult.Should().BeNull();
    }

    [Fact]
    public void ForceBlock_DoesNotSetOverriddenResult()
    {
        var decision = FailDecision.ForceBlock("reason");

        decision.OverriddenResult.Should().BeNull();
    }

    [Theory]
    [InlineData(FailDecisionType.Continue, 0)]
    [InlineData(FailDecisionType.Override, 1)]
    public void FailDecisionType_HasExpectedValues(FailDecisionType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}
