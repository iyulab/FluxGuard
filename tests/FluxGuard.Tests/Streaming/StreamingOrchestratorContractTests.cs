#pragma warning disable CA2012 // Use ValueTasks correctly - configuring a substitute stores the ValueTask it returns

using AwesomeAssertions;
using FluxGuard.Core;
using FluxGuard.Streaming;
using NSubstitute;
using Xunit;

namespace FluxGuard.Tests.Streaming;

/// <summary>
/// What a caller forwarding <see cref="StreamingChunkResult.OutputChunk"/> actually receives. The final result used
/// to carry the unprocessed tail of the buffer as its <c>OutputChunk</c>, after every chunk had already been forwarded,
/// so the tail was sent twice. <c>MinChunkSize</c> was read by nothing, and a guard that threw was skipped with no way
/// to ask for the stream to stop instead.
/// </summary>
public class StreamingOrchestratorContractTests
{
    private static GuardContext Context() => new() { OriginalInput = "question" };

    private static async IAsyncEnumerable<string> Chunks(params string[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    private static IStreamingGuard PassGuard(List<string>? seenChunks = null)
    {
        var guard = Substitute.For<IStreamingGuard>();
        guard.IsEnabled.Returns(true);
        guard.ValidateChunkAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                seenChunks?.Add(call.ArgAt<string>(1));
                return new ValueTask<TokenValidation>(TokenValidation.Safe);
            });
        guard.ValidateFinalAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TokenValidation>(TokenValidation.Safe));
        return guard;
    }

    private static IStreamingGuard ThrowingGuard()
    {
        var guard = Substitute.For<IStreamingGuard>();
        guard.IsEnabled.Returns(true);
        guard.Name.Returns("broken");
        guard.ValidateChunkAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<TokenValidation>>(_ => throw new InvalidOperationException("guard error"));
        guard.ValidateFinalAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<TokenValidation>>(_ => throw new InvalidOperationException("guard error"));
        return guard;
    }

    private static async Task<List<StreamingChunkResult>> RunAsync(StreamingGuardOrchestrator orchestrator, params string[] chunks)
    {
        var results = new List<StreamingChunkResult>();
        await foreach (var result in orchestrator.ValidateStreamAsync(Context(), Chunks(chunks), TestContext.Current.CancellationToken))
        {
            results.Add(result);
        }

        return results;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForwardedOutput_IsTheInputExactlyOnce(bool sentenceLevel)
    {
        var orchestrator = new StreamingGuardOrchestrator([PassGuard()],
            new StreamingGuardOptions { EnableSentenceLevelValidation = sentenceLevel });

        var results = await RunAsync(orchestrator, "Hello ", "World. ", "And more");

        string.Concat(results.Select(r => r.OutputChunk)).Should().Be("Hello World. And more");
        results[^1].IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task MinChunkSize_CoalescesSmallChunksBeforeValidation()
    {
        var seen = new List<string>();
        var orchestrator = new StreamingGuardOrchestrator([PassGuard(seen)],
            new StreamingGuardOptions { MinChunkSize = 5, EnableSentenceLevelValidation = false });

        var results = await RunAsync(orchestrator, "a", "b", "c", "d", "e", "f");

        seen.Should().Equal("abcde", "f");
        string.Concat(results.Select(r => r.OutputChunk)).Should().Be("abcdef");
    }

    [Fact]
    public async Task MinChunkSize_DefaultValidatesEveryChunk()
    {
        // The counterpart: coalescing that always happened would pass the fact above with any option value.
        var seen = new List<string>();
        var orchestrator = new StreamingGuardOrchestrator([PassGuard(seen)],
            new StreamingGuardOptions { EnableSentenceLevelValidation = false });

        await RunAsync(orchestrator, "a", "b", "c");

        seen.Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task FailClosed_AGuardThatThrowsTerminatesTheStream()
    {
        var orchestrator = new StreamingGuardOrchestrator([ThrowingGuard(), PassGuard()],
            new StreamingGuardOptions { FailMode = FailMode.Closed, EnableSentenceLevelValidation = false });

        var results = await RunAsync(orchestrator, "first", "second");

        results.Should().ContainSingle().Which.IsTerminated.Should().BeTrue();
        results[0].OutputChunk.Should().BeNull();
        results[0].Validation.GuardName.Should().Be("broken");
    }

    [Fact]
    public async Task FailOpen_AGuardThatThrowsIsSkipped()
    {
        var orchestrator = new StreamingGuardOrchestrator([ThrowingGuard(), PassGuard()],
            new StreamingGuardOptions { FailMode = FailMode.Open, EnableSentenceLevelValidation = false });

        var results = await RunAsync(orchestrator, "first", "second");

        results.Should().NotContain(r => r.IsTerminated);
        string.Concat(results.Select(r => r.OutputChunk)).Should().Be("firstsecond");
    }
}
