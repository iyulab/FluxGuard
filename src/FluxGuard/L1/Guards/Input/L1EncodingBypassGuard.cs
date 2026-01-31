using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Normalization;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;

namespace FluxGuard.L1.Guards.Input;

/// <summary>
/// L1 Encoding Bypass Guard
/// Detects encoding-based bypass attempts (Base64, Hex, Unicode, etc.)
/// </summary>
public sealed class L1EncodingBypassGuard : IInputGuard
{
    private readonly PatternEngine _engine;
    private readonly int _invisibleCharThreshold;
    private readonly int _homoglyphThreshold;

    public string Name => "L1EncodingBypass";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 50; // Run early to detect obfuscation

    public L1EncodingBypassGuard(
        IPatternRegistry registry,
        bool isEnabled = true,
        int invisibleCharThreshold = 5,
        int homoglyphThreshold = 10)
    {
        _engine = new PatternEngine(registry);
        IsEnabled = isEnabled;
        _invisibleCharThreshold = invisibleCharThreshold;
        _homoglyphThreshold = homoglyphThreshold;

        // Register patterns if not already registered
        if (!registry.HasCategory(EncodingPatterns.Category))
        {
            foreach (var pattern in EncodingPatterns.GetPatterns())
            {
                registry.Register(EncodingPatterns.Category, pattern);
            }
        }
    }

    public ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        var input = context.OriginalInput;

        // Check for excessive invisible characters
        var invisibleCount = ZeroWidthFilter.CountInvisibleCharacters(input);
        if (invisibleCount >= _invisibleCharThreshold)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: Math.Min(1.0, invisibleCount / 20.0),
                severity: Severity.High,
                pattern: "InvisibleCharacters",
                matchedText: $"[{invisibleCount} invisible characters]",
                details: $"Excessive invisible characters detected: {invisibleCount}"));
        }

        // Check for excessive homoglyphs
        var homoglyphCount = HomoglyphDetector.CountHomoglyphs(input);
        if (homoglyphCount >= _homoglyphThreshold)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: Math.Min(1.0, homoglyphCount / 30.0),
                severity: Severity.High,
                pattern: "Homoglyphs",
                matchedText: $"[{homoglyphCount} homoglyphs]",
                details: $"Excessive homoglyph characters detected: {homoglyphCount}"));
        }

        // Check for encoding patterns
        var normalizedInput = string.IsNullOrEmpty(context.NormalizedInput)
            ? input
            : context.NormalizedInput;

        var match = _engine.FirstMatch(normalizedInput, EncodingPatterns.Category);

        if (match is null)
        {
            return ValueTask.FromResult(GuardCheckResult.Safe);
        }

        // Encoding detection is typically medium severity
        // Flag for review but allow through with escalation
        if (match.Confidence >= 0.8)
        {
            return ValueTask.FromResult(GuardCheckResult.Escalate(
                score: match.Confidence,
                pattern: match.PatternName,
                details: $"Potential encoding bypass: {match.PatternName}"));
        }

        return ValueTask.FromResult(GuardCheckResult.Safe);
    }
}
