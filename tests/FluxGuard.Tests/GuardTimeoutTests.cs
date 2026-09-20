using System.Diagnostics;
using AwesomeAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Hooks;
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// <c>GuardTimeoutMs</c> was declared with a default of 5000 and read by nothing: a guard that never answered held the
/// request for as long as it liked. A guard that outlives the timeout is a guard error, so it takes the same
/// <see cref="FailMode"/> path as a guard that throws.
/// </summary>
public class GuardTimeoutTests
{
    private static readonly TimeSpan GuardDelay = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task CheckInputAsync_FailOpen_SkipsAGuardThatOutlivesTheTimeout()
    {
        var hooks = new RecordingHooks();
        var guard = FluxGuard.Create(builder => builder
            .Configure(o => o.GuardTimeoutMs = 100)
            .WithFailMode(FailMode.Open)
            .WithHooks(hooks)
            .AddInputGuard(new SlowGuard(GuardDelay)));

        var stopwatch = Stopwatch.StartNew();
        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        result.IsBlocked.Should().BeFalse();
        hooks.GuardErrors.Should().ContainSingle().Which.Should().BeOfType<TimeoutException>()
            .Which.Message.Should().Contain("slow").And.Contain("100");
    }

    [Fact]
    public async Task CheckInputAsync_FailClosed_BlocksWhenAGuardOutlivesTheTimeout()
    {
        var guard = FluxGuard.Create(builder => builder
            .Configure(o => o.GuardTimeoutMs = 100)
            .WithFailMode(FailMode.Closed)
            .AddInputGuard(new SlowGuard(GuardDelay)));

        var stopwatch = Stopwatch.StartNew();
        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        // Without the elapsed bound this passes on a pipeline with no timeout: the guard blocks by itself after 30 s.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain("Guard error").And.Contain("slow");
    }

    [Fact]
    public async Task CheckInputAsync_KeepsTheVerdictOfAGuardThatAnswersInTime()
    {
        // The counterpart: a timeout that fired on every guard would pass the two facts above as well.
        var guard = FluxGuard.Create(builder => builder
            .Configure(o => o.GuardTimeoutMs = 30_000)
            .WithFailMode(FailMode.Open)
            .AddInputGuard(new SlowGuard(TimeSpan.FromMilliseconds(50))));

        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
        result.TriggeredGuards.Should().ContainSingle().Which.GuardName.Should().Be("slow");
    }

    [Fact]
    public async Task CheckInputAsync_TreatsZeroAsNoTimeout()
    {
        var guard = FluxGuard.Create(builder => builder
            .Configure(o => o.GuardTimeoutMs = 0)
            .WithFailMode(FailMode.Open)
            .AddInputGuard(new SlowGuard(TimeSpan.FromMilliseconds(300))));

        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task CheckOutputAsync_FailClosed_BlocksWhenAGuardOutlivesTheTimeout()
    {
        var guard = FluxGuard.Create(builder => builder
            .Configure(o => o.GuardTimeoutMs = 100)
            .WithFailMode(FailMode.Closed)
            .AddOutputGuard(new SlowGuard(GuardDelay)));

        var stopwatch = Stopwatch.StartNew();
        var result = await guard.CheckOutputAsync("question", "answer", TestContext.Current.CancellationToken);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain("slow");
    }

    [Fact]
    public async Task CheckInputAsync_CallerCancellationIsNotReportedAsATimeout()
    {
        var guard = FluxGuard.Create(builder => builder
            .Configure(o => o.GuardTimeoutMs = 30_000)
            .AddInputGuard(new SlowGuard(GuardDelay)));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(100);

        var act = () => guard.CheckInputAsync("hello", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>Blocks with high severity once <paramref name="delay"/> has passed; ignores the context's token on purpose.</summary>
    private sealed class SlowGuard(TimeSpan delay) : IInputGuard, IOutputGuard
    {
        public string Name => "slow";
        public string Layer => "L1";
        public bool IsEnabled => true;
        public int Order => 0;

        public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
        {
            await Task.Delay(delay, CancellationToken.None);
            return Verdict();
        }

        public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context, string output)
        {
            await Task.Delay(delay, CancellationToken.None);
            return Verdict();
        }

        private static GuardCheckResult Verdict() => new()
        {
            GuardName = "slow",
            Passed = false,
            Score = 1.0,
            Severity = Severity.Critical,
            Details = "slow guard verdict",
        };
    }

    private sealed class RecordingHooks : FluxGuardHooks
    {
        public List<Exception> GuardErrors { get; } = [];

        public override ValueTask<FailDecision> OnGuardErrorAsync(GuardContext context, string guardName, Exception exception)
        {
            GuardErrors.Add(exception);
            return base.OnGuardErrorAsync(context, guardName, exception);
        }
    }
}
