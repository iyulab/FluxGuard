using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns;

/// <summary>
/// Pattern matching engine
/// High-performance regex-based pattern detection
/// </summary>
public sealed class PatternEngine
{
    private readonly IPatternRegistry _registry;
    private readonly TimeSpan _matchTimeout;

    public PatternEngine(IPatternRegistry registry, TimeSpan? matchTimeout = null)
    {
        _registry = registry;
        _matchTimeout = matchTimeout ?? TimeSpan.FromMilliseconds(100);
    }

    /// <summary>
    /// Perform pattern matching on text
    /// </summary>
    /// <param name="input">Text to check</param>
    /// <param name="category">Pattern category</param>
    /// <returns>List of matches</returns>
    public IReadOnlyList<PatternMatch> Match(string input, string category)
    {
        if (string.IsNullOrEmpty(input) || !_registry.HasCategory(category))
            return [];

        var patterns = _registry.GetPatterns(category);
        var matches = new List<PatternMatch>();

        foreach (var pattern in patterns)
        {
            try
            {
                var regexMatches = pattern.Regex.Matches(input);

                foreach (Match match in regexMatches)
                {
                    matches.Add(new PatternMatch
                    {
                        PatternId = pattern.Id,
                        PatternName = pattern.Name,
                        MatchedText = match.Value,
                        StartIndex = match.Index,
                        Length = match.Length,
                        Confidence = pattern.Confidence,
                        Severity = pattern.Severity
                    });
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip this pattern on timeout
                matches.Add(new PatternMatch
                {
                    PatternId = pattern.Id,
                    PatternName = pattern.Name,
                    MatchedText = "[TIMEOUT]",
                    StartIndex = 0,
                    Length = 0,
                    Confidence = 0.5,
                    Severity = Severity.Medium,
                    TimedOut = true
                });
            }
        }

        return matches;
    }

    /// <summary>
    /// Perform pattern matching across all categories
    /// </summary>
    /// <param name="input">Text to check</param>
    /// <returns>Matches by category</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<PatternMatch>> MatchAll(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new Dictionary<string, IReadOnlyList<PatternMatch>>();

        var allPatterns = _registry.GetAllPatterns();
        var results = new Dictionary<string, IReadOnlyList<PatternMatch>>();

        foreach (var category in allPatterns.Keys)
        {
            var matches = Match(input, category);
            if (matches.Count > 0)
            {
                results[category] = matches;
            }
        }

        return results;
    }

    /// <summary>
    /// Check if any pattern matches (fast check)
    /// </summary>
    /// <param name="input">Text to check</param>
    /// <param name="category">Pattern category</param>
    /// <returns>Whether any pattern matches</returns>
    public bool IsMatch(string input, string category)
    {
        if (string.IsNullOrEmpty(input) || !_registry.HasCategory(category))
            return false;

        var patterns = _registry.GetPatterns(category);

        foreach (var pattern in patterns)
        {
            try
            {
                if (pattern.Regex.IsMatch(input))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // Timeout is treated as potential threat
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get first matching pattern
    /// </summary>
    /// <param name="input">Text to check</param>
    /// <param name="category">Pattern category</param>
    /// <returns>First match (null if none)</returns>
    public PatternMatch? FirstMatch(string input, string category)
    {
        if (string.IsNullOrEmpty(input) || !_registry.HasCategory(category))
            return null;

        var patterns = _registry.GetPatterns(category);

        foreach (var pattern in patterns)
        {
            try
            {
                var match = pattern.Regex.Match(input);
                if (match.Success)
                {
                    return new PatternMatch
                    {
                        PatternId = pattern.Id,
                        PatternName = pattern.Name,
                        MatchedText = match.Value,
                        StartIndex = match.Index,
                        Length = match.Length,
                        Confidence = pattern.Confidence,
                        Severity = pattern.Severity
                    };
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return new PatternMatch
                {
                    PatternId = pattern.Id,
                    PatternName = pattern.Name,
                    MatchedText = "[TIMEOUT]",
                    StartIndex = 0,
                    Length = 0,
                    Confidence = 0.5,
                    Severity = Severity.Medium,
                    TimedOut = true
                };
            }
        }

        return null;
    }
}

/// <summary>
/// Pattern match result
/// </summary>
public sealed record PatternMatch
{
    /// <summary>Pattern ID</summary>
    public required string PatternId { get; init; }

    /// <summary>Pattern name</summary>
    public required string PatternName { get; init; }

    /// <summary>Matched text</summary>
    public required string MatchedText { get; init; }

    /// <summary>Start index</summary>
    public required int StartIndex { get; init; }

    /// <summary>Match length</summary>
    public required int Length { get; init; }

    /// <summary>Confidence score</summary>
    public double Confidence { get; init; }

    /// <summary>Severity</summary>
    public Severity Severity { get; init; }

    /// <summary>Whether timeout occurred</summary>
    public bool TimedOut { get; init; }
}
