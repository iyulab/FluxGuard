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
/// <remarks>
/// An encoding alone is not an attack - a hash, a token or a data URI is Base64 too - so the guard judges what the
/// encoded text says: it decodes the spans it can read (Base64, hexadecimal, Unicode escapes, HTML entities,
/// percent-encoding, and ROT13 or reversed text when the input asks for them), runs the prompt-injection and jailbreak
/// guards on the decoded text, and blocks or escalates exactly as they would for the same words typed plainly. Plain
/// encoded content that says nothing hostile passes.
/// </remarks>
public sealed class L1EncodingBypassGuard : IInputGuard
{
    private readonly PatternEngine _engine;
    private readonly L1PromptInjectionGuard _injectionGuard;
    private readonly L1JailbreakGuard _jailbreakGuard;
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
        : this(registry, isEnabled, invisibleCharThreshold, homoglyphThreshold, payloadEscalationThreshold: 0.5)
    {
    }

    /// <param name="registry">Pattern registry shared with the other L1 guards.</param>
    /// <param name="isEnabled">Whether the guard runs.</param>
    /// <param name="invisibleCharThreshold">Invisible characters at which the input is blocked.</param>
    /// <param name="homoglyphThreshold">Homoglyph characters at which the input is blocked.</param>
    /// <param name="payloadEscalationThreshold">Confidence at which an injection or jailbreak pattern found in decoded
    /// text escalates (the prompt-injection and jailbreak guards' own escalation threshold; the strict preset lowers it).</param>
    public L1EncodingBypassGuard(
        IPatternRegistry registry,
        bool isEnabled,
        int invisibleCharThreshold,
        int homoglyphThreshold,
        double payloadEscalationThreshold)
    {
        _engine = new PatternEngine(registry);
        _injectionGuard = new L1PromptInjectionGuard(registry, true, payloadEscalationThreshold);
        _jailbreakGuard = new L1JailbreakGuard(registry, true, payloadEscalationThreshold);
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

    public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        var input = context.OriginalInput;

        // Check for excessive invisible characters
        var invisibleCount = ZeroWidthFilter.CountInvisibleCharacters(input);
        if (invisibleCount >= _invisibleCharThreshold)
        {
            return GuardCheckResult.Block(
                score: Math.Min(1.0, invisibleCount / 20.0),
                severity: Severity.High,
                pattern: "InvisibleCharacters",
                matchedText: $"[{invisibleCount} invisible characters]",
                details: $"Excessive invisible characters detected: {invisibleCount}");
        }

        // Check for excessive homoglyphs
        var homoglyphCount = HomoglyphDetector.CountHomoglyphs(input);
        if (homoglyphCount >= _homoglyphThreshold)
        {
            return GuardCheckResult.Block(
                score: Math.Min(1.0, homoglyphCount / 30.0),
                severity: Severity.High,
                pattern: "Homoglyphs",
                matchedText: $"[{homoglyphCount} homoglyphs]",
                details: $"Excessive homoglyph characters detected: {homoglyphCount}");
        }

        // Check for encoding patterns
        var normalizedInput = string.IsNullOrEmpty(context.NormalizedInput)
            ? input
            : context.NormalizedInput;

        // What the encoded text says decides the verdict (see the class remarks).
        var payload = await CheckEncodedPayloadsAsync(input, normalizedInput).ConfigureAwait(false);
        if (payload is { Passed: false })
        {
            return payload;
        }

        var match = _engine.FirstMatch(normalizedInput, EncodingPatterns.Category);

        // Encoding detection is typically medium severity
        // Flag for review but allow through with escalation
        if (match is { Confidence: >= 0.8 })
        {
            return GuardCheckResult.Escalate(
                score: match.Confidence,
                pattern: match.PatternName,
                details: $"Potential encoding bypass: {match.PatternName}");
        }

        return payload ?? GuardCheckResult.Safe;
    }

    /// <summary>
    /// Decodes the readable encoded spans of the input and judges each with the injection and jailbreak guards. Returns
    /// a block as soon as one is found, otherwise the first escalation, otherwise null.
    /// </summary>
    private async ValueTask<GuardCheckResult?> CheckEncodedPayloadsAsync(string input, string normalizedInput)
    {
        // The encoding-request patterns (not only the first match) say whether ROT13 or reversed text was asked for.
        var requests = _engine.Match(normalizedInput, EncodingPatterns.Category);
        var rot13 = requests.Any(m => m.PatternName == "ROT13Cipher");
        var reverse = requests.Any(m => m.PatternName == "ReverseText");

        // Base64 is case-sensitive: decode the input as sent (without zero-width characters), not a case-folded copy.
        var decoded = EncodedPayloadDecoder.Decode(ZeroWidthFilter.Filter(input), rot13, reverse);

        GuardCheckResult? escalation = null;
        foreach (var candidate in decoded.Candidates)
        {
            var candidateContext = new GuardContext { OriginalInput = HomoglyphDetector.Normalize(candidate.Text) };

            foreach (IInputGuard guard in new IInputGuard[] { _injectionGuard, _jailbreakGuard })
            {
                var result = await guard.CheckAsync(candidateContext).ConfigureAwait(false);
                if (!result.Passed)
                {
                    return GuardCheckResult.Block(
                        result.Score,
                        result.Severity,
                        pattern: $"{candidate.Encoding}:{result.Pattern}",
                        matchedText: result.MatchedText,
                        details: $"Hostile text hidden in {candidate.Encoding}: {result.Details}");
                }

                if (result.NeedsEscalation && escalation is null)
                {
                    escalation = GuardCheckResult.Escalate(
                        result.Score,
                        pattern: $"{candidate.Encoding}:{result.Pattern}",
                        details: $"Suspicious text hidden in {candidate.Encoding}: {result.Details}");
                }
            }
        }

        // Input the guard could not read in full must not read as clean.
        if (escalation is null && decoded.TimedOut)
        {
            escalation = GuardCheckResult.Escalate(
                score: 0.5,
                pattern: "EncodedPayloadTimeout",
                details: "Decoding the input timed out");
        }

        if (escalation is null && decoded.Truncated)
        {
            escalation = GuardCheckResult.Escalate(
                score: 0.5,
                pattern: "EncodedPayloadNotFullyRead",
                details: "The input has more encoded content than the guard reads in one check");
        }

        return escalation;
    }
}
