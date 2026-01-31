using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Configuration;
using Microsoft.Extensions.Options;

namespace FluxGuard.Remote.Caching;

/// <summary>
/// In-memory semantic cache implementation
/// Uses hash-based exact matching (semantic similarity requires embedding models)
/// </summary>
public sealed class InMemorySemanticCache : ISemanticCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly RemoteGuardOptions _options;
    private long _hits;
    private long _misses;

    /// <summary>
    /// Create in-memory semantic cache
    /// </summary>
    public InMemorySemanticCache(IOptions<RemoteGuardOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public ValueTask<RemoteGuardResult?> TryGetAsync(
        string input,
        string guardType,
        CancellationToken cancellationToken = default)
    {
        var key = ComputeKey(input, guardType);

        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired(_options.CacheTtlSeconds))
            {
                Interlocked.Increment(ref _hits);
                return ValueTask.FromResult<RemoteGuardResult?>(entry.Result);
            }

            // Remove expired entry
            _cache.TryRemove(key, out _);
        }

        Interlocked.Increment(ref _misses);
        return ValueTask.FromResult<RemoteGuardResult?>(null);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(
        string input,
        string guardType,
        RemoteGuardResult result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableCache)
        {
            return ValueTask.CompletedTask;
        }

        var key = ComputeKey(input, guardType);

        // Evict if at capacity
        if (_cache.Count >= _options.MaxCacheEntries)
        {
            EvictOldestEntries();
        }

        _cache[key] = new CacheEntry(result, DateTimeOffset.UtcNow);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public CacheStats GetStats()
    {
        var entries = _cache.ToArray();
        var memoryBytes = entries.Sum(e => EstimateEntrySize(e.Value));

        return new CacheStats
        {
            TotalEntries = entries.Length,
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            MemoryBytes = memoryBytes
        };
    }

    private static string ComputeKey(string input, string guardType)
    {
        // Normalize input for better cache hits
        var normalized = input.Trim().ToLowerInvariant();
        var combined = $"{guardType}:{normalized}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hashBytes);
    }

    private void EvictOldestEntries()
    {
        // Simple eviction: remove 10% of entries (oldest first)
        var entriesToRemove = _cache
            .OrderBy(kvp => kvp.Value.CreatedAt)
            .Take(_options.MaxCacheEntries / 10)
            .ToList();

        foreach (var entry in entriesToRemove)
        {
            _cache.TryRemove(entry.Key, out _);
        }
    }

    private static long EstimateEntrySize(CacheEntry entry)
    {
        // Rough estimate: key (64 bytes) + result object (~200 bytes) + overhead
        return 64 + 200 + (entry.Result.Reasoning?.Length ?? 0) * 2;
    }

    private sealed record CacheEntry(RemoteGuardResult Result, DateTimeOffset CreatedAt)
    {
        public bool IsExpired(int ttlSeconds) =>
            DateTimeOffset.UtcNow - CreatedAt > TimeSpan.FromSeconds(ttlSeconds);
    }
}
