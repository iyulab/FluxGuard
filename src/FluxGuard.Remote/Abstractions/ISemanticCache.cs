namespace FluxGuard.Remote.Abstractions;

/// <summary>
/// Semantic cache for L3 remote guard results
/// Reduces LLM API calls by caching similar inputs
/// </summary>
public interface ISemanticCache
{
    /// <summary>
    /// Try to get cached result for input
    /// </summary>
    /// <param name="input">Input text</param>
    /// <param name="guardType">Guard type for cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached result if found, null otherwise</returns>
    ValueTask<RemoteGuardResult?> TryGetAsync(
        string input,
        string guardType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache a result for input
    /// </summary>
    /// <param name="input">Input text</param>
    /// <param name="guardType">Guard type for cache key</param>
    /// <param name="result">Result to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask SetAsync(
        string input,
        string guardType,
        RemoteGuardResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cached entries
    /// </summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    CacheStats GetStats();
}

/// <summary>
/// Cache statistics
/// </summary>
public sealed record CacheStats
{
    /// <summary>
    /// Total entries in cache
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    /// Total cache hits
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Total cache misses
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Hit rate (0.0 ~ 1.0)
    /// </summary>
    public double HitRate => Hits + Misses > 0
        ? (double)Hits / (Hits + Misses)
        : 0.0;

    /// <summary>
    /// Estimated memory usage in bytes
    /// </summary>
    public long MemoryBytes { get; init; }
}
