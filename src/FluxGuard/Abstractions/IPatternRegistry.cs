using System.Text.RegularExpressions;

namespace FluxGuard.Abstractions;

/// <summary>
/// Pattern registry interface
/// </summary>
public interface IPatternRegistry
{
    /// <summary>
    /// Register pattern by category
    /// </summary>
    /// <param name="category">Pattern category</param>
    /// <param name="pattern">Pattern to register</param>
    void Register(string category, PatternDefinition pattern);

    /// <summary>
    /// Get patterns by category
    /// </summary>
    /// <param name="category">Pattern category</param>
    /// <returns>Pattern list</returns>
    IReadOnlyList<PatternDefinition> GetPatterns(string category);

    /// <summary>
    /// Get all patterns
    /// </summary>
    /// <returns>Pattern map by category</returns>
    IReadOnlyDictionary<string, IReadOnlyList<PatternDefinition>> GetAllPatterns();

    /// <summary>
    /// Check if category exists
    /// </summary>
    /// <param name="category">Pattern category</param>
    /// <returns>Whether exists</returns>
    bool HasCategory(string category);
}

/// <summary>
/// Pattern definition
/// </summary>
public sealed record PatternDefinition
{
    /// <summary>
    /// Pattern ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Pattern name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Compiled regex
    /// </summary>
    public required Regex Regex { get; init; }

    /// <summary>
    /// Severity
    /// </summary>
    public Core.Severity Severity { get; init; } = Core.Severity.Medium;

    /// <summary>
    /// Confidence score (0.0 ~ 1.0)
    /// </summary>
    public double Confidence { get; init; } = 0.8;

    /// <summary>
    /// Optional check on the matched text, for formats the regular expression cannot verify by itself (a check digit,
    /// a checksum). A match it rejects is dropped. Null keeps every match.
    /// </summary>
    public Func<string, bool>? Validator { get; init; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether enabled
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
