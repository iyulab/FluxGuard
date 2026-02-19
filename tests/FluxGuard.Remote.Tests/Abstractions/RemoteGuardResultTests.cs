using FluentAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Remote.Abstractions;
using Xunit;

namespace FluxGuard.Remote.Tests.Abstractions;

public class RemoteGuardResultTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var result = new RemoteGuardResult();

        result.Passed.Should().BeFalse();
        result.Score.Should().Be(0);
        result.Severity.Should().Be(Severity.None);
        result.Reasoning.Should().BeNull();
        result.Categories.Should().BeEmpty();
        result.LatencyMs.Should().Be(0);
        result.FromCache.Should().BeFalse();
        result.Model.Should().BeNull();
    }

    [Fact]
    public void Pass_ShouldSetCorrectValues()
    {
        var result = RemoteGuardResult.Pass(12.5, "Content is safe");

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.0);
        result.Reasoning.Should().Be("Content is safe");
        result.LatencyMs.Should().Be(12.5);
        result.Severity.Should().Be(Severity.None);
        result.FromCache.Should().BeFalse();
    }

    [Fact]
    public void Block_ShouldSetCorrectValues()
    {
        var categories = new[] { "hate_speech", "violence" };
        var result = RemoteGuardResult.Block(
            score: 0.95,
            severity: Severity.High,
            reasoning: "Toxic content detected",
            categories: categories,
            latencyMs: 150.3);

        result.Passed.Should().BeFalse();
        result.Score.Should().Be(0.95);
        result.Severity.Should().Be(Severity.High);
        result.Reasoning.Should().Be("Toxic content detected");
        result.Categories.Should().HaveCount(2);
        result.LatencyMs.Should().Be(150.3);
    }

    [Fact]
    public void Block_WithoutCategories_ShouldDefaultToEmpty()
    {
        var result = RemoteGuardResult.Block(0.8, Severity.Medium, "Blocked");

        result.Categories.Should().BeEmpty();
        result.LatencyMs.Should().Be(0);
    }

    [Fact]
    public void FromCacheEntry_ShouldSetCacheFlag()
    {
        var original = RemoteGuardResult.Block(0.9, Severity.High, "Harmful");

        var cached = RemoteGuardResult.FromCacheEntry(original, 0.5);

        cached.FromCache.Should().BeTrue();
        cached.LatencyMs.Should().Be(0.5);
        cached.Passed.Should().Be(original.Passed);
        cached.Score.Should().Be(original.Score);
        cached.Severity.Should().Be(original.Severity);
        cached.Reasoning.Should().Be(original.Reasoning);
    }
}

public class CompletionRequestTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var request = new CompletionRequest { UserPrompt = "Hello" };

        request.SystemPrompt.Should().BeNull();
        request.Model.Should().BeNull();
        request.MaxTokens.Should().BeNull();
        request.Temperature.Should().BeNull();
        request.ResponseFormat.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var request = new CompletionRequest
        {
            SystemPrompt = "You are a safety classifier.",
            UserPrompt = "Classify this text.",
            Model = "gpt-4o-mini",
            MaxTokens = 256,
            Temperature = 0.0,
            ResponseFormat = "json_object"
        };

        request.SystemPrompt.Should().Be("You are a safety classifier.");
        request.Model.Should().Be("gpt-4o-mini");
        request.ResponseFormat.Should().Be("json_object");
    }
}

public class CompletionResponseTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var response = new CompletionResponse();

        response.Success.Should().BeFalse();
        response.Content.Should().BeNull();
        response.Error.Should().BeNull();
        response.Model.Should().BeNull();
        response.InputTokens.Should().BeNull();
        response.OutputTokens.Should().BeNull();
        response.LatencyMs.Should().Be(0);
    }

    [Fact]
    public void Ok_ShouldSetCorrectValues()
    {
        var response = CompletionResponse.Ok(
            content: "Safe",
            model: "gpt-4o-mini",
            inputTokens: 100,
            outputTokens: 5,
            latencyMs: 250.0);

        response.Success.Should().BeTrue();
        response.Content.Should().Be("Safe");
        response.Model.Should().Be("gpt-4o-mini");
        response.InputTokens.Should().Be(100);
        response.OutputTokens.Should().Be(5);
        response.LatencyMs.Should().Be(250.0);
        response.Error.Should().BeNull();
    }

    [Fact]
    public void Fail_ShouldSetCorrectValues()
    {
        var response = CompletionResponse.Fail("API rate limit exceeded", 50.0);

        response.Success.Should().BeFalse();
        response.Error.Should().Be("API rate limit exceeded");
        response.LatencyMs.Should().Be(50.0);
        response.Content.Should().BeNull();
    }
}

public class CacheStatsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var stats = new CacheStats();

        stats.TotalEntries.Should().Be(0);
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.MemoryBytes.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(10, 0, 1.0)]
    [InlineData(0, 10, 0.0)]
    [InlineData(7, 3, 0.7)]
    [InlineData(1, 1, 0.5)]
    public void HitRate_ShouldComputeCorrectly(long hits, long misses, double expected)
    {
        var stats = new CacheStats { Hits = hits, Misses = misses };
        stats.HitRate.Should().BeApproximately(expected, 0.001);
    }
}
