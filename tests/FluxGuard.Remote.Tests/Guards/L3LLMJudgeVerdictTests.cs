using AwesomeAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Caching;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.Guards;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

#pragma warning disable CA2012 // Use ValueTasks correctly - configuring a substitute stores the ValueTask it returns

namespace FluxGuard.Remote.Tests.Guards;

/// <summary>
/// Two promises of the judge that nothing kept. <c>LLMJudgeOptions.BlockThreshold</c> (and the builder's
/// <c>WithBlockThreshold</c>) was read by nothing: any "unsafe" verdict blocked, whatever its confidence. And a judge
/// that could not answer reported "pass" by itself, so the pipeline's <see cref="FailMode"/> never saw the failure -
/// and a reply that could not be parsed was cached as that pass.
/// </summary>
public class L3LLMJudgeVerdictTests
{
    private readonly IRemoteLlmService _service = Substitute.For<IRemoteLlmService>();

    private L3LLMJudgeGuard CreateJudge(Action<RemoteGuardOptions>? configure = null)
    {
        _service.IsAvailable.Returns(true);
        var value = new RemoteGuardOptions();
        configure?.Invoke(value);
        var options = Options.Create(value);
        return new L3LLMJudgeGuard(_service, new InMemorySemanticCache(options), options, NullLogger<L3LLMJudgeGuard>.Instance);
    }

    private void JudgeAnswers(string json) => _service
        .CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
        .Returns(CompletionResponse.Ok(json, "judge-model"));

    private static string Unsafe(string confidence) =>
        "{ \"is_safe\": false, " + confidence + "\"severity\": \"high\", \"categories\": [\"prompt_injection\"], \"reasoning\": \"looks like an injection\" }";

    private static ValueTask<RemoteGuardResult> CheckAsync(L3LLMJudgeGuard judge, string input = "some input") =>
        judge.CheckInputAsync(new GuardContext { OriginalInput = input }, GuardResult.Pass("test", 0), TestContext.Current.CancellationToken);

    [Fact]
    public async Task UnsafeVerdictBelowTheBlockThreshold_IsFlaggedNotBlocked()
    {
        JudgeAnswers(Unsafe("\"confidence\": 0.6, "));

        var result = await CheckAsync(CreateJudge());

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.6);
        result.Reasoning.Should().Contain("injection");
    }

    [Fact]
    public async Task UnsafeVerdictAtTheBlockThreshold_Blocks()
    {
        JudgeAnswers(Unsafe("\"confidence\": 0.8, "));

        var result = await CheckAsync(CreateJudge());

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ALowerBlockThreshold_BlocksTheSameVerdict()
    {
        // The same reply as the first fact: only the option differs, so the option is what decides.
        JudgeAnswers(Unsafe("\"confidence\": 0.6, "));

        var result = await CheckAsync(CreateJudge(o => o.Judge.BlockThreshold = 0.5));

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task UnsafeVerdictWithoutAConfidence_Blocks()
    {
        // No number to compare: the verdict stands.
        JudgeAnswers(Unsafe(string.Empty));

        var result = await CheckAsync(CreateJudge());

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task AJudgeThatCannotAnswer_IsAGuardError()
    {
        _service.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CompletionResponse.Fail("connection refused"));

        var act = async () => await CheckAsync(CreateJudge());

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("connection refused");
    }

    [Fact]
    public async Task AReplyThatCannotBeParsed_IsAGuardError_AndIsNotCached()
    {
        var judge = CreateJudge();
        JudgeAnswers("this is not json");

        var first = async () => await CheckAsync(judge, "same input");
        await first.Should().ThrowAsync<InvalidOperationException>();

        // The same input again, and this time the judge answers: a cached "pass" from the failed call would hide it.
        JudgeAnswers(Unsafe("\"confidence\": 0.95, "));
        var second = await CheckAsync(judge, "same input");

        second.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task FailClosedPipeline_BlocksWhenTheJudgeCannotAnswer()
    {
        _service.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CompletionResponse.Fail("connection refused"));
        var guard = BuildPipeline(FailMode.Closed);

        var result = await guard.CheckInputAsync("ambiguous input", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain("L3 guard error");
    }

    [Fact]
    public async Task FailOpenPipeline_PassesWhenTheJudgeCannotAnswer()
    {
        _service.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CompletionResponse.Fail("connection refused"));
        var guard = BuildPipeline(FailMode.Open);

        var result = await guard.CheckInputAsync("ambiguous input", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeFalse();
    }

    private IFluxGuard BuildPipeline(FailMode failMode)
    {
        var escalate = Substitute.For<IInputGuard>();
        escalate.Name.Returns("Ambiguous");
        escalate.Layer.Returns("L2");
        escalate.IsEnabled.Returns(true);
        escalate.CheckAsync(Arg.Any<GuardContext>())
            .Returns(GuardCheckResult.Escalate(0.6, "ambiguous", "uncertain content"));

        return FluxGuardBuilder.Create()
            .WithFailMode(failMode)
            .AddInputGuard(escalate)
            .AddRemoteGuard(CreateJudge())
            .Configure(o =>
            {
                o.EnableL3Escalation = true;
                o.EscalationThreshold = 0.5;
            })
            .Build();
    }
}
