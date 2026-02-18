#pragma warning disable CA2012 // Use ValueTasks correctly — test mocking requires storing ValueTask from NSubstitute .Returns()

using FluentAssertions;
using FluxGuard.Core;
using FluxGuard.Streaming;
using NSubstitute;
using Xunit;

namespace FluxGuard.Tests.Streaming;

public class StreamingGuardOrchestratorTests
{
    private static GuardContext CreateContext() => new()
    {
        OriginalInput = "test input"
    };

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    private static IStreamingGuard CreatePassGuard()
    {
        var guard = Substitute.For<IStreamingGuard>();
        guard.IsEnabled.Returns(true);
        guard.ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(TokenValidation.Safe));
        guard.ValidateFinalAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(TokenValidation.Safe));
        return guard;
    }

    #region Chunk Pass-Through

    [Fact]
    public async Task ValidateStreamAsync_AllChunksPass_YieldsAllChunks()
    {
        var guard = CreatePassGuard();
        var orchestrator = new StreamingGuardOrchestrator([guard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("Hello ", "World");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Should().HaveCount(3);
        results[0].OriginalChunk.Should().Be("Hello ");
        results[0].OutputChunk.Should().Be("Hello ");
        results[0].IsTerminated.Should().BeFalse();
        results[0].IsSuppressed.Should().BeFalse();
        results[1].OriginalChunk.Should().Be("World");
        results[2].IsFinal.Should().BeTrue();
    }

    #endregion

    #region Stream Termination

    [Fact]
    public async Task ValidateStreamAsync_TerminateOnSecondChunk_StopsEarly()
    {
        var guard = Substitute.For<IStreamingGuard>();
        guard.IsEnabled.Returns(true);
        var callCount = 0;
        guard.ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref callCount) == 2)
                    return new ValueTask<TokenValidation>(new TokenValidation
                    {
                        Passed = false,
                        ShouldTerminate = true,
                        GuardName = "test-guard"
                    });
                return new ValueTask<TokenValidation>(TokenValidation.Safe);
            });
        var orchestrator = new StreamingGuardOrchestrator([guard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("safe", "bad", "unreachable");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Should().HaveCount(2);
        results[0].IsTerminated.Should().BeFalse();
        results[1].IsTerminated.Should().BeTrue();
        results[1].OriginalChunk.Should().Be("bad");
    }

    #endregion

    #region Chunk Suppression

    [Fact]
    public async Task ValidateStreamAsync_SuppressedChunk_SetsSuppressedFlag()
    {
        var guard = Substitute.For<IStreamingGuard>();
        guard.IsEnabled.Returns(true);
        guard.ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(new TokenValidation
            {
                Passed = false,
                ShouldSuppress = true
            }));
        guard.ValidateFinalAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(TokenValidation.Safe));
        var orchestrator = new StreamingGuardOrchestrator([guard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("bad");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Should().Contain(r => r.IsSuppressed);
    }

    [Fact]
    public async Task ValidateStreamAsync_SuppressedWithReplacement_YieldsReplacement()
    {
        var guard = Substitute.For<IStreamingGuard>();
        guard.IsEnabled.Returns(true);
        guard.ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(new TokenValidation
            {
                Passed = false,
                ShouldSuppress = true,
                ReplacementText = "[REDACTED]"
            }));
        guard.ValidateFinalAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(TokenValidation.Safe));
        var orchestrator = new StreamingGuardOrchestrator([guard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("sensitive");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        var suppressed = results.First(r => r.IsSuppressed);
        suppressed.OutputChunk.Should().Be("[REDACTED]");
    }

    #endregion

    #region Empty Stream

    [Fact]
    public async Task ValidateStreamAsync_EmptyStream_YieldsNothing()
    {
        var guard = CreatePassGuard();
        var orchestrator = new StreamingGuardOrchestrator([guard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable();

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    #endregion

    #region Disabled Guards

    [Fact]
    public async Task ValidateStreamAsync_DisabledGuard_IsFiltered()
    {
        var disabledGuard = Substitute.For<IStreamingGuard>();
        disabledGuard.IsEnabled.Returns(false);
        var enabledGuard = CreatePassGuard();
        var orchestrator = new StreamingGuardOrchestrator(
            [disabledGuard, enabledGuard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("test");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Where(r => !r.IsFinal).Should().ContainSingle();
        await disabledGuard.DidNotReceive().ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Guard Error Handling

    [Fact]
    public async Task ValidateStreamAsync_GuardThrows_ContinuesWithNextGuard()
    {
        var failingGuard = Substitute.For<IStreamingGuard>();
        failingGuard.IsEnabled.Returns(true);
        failingGuard.ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<TokenValidation>>(_ => throw new InvalidOperationException("guard error"));
        failingGuard.ValidateFinalAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<TokenValidation>>(_ => throw new InvalidOperationException("guard error"));

        var passingGuard = CreatePassGuard();
        var orchestrator = new StreamingGuardOrchestrator(
            [failingGuard, passingGuard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("test");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Where(r => !r.IsFinal).Should().ContainSingle();
        results.First(r => !r.IsFinal).OutputChunk.Should().Be("test");
    }

    #endregion

    #region No Guards

    [Fact]
    public async Task ValidateStreamAsync_NoGuards_PassesAllChunks()
    {
        var orchestrator = new StreamingGuardOrchestrator([],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        var chunks = ToAsyncEnumerable("hello", "world");

        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(CreateContext(), chunks))
        {
            results.Add(result);
        }

        results.Where(r => !r.IsFinal).Should().HaveCount(2);
        results.Last().IsFinal.Should().BeTrue();
    }

    #endregion

    #region CancellationToken

    [Fact]
    public async Task ValidateStreamAsync_PassesCancellationToken()
    {
        var guard = CreatePassGuard();
        var orchestrator = new StreamingGuardOrchestrator([guard],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });
        using var cts = new CancellationTokenSource();
        var chunks = ToAsyncEnumerable("test");

        await foreach (var _ in orchestrator.ValidateStreamAsync(CreateContext(), chunks, cts.Token))
        {
            // consume
        }

        await guard.Received(1).ValidateChunkAsync(
            Arg.Any<GuardContext>(), Arg.Any<string>(),
            Arg.Any<string>(), cts.Token);
    }

    #endregion
}
