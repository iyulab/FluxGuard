using AwesomeAssertions;
using FluxGuard.Core;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.Guards;
using FluxGuard.Remote.Hallucination;
using FluxGuard.Remote.RAG;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FluxGuard.Remote.Tests.Guards;

/// <summary>
/// A check that cannot run is a guard error, not a pass: the guards used to catch every exception and answer "pass"
/// themselves ("fail open"), so a pipeline configured with <see cref="FailMode.Closed"/> never saw the failure.
/// </summary>
public class L3GuardFailureTests
{
    private static readonly IOptions<RemoteGuardOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new RemoteGuardOptions());

    [Fact]
    public async Task HallucinationGuard_PropagatesADetectorFailure()
    {
        var detector = Substitute.For<IHallucinationDetector>();
        detector.DetectAsync(Arg.Any<GuardContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("detector down"));
        var guard = new L3HallucinationGuard(detector, Options, NullLogger<L3HallucinationGuard>.Instance);
        var context = new GuardContext
        {
            OriginalInput = "question",
            Metadata = new Dictionary<string, object> { [L3HallucinationGuard.GroundingContextKey] = "the grounding text" },
        };

        var act = async () => await guard.CheckAsync(context, "an answer");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task RAGSecurityGuard_PropagatesAPipelineFailure()
    {
        var pipeline = Substitute.For<IRAGSecurityPipeline>();
        pipeline.ValidateDocumentsAsync(Arg.Any<IEnumerable<RAGDocument>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("pipeline down"));
        var guard = new L3RAGSecurityGuard(pipeline, Options, NullLogger<L3RAGSecurityGuard>.Instance);
        var context = new GuardContext
        {
            OriginalInput = "question",
            Metadata = new Dictionary<string, object>
            {
                [L3RAGSecurityGuard.RAGDocumentsKey] = new List<RAGDocument> { new() { Id = "d1", Content = "text" } },
            },
        };

        var act = async () => await guard.CheckAsync(context);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
