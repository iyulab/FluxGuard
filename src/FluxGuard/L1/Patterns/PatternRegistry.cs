using System.Collections.Concurrent;
using FluxGuard.Abstractions;

namespace FluxGuard.L1.Patterns;

/// <summary>
/// Pattern registry implementation
/// Thread-safe pattern management
/// </summary>
public sealed class PatternRegistry : IPatternRegistry
{
    private readonly ConcurrentDictionary<string, List<PatternDefinition>> _patterns = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Register(string category, PatternDefinition pattern)
    {
        _patterns.AddOrUpdate(
            category,
            _ => [pattern],
            (_, list) =>
            {
                lock (_lock)
                {
                    if (!list.Exists(p => p.Id == pattern.Id))
                    {
                        list.Add(pattern);
                    }
                }
                return list;
            });
    }

    /// <summary>
    /// Register multiple patterns at once
    /// </summary>
    /// <param name="category">Pattern category</param>
    /// <param name="patterns">Patterns to register</param>
    public void RegisterMany(string category, IEnumerable<PatternDefinition> patterns)
    {
        foreach (var pattern in patterns)
        {
            Register(category, pattern);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PatternDefinition> GetPatterns(string category)
    {
        return _patterns.TryGetValue(category, out var patterns)
            ? patterns.Where(p => p.IsEnabled).ToList()
            : [];
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<PatternDefinition>> GetAllPatterns()
    {
        return _patterns.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<PatternDefinition>)kvp.Value.Where(p => p.IsEnabled).ToList());
    }

    /// <inheritdoc />
    public bool HasCategory(string category)
    {
        return _patterns.ContainsKey(category);
    }

    /// <summary>
    /// Disable a pattern
    /// </summary>
    /// <param name="category">Pattern category</param>
    /// <param name="patternId">Pattern ID</param>
    public void DisablePattern(string category, string patternId)
    {
        if (_patterns.TryGetValue(category, out var patterns))
        {
            lock (_lock)
            {
                var pattern = patterns.Find(p => p.Id == patternId);
                if (pattern is not null)
                {
                    var index = patterns.IndexOf(pattern);
                    patterns[index] = pattern with { IsEnabled = false };
                }
            }
        }
    }

    /// <summary>
    /// Remove a category
    /// </summary>
    /// <param name="category">Pattern category</param>
    public void RemoveCategory(string category)
    {
        _patterns.TryRemove(category, out _);
    }

    /// <summary>
    /// Clear all patterns
    /// </summary>
    public void Clear()
    {
        _patterns.Clear();
    }
}
