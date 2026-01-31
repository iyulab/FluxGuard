using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;
using FluxGuard.L1.Patterns.PII;

namespace FluxGuard.L1.Guards.Input;

/// <summary>
/// L1 PII Exposure Guard
/// Detects PII in input that shouldn't be sent to LLM
/// </summary>
public sealed class L1PIIExposureGuard : IInputGuard
{
    private readonly PatternEngine _engine;
    private readonly IReadOnlyList<string> _enabledLanguages;

    public string Name => "L1PIIExposure";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 200;

    public L1PIIExposureGuard(
        IPatternRegistry registry,
        bool isEnabled = true,
        IReadOnlyList<string>? enabledLanguages = null)
    {
        _engine = new PatternEngine(registry);
        IsEnabled = isEnabled;
        _enabledLanguages = enabledLanguages ?? ["en", "ko", "ja", "zh", "es", "fr", "de", "pt", "ru", "ar"];

        // Register common PII patterns
        if (!registry.HasCategory(PIIPatterns.Category))
        {
            foreach (var pattern in PIIPatterns.GetPatterns())
            {
                registry.Register(PIIPatterns.Category, pattern);
            }
        }

        // Register language-specific patterns
        RegisterLanguagePatterns(registry);
    }

    private void RegisterLanguagePatterns(IPatternRegistry registry)
    {
        if (_enabledLanguages.Contains("ko") && !registry.HasCategory(KoreanPIIPatterns.Category))
        {
            foreach (var pattern in KoreanPIIPatterns.GetPatterns())
            {
                registry.Register(KoreanPIIPatterns.Category, pattern);
            }
        }

        if (_enabledLanguages.Contains("ja") && !registry.HasCategory(JapanesePIIPatterns.Category))
        {
            foreach (var pattern in JapanesePIIPatterns.GetPatterns())
            {
                registry.Register(JapanesePIIPatterns.Category, pattern);
            }
        }

        if (_enabledLanguages.Contains("en") && !registry.HasCategory(USPIIPatterns.Category))
        {
            foreach (var pattern in USPIIPatterns.GetPatterns())
            {
                registry.Register(USPIIPatterns.Category, pattern);
            }
        }
    }

    public ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        var input = string.IsNullOrEmpty(context.NormalizedInput)
            ? context.OriginalInput
            : context.NormalizedInput;

        // Check all PII categories
        var categories = new List<string> { PIIPatterns.Category };

        if (_enabledLanguages.Contains("ko"))
            categories.Add(KoreanPIIPatterns.Category);
        if (_enabledLanguages.Contains("ja"))
            categories.Add(JapanesePIIPatterns.Category);
        if (_enabledLanguages.Contains("en"))
            categories.Add(USPIIPatterns.Category);

        PatternMatch? criticalMatch = null;
        var allMatches = new List<PatternMatch>();

        foreach (var category in categories)
        {
            var matches = _engine.Match(input, category);
            allMatches.AddRange(matches);

            // Find critical match
            var critical = matches.FirstOrDefault(m => m.Severity >= Severity.Critical);
            if (critical is not null && criticalMatch is null)
            {
                criticalMatch = critical;
            }
        }

        if (allMatches.Count == 0)
        {
            return ValueTask.FromResult(GuardCheckResult.Safe);
        }

        // Critical PII (private keys, credit cards, SSN) -> block
        if (criticalMatch is not null)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: criticalMatch.Confidence,
                severity: criticalMatch.Severity,
                pattern: criticalMatch.PatternName,
                matchedText: MaskPII(criticalMatch.MatchedText),
                details: $"Critical PII detected: {criticalMatch.PatternName}"));
        }

        // High severity PII -> warn but may allow
        var highMatch = allMatches.FirstOrDefault(m => m.Severity >= Severity.High);
        if (highMatch is not null)
        {
            return ValueTask.FromResult(new GuardCheckResult
            {
                Passed = true, // Flag but don't block by default
                Score = highMatch.Confidence,
                Severity = highMatch.Severity,
                Pattern = highMatch.PatternName,
                MatchedText = MaskPII(highMatch.MatchedText),
                Details = $"PII detected (flagged): {highMatch.PatternName}"
            });
        }

        // Lower severity PII -> just track
        return ValueTask.FromResult(new GuardCheckResult
        {
            Passed = true,
            Score = allMatches.Max(m => m.Confidence),
            Severity = allMatches.Max(m => m.Severity),
            Pattern = allMatches.First().PatternName,
            Details = $"Minor PII detected: {allMatches.Count} instance(s)"
        });
    }

    private static string MaskPII(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 4)
            return "****";

        // Show first 2 and last 2 characters
        return text[..2] + new string('*', Math.Min(text.Length - 4, 10)) + text[^2..];
    }
}
