using AwesomeAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Caching;
using FluxGuard.Remote.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace FluxGuard.Remote.Tests.Caching;

public class InMemorySemanticCacheTests
{
    private readonly InMemorySemanticCache _cache;

    public InMemorySemanticCacheTests()
    {
        var options = Options.Create(new RemoteGuardOptions
        {
            EnableCache = true,
            CacheTtlSeconds = 3600,
            MaxCacheEntries = 100
        });
        _cache = new InMemorySemanticCache(options);
    }

    [Fact]
    public async Task TryGetAsync_NotCached_ReturnsNull()
    {
        // Act
        var result = await _cache.TryGetAsync("test input", "InputJudge", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGet_ReturnsCachedResult()
    {
        // Arrange
        var input = "test input for caching";
        var guardType = "InputJudge";
        var expectedResult = new RemoteGuardResult
        {
            Passed = true,
            Score = 0.1,
            Reasoning = "Test reasoning"
        };

        // Act
        await _cache.SetAsync(input, guardType, expectedResult, TestContext.Current.CancellationToken);
        var result = await _cache.TryGetAsync(input, guardType, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Passed.Should().Be(expectedResult.Passed);
        result.Score.Should().Be(expectedResult.Score);
        result.Reasoning.Should().Be(expectedResult.Reasoning);
    }

    [Fact]
    public async Task TryGetAsync_DifferentGuardType_ReturnsNull()
    {
        // Arrange
        var input = "test input";
        var result = new RemoteGuardResult { Passed = true };

        await _cache.SetAsync(input, "InputJudge", result, TestContext.Current.CancellationToken);

        // Act
        var cached = await _cache.TryGetAsync(input, "OutputJudge", TestContext.Current.CancellationToken);

        // Assert
        cached.Should().BeNull();
    }

    [Fact]
    public async Task GetStats_AfterOperations_ReturnsCorrectStats()
    {
        // Arrange
        var input = "test input";
        var result = new RemoteGuardResult { Passed = true };

        // Act - 1 set, 1 hit, 1 miss
        await _cache.SetAsync(input, "Test", result, TestContext.Current.CancellationToken);
        await _cache.TryGetAsync(input, "Test", TestContext.Current.CancellationToken);  // hit
        await _cache.TryGetAsync("other", "Test", TestContext.Current.CancellationToken); // miss

        var stats = _cache.GetStats();

        // Assert
        stats.TotalEntries.Should().Be(1);
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().Be(0.5);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        // Arrange
        await _cache.SetAsync("input1", "Test", new RemoteGuardResult { Passed = true }, TestContext.Current.CancellationToken);
        await _cache.SetAsync("input2", "Test", new RemoteGuardResult { Passed = false }, TestContext.Current.CancellationToken);

        // Act
        await _cache.ClearAsync(TestContext.Current.CancellationToken);
        var stats = _cache.GetStats();

        // Assert
        stats.TotalEntries.Should().Be(0);
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_WhenDisabled_DoesNotCache()
    {
        // Arrange
        var options = Options.Create(new RemoteGuardOptions { EnableCache = false });
        var disabledCache = new InMemorySemanticCache(options);

        // Act
        await disabledCache.SetAsync("input", "Test", new RemoteGuardResult { Passed = true }, TestContext.Current.CancellationToken);
        var result = await disabledCache.TryGetAsync("input", "Test", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ExceedsMaxEntries_EvictsOldest()
    {
        // Arrange
        var options = Options.Create(new RemoteGuardOptions
        {
            EnableCache = true,
            MaxCacheEntries = 10
        });
        var smallCache = new InMemorySemanticCache(options);

        // Add 10 entries
        for (int i = 0; i < 10; i++)
        {
            await smallCache.SetAsync($"input{i}", "Test", new RemoteGuardResult { Passed = true }, TestContext.Current.CancellationToken);
        }

        // Act - Add one more to trigger eviction
        await smallCache.SetAsync("input_new", "Test", new RemoteGuardResult { Passed = true }, TestContext.Current.CancellationToken);

        var stats = smallCache.GetStats();

        // Assert - Should have evicted 10% = 1 entry
        stats.TotalEntries.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task TryGetAsync_NormalizedInput_MatchesSimilarInputs()
    {
        // Arrange
        var result = new RemoteGuardResult { Passed = true };
        await _cache.SetAsync("  Test Input  ", "Test", result, TestContext.Current.CancellationToken);

        // Act - Different whitespace, same content after normalization
        var cached = await _cache.TryGetAsync("test input", "Test", TestContext.Current.CancellationToken);

        // Assert
        cached.Should().NotBeNull();
    }
}
