using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;
using FluxGuard.L1.Patterns.PII;

namespace FluxGuard.L1.Guards.Output;

/// <summary>
/// L1 PII Leakage Guard
/// Detects PII in LLM output that shouldn't be exposed
/// </summary>
public sealed class L1PIILeakageGuard : IOutputGuard
{
    private readonly PatternEngine _engine;
    private readonly IReadOnlyList<string> _enabledLanguages;

    public string Name => "L1PIILeakage";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 100;

    public L1PIILeakageGuard(
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

    public ValueTask<GuardCheckResult> CheckAsync(GuardContext context, string output)
    {
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
            var matches = _engine.Match(output, category);
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

        // Critical PII in output -> block
        if (criticalMatch is not null)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: criticalMatch.Confidence,
                severity: criticalMatch.Severity,
                pattern: criticalMatch.PatternName,
                matchedText: MaskPII(criticalMatch.MatchedText),
                details: $"Critical PII leakage detected in output: {criticalMatch.PatternName}"));
        }

        // High severity PII in output -> block (output is more sensitive than input)
        var highMatch = allMatches.FirstOrDefault(m => m.Severity >= Severity.High);
        if (highMatch is not null)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: highMatch.Confidence,
                severity: highMatch.Severity,
                pattern: highMatch.PatternName,
                matchedText: MaskPII(highMatch.MatchedText),
                details: $"PII leakage detected in output: {highMatch.PatternName}"));
        }

        // Medium severity -> flag but allow
        return ValueTask.FromResult(new GuardCheckResult
        {
            Passed = true,
            Score = allMatches.Max(m => m.Confidence),
            Severity = allMatches.Max(m => m.Severity),
            Pattern = allMatches.First().PatternName,
            Details = $"Minor PII in output: {allMatches.Count} instance(s)"
        });
    }

    private static string MaskPII(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 4)
            return "****";

        return text[..2] + new string('*', Math.Min(text.Length - 4, 10)) + text[^2..];
    }
}
