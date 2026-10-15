using FluxGuard.Core;
using FluxGuard.Configuration;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;
using FluxGuard.L1.Patterns.PII;

namespace FluxGuard.L1.Guards;

/// <summary>
/// The action a PII guard takes for each pattern: a consumer override by pattern name, otherwise
/// <see cref="PIIAction.Block"/> at or above the guard's blocking severity and <see cref="PIIAction.Flag"/> below it.
/// </summary>
internal sealed class PIIActionPolicy
{
    private static readonly Lazy<IReadOnlySet<string>> KnownNames = new(() =>
        PIIPatterns.GetPatterns()
            .Concat(KoreanPIIPatterns.GetPatterns())
            .Concat(JapanesePIIPatterns.GetPatterns())
            .Concat(USPIIPatterns.GetPatterns())
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, PIIAction> _overrides;
    private readonly Severity _blockAt;

    /// <summary>
    /// Validates <paramref name="overrides"/>: every key must name a PII pattern and every value a defined action, so a
    /// misspelt key fails here instead of silently changing nothing.
    /// </summary>
    public PIIActionPolicy(IEnumerable<KeyValuePair<string, PIIAction>>? overrides, Severity blockAt, string optionName)
    {
        _blockAt = blockAt;
        _overrides = new Dictionary<string, PIIAction>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, action) in overrides ?? [])
        {
            if (!KnownNames.Value.Contains(name))
            {
                throw new ArgumentException(
                    $"{optionName} names unknown PII pattern '{name}'. Known patterns: " +
                    string.Join(", ", KnownNames.Value.Order(StringComparer.Ordinal)) + ".",
                    optionName);
            }

            if (!Enum.IsDefined(action))
            {
                throw new ArgumentOutOfRangeException(
                    optionName, action, $"{optionName}['{name}'] is not a defined PIIAction.");
            }

            _overrides[name] = action;
        }
    }

    public PIIAction ActionFor(PatternMatch match) =>
        _overrides.TryGetValue(match.PatternName, out var action)
            ? action
            : match.Severity >= _blockAt ? PIIAction.Block : PIIAction.Flag;

    /// <summary>
    /// Splits <paramref name="matches"/> by action: the match that makes the guard refuse (highest severity, then
    /// confidence), or null, and the matches it reports when it passes. Ignored matches are in neither.
    /// </summary>
    public (PatternMatch? Blocking, IReadOnlyList<PatternMatch> Reported) Decide(IEnumerable<PatternMatch> matches)
    {
        PatternMatch? blocking = null;
        var reported = new List<PatternMatch>();

        foreach (var match in matches)
        {
            switch (ActionFor(match))
            {
                case PIIAction.Ignore:
                    continue;
                case PIIAction.Block:
                    if (blocking is null
                        || match.Severity > blocking.Severity
                        || (match.Severity == blocking.Severity && match.Confidence > blocking.Confidence))
                    {
                        blocking = match;
                    }
                    break;
            }

            reported.Add(match);
        }

        return (blocking, reported);
    }

    public static string Mask(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 4)
            return "****";

        // Show first 2 and last 2 characters
        return text[..2] + new string('*', Math.Min(text.Length - 4, 10)) + text[^2..];
    }
}
