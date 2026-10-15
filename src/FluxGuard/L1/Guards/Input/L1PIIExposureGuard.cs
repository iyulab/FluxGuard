using FluxGuard.Abstractions;
using FluxGuard.Configuration;
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
    private readonly PIIActionPolicy _policy;

    public string Name => "L1PIIExposure";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 200;

    public L1PIIExposureGuard(
        IPatternRegistry registry,
        bool isEnabled = true,
        IReadOnlyList<string>? enabledLanguages = null,
        IReadOnlyDictionary<string, PIIAction>? actions = null)
    {
        _engine = new PatternEngine(registry);
        IsEnabled = isEnabled;
        _enabledLanguages = enabledLanguages ?? ["en", "ko", "ja", "zh", "es", "fr", "de", "pt", "ru", "ar"];
        // Per-pattern overrides; a pattern not named keeps its severity default (Critical blocks, everything else flags).
        _policy = new PIIActionPolicy(actions, Severity.Critical, "InputGuardOptions.PIIExposureActions");

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

        var allMatches = new List<PatternMatch>();

        foreach (var category in categories)
        {
            allMatches.AddRange(_engine.Match(input, category));
        }

        var (blocking, reported) = _policy.Decide(allMatches);
        if (reported.Count == 0)
        {
            return ValueTask.FromResult(GuardCheckResult.Safe);
        }

        // A pattern whose action is Block (by default Critical PII: private keys, credit cards, SSN) -> refuse
        if (blocking is not null)
        {
            return ValueTask.FromResult(GuardCheckResult.Block(
                score: blocking.Confidence,
                severity: blocking.Severity,
                pattern: blocking.PatternName,
                matchedText: MaskPII(blocking.MatchedText),
                details: blocking.Severity >= Severity.Critical
                    ? $"Critical PII detected: {blocking.PatternName}"
                    : $"PII detected: {blocking.PatternName}"));
        }

        // Everything else is reported, not refused: the check can be flagged but not blocked
        var top = reported.MaxBy(m => m.Severity)!;
        return ValueTask.FromResult(new GuardCheckResult
        {
            Passed = true,
            Score = reported.Max(m => m.Confidence),
            Severity = top.Severity,
            Pattern = top.PatternName,
            MatchedText = top.Severity >= Severity.High ? MaskPII(top.MatchedText) : null,
            Details = top.Severity >= Severity.High
                ? $"PII detected (flagged): {top.PatternName}"
                : $"Minor PII detected: {reported.Count} instance(s)"
        });
    }

    private static string MaskPII(string text) => PIIActionPolicy.Mask(text);
}
