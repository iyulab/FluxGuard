using FluxGuard.Abstractions;
using FluxGuard.Configuration;
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
    private readonly PIIActionPolicy _policy;

    public string Name => "L1PIILeakage";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 100;

    public L1PIILeakageGuard(
        IPatternRegistry registry,
        bool isEnabled = true,
        IReadOnlyList<string>? enabledLanguages = null,
        IReadOnlyDictionary<string, PIIAction>? actions = null)
    {
        _engine = new PatternEngine(registry);
        IsEnabled = isEnabled;
        _enabledLanguages = enabledLanguages ?? ["en", "ko", "ja", "zh", "es", "fr", "de", "pt", "ru", "ar"];
        // Per-pattern overrides; a pattern not named keeps its severity default (High and Critical block, everything else flags).
        _policy = new PIIActionPolicy(actions, Severity.High, "OutputGuardOptions.PIILeakageActions");

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

        var allMatches = new List<PatternMatch>();

        foreach (var category in categories)
        {
            allMatches.AddRange(_engine.Match(output, category));
        }

        var (blocking, reported) = _policy.Decide(allMatches);
        if (reported.Count == 0)
        {
            return ValueTask.FromResult(GuardCheckResult.Safe);
        }

        // A pattern whose action is Block (by default High and Critical PII - output is more sensitive than input) -> refuse
        if (blocking is not null)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: blocking.Confidence,
                severity: blocking.Severity,
                pattern: blocking.PatternName,
                matchedText: MaskPII(blocking.MatchedText),
                details: blocking.Severity >= Severity.Critical
                    ? $"Critical PII leakage detected in output: {blocking.PatternName}"
                    : $"PII leakage detected in output: {blocking.PatternName}"));
        }

        // Everything else is reported, not refused: the check can be flagged but not blocked
        var top = reported.MaxBy(m => m.Severity)!;
        return ValueTask.FromResult(new GuardCheckResult
        {
            Passed = true,
            Score = reported.Max(m => m.Confidence),
            Severity = top.Severity,
            Pattern = top.PatternName,
            Details = $"Minor PII in output: {reported.Count} instance(s)"
        });
    }

    private static string MaskPII(string text) => PIIActionPolicy.Mask(text);
}
