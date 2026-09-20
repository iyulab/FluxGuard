using AwesomeAssertions;
using FluxGuard.Core;
using FluxGuard.Hooks;
using NSubstitute;

#pragma warning disable CA2012 // Use ValueTasks correctly - configuring a substitute stores the ValueTask it returns
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// <c>MaxInputLength</c> and <c>MaxOutputLength</c> were declared, shown in the README's configuration example, and read by
/// nothing: an input of any length passed. These drive the public entry points, because a test of a length check alone
/// would pass while the pipeline never called it.
/// </summary>
public class LengthLimitTests
{
    [Fact]
    public async Task CheckInputAsync_BlocksAnInputOverTheConfiguredMaximum()
    {
        var guard = FluxGuard.Create(builder => builder.ConfigureInputGuards(o => o.MaxInputLength = 8192));

        var result = await guard.CheckInputAsync(new string('a', 8193), TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain("8193").And.Contain("8192");
        result.TriggeredGuards.Should().ContainSingle().Which.GuardName.Should().Be("InputLength");
    }

    [Fact]
    public async Task CheckInputAsync_PassesAnInputAtTheConfiguredMaximum()
    {
        var guard = FluxGuard.Create(builder => builder.ConfigureInputGuards(o => o.MaxInputLength = 8192));

        var result = await guard.CheckInputAsync(new string('a', 8192), TestContext.Current.CancellationToken);

        // The counterpart: without it the fact above would pass on a pipeline that blocks everything.
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task CheckInputAsync_TreatsZeroAsNoLimit()
    {
        var guard = FluxGuard.Create(builder => builder.ConfigureInputGuards(o => o.MaxInputLength = 0));

        var result = await guard.CheckInputAsync(new string('a', 200_000), TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task CheckOutputAsync_BlocksAnOutputOverTheConfiguredMaximum()
    {
        var guard = FluxGuard.Create(builder => builder.ConfigureOutputGuards(o => o.MaxOutputLength = 4096));

        var blocked = await guard.CheckOutputAsync("question", new string('b', 4097), TestContext.Current.CancellationToken);
        var passed = await guard.CheckOutputAsync("question", new string('b', 4096), TestContext.Current.CancellationToken);

        blocked.IsBlocked.Should().BeTrue();
        blocked.TriggeredGuards.Should().ContainSingle().Which.GuardName.Should().Be("OutputLength");
        passed.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task ALengthBlock_RunsTheSameHooksAsAnyOtherBlock()
    {
        var hooks = Substitute.For<IFluxGuardHooks>();
        hooks.OnBeforeCheckAsync(Arg.Any<GuardContext>()).Returns(new ValueTask<bool>(true));
        var guard = FluxGuard.Create(builder => builder
            .ConfigureInputGuards(o => o.MaxInputLength = 10)
            .WithHooks(hooks));

        await guard.CheckInputAsync(new string('a', 11), TestContext.Current.CancellationToken);

        await hooks.Received(1).OnBlockedAsync(Arg.Any<GuardContext>(), Arg.Is<GuardResult>(r => r.IsBlocked));
        await hooks.Received(1).OnAfterCheckAsync(Arg.Any<GuardContext>(), Arg.Is<GuardResult>(r => r.IsBlocked));
    }
}
