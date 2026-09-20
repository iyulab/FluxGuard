using AwesomeAssertions;
using FluxGuard.Core;
using FluxGuard.SDK.AI.ChatClient;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FluxGuard.SDK.Tests;

/// <summary>
/// <c>ValidateStreamingOutput</c> was read by nothing: a streamed response was never checked, whatever the option said,
/// while the same response fetched without streaming was. The check runs when the stream ends, on the whole text.
/// </summary>
public class FluxGuardChatClientStreamingTests
{
    private const string Leaked = "the secret is 1234";

    private static IChatClient InnerStreaming(params string[] parts)
    {
        var inner = Substitute.For<IChatClient>();
        inner.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Stream(parts));
        return inner;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(string[] parts)
    {
        foreach (var part in parts)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, part);
            await Task.CompletedTask;
        }
    }

    /// <summary>A guard that passes every input and blocks exactly one output text.</summary>
    private static IFluxGuard GuardBlockingOutput(string blockedOutput)
    {
        var guard = Substitute.For<IFluxGuard>();
        guard.CheckInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GuardResult.Pass("in", 0));
        guard.CheckOutputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(1) == blockedOutput
                ? GuardResult.Block("out", "leak", 1.0, Severity.High, [], 0)
                : GuardResult.Pass("out", 0));
        return guard;
    }

    private static async Task<List<string>> ReadAsync(FluxGuardChatClient client)
    {
        var received = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "question")], null, TestContext.Current.CancellationToken))
        {
            received.Add(update.Text);
        }

        return received;
    }

    [Fact]
    public async Task ValidateStreamingOutput_On_ThrowsAtTheEndOfABlockedStream()
    {
        var client = new FluxGuardChatClient(
            InnerStreaming("the secret ", "is 1234"), GuardBlockingOutput(Leaked),
            NullLogger<FluxGuardChatClient>.Instance,
            new FluxGuardChatClientOptions { ValidateStreamingOutput = true });

        var act = async () => await ReadAsync(client);

        (await act.Should().ThrowAsync<FluxGuardChatBlockedException>()).Which.Result.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateStreamingOutput_On_PassesAStreamTheGuardAccepts()
    {
        // The counterpart: a client that threw on every stream would pass the fact above.
        var client = new FluxGuardChatClient(
            InnerStreaming("a harmless ", "answer"), GuardBlockingOutput(Leaked),
            NullLogger<FluxGuardChatClient>.Instance,
            new FluxGuardChatClientOptions { ValidateStreamingOutput = true });

        var received = await ReadAsync(client);

        received.Should().Equal("a harmless ", "answer");
    }

    [Fact]
    public async Task ValidateStreamingOutput_Off_DoesNotCheckTheStream()
    {
        var guard = GuardBlockingOutput(Leaked);
        var client = new FluxGuardChatClient(
            InnerStreaming("the secret ", "is 1234"), guard,
            NullLogger<FluxGuardChatClient>.Instance,
            new FluxGuardChatClientOptions { ValidateStreamingOutput = false });

        var received = await ReadAsync(client);

        received.Should().Equal("the secret ", "is 1234");
        await guard.DidNotReceive().CheckOutputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateOutput_Off_WinsOverValidateStreamingOutput()
    {
        var guard = GuardBlockingOutput(Leaked);
        var client = new FluxGuardChatClient(
            InnerStreaming("the secret ", "is 1234"), guard,
            NullLogger<FluxGuardChatClient>.Instance,
            new FluxGuardChatClientOptions { ValidateOutput = false, ValidateStreamingOutput = true });

        await ReadAsync(client);

        await guard.DidNotReceive().CheckOutputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
