using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;

namespace FluxGuard.L1.Guards.Input;

/// <summary>
/// L1 Prompt Injection Guard
/// Regex-based prompt injection detection
/// </summary>
public sealed class L1PromptInjectionGuard : IInputGuard
{
    private readonly PatternEngine _engine;
    private readonly double _escalationThreshold;

    public string Name => "L1PromptInjection";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 100;

    public L1PromptInjectionGuard(
        IPatternRegistry registry,
        bool isEnabled = true,
        double escalationThreshold = 0.5)
    {
        _engine = new PatternEngine(registry);
        IsEnabled = isEnabled;
        _escalationThreshold = escalationThreshold;

        // Register patterns if not already registered
        if (!registry.HasCategory(PromptInjectionPatterns.Category))
        {
            foreach (var pattern in PromptInjectionPatterns.GetPatterns())
            {
                registry.Register(PromptInjectionPatterns.Category, pattern);
            }
        }
    }

    public ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        var input = string.IsNullOrEmpty(context.NormalizedInput)
            ? context.OriginalInput
            : context.NormalizedInput;

        var match = _engine.FirstMatch(input, PromptInjectionPatterns.Category);

        if (match is null)
        {
            return ValueTask.FromResult(GuardCheckResult.Safe);
        }

        // Critical severity -> immediate block
        if (match.Severity >= Severity.Critical)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: match.Confidence,
                severity: match.Severity,
                pattern: match.PatternName,
                matchedText: TruncateForSafety(match.MatchedText),
                details: $"Prompt injection detected: {match.PatternName}"));
        }

        // High severity with high confidence -> block
        if (match.Severity >= Severity.High && match.Confidence >= 0.9)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: match.Confidence,
                severity: match.Severity,
                pattern: match.PatternName,
                matchedText: TruncateForSafety(match.MatchedText),
                details: $"High-confidence prompt injection: {match.PatternName}"));
        }

        // Medium-high confidence -> escalate to L2/L3
        if (match.Confidence >= _escalationThreshold)
        {
            return ValueTask.FromResult(GuardCheckResult.Escalate(
                score: match.Confidence,
                pattern: match.PatternName,
                details: $"Potential prompt injection requires ML verification: {match.PatternName}"));
        }

        return ValueTask.FromResult(GuardCheckResult.Safe);
    }

    private static string TruncateForSafety(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }
}
