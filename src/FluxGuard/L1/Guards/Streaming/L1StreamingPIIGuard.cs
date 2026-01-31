using System.Text.RegularExpressions;
using FluxGuard.Core;
using FluxGuard.Streaming;

namespace FluxGuard.L1.Guards.Streaming;

/// <summary>
/// L1 Streaming PII guard for real-time PII detection in output streams
/// </summary>
public sealed partial class L1StreamingPIIGuard : IStreamingGuard
{
    private readonly bool _suppressPII;
    private readonly string _replacementPattern;

    /// <inheritdoc />
    public string Name => "L1StreamingPII";

    /// <inheritdoc />
    public string Layer => "L1";

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <summary>
    /// Create L1 Streaming PII guard
    /// </summary>
    /// <param name="enabled">Whether guard is enabled</param>
    /// <param name="suppressPII">Whether to suppress detected PII (default: true)</param>
    /// <param name="replacementPattern">Pattern for PII replacement (default: "[REDACTED]")</param>
    public L1StreamingPIIGuard(
        bool enabled = true,
        bool suppressPII = true,
        string replacementPattern = "[REDACTED]")
    {
        IsEnabled = enabled;
        _suppressPII = suppressPII;
        _replacementPattern = replacementPattern;
    }

    /// <inheritdoc />
    public ValueTask<TokenValidation> ValidateChunkAsync(
        GuardContext context,
        string chunk,
        string buffer,
        CancellationToken cancellationToken = default)
    {
        // For chunk-level, we do a quick check on accumulated buffer
        // to detect PII that might span multiple chunks
        return ValidateTextAsync(buffer, isChunk: true);
    }

    /// <inheritdoc />
    public ValueTask<TokenValidation> ValidateFinalAsync(
        GuardContext context,
        string fullOutput,
        CancellationToken cancellationToken = default)
    {
        return ValidateTextAsync(fullOutput, isChunk: false);
    }

    private ValueTask<TokenValidation> ValidateTextAsync(string text, bool isChunk)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ValueTask.FromResult(TokenValidation.Safe);
        }

        // Check for various PII patterns
        var (hasPII, pattern, matchedText) = DetectPII(text);

        if (!hasPII)
        {
            return ValueTask.FromResult(TokenValidation.Pass(Name));
        }

        if (_suppressPII)
        {
            // Suppress and replace PII
            return ValueTask.FromResult(TokenValidation.Suppress(
                Name,
                replacement: _replacementPattern,
                pattern: pattern));
        }

        // Terminate if PII detected and not suppressing
        return ValueTask.FromResult(TokenValidation.Terminate(
            Name,
            score: 0.9,
            severity: Severity.High,
            pattern: pattern,
            matchedText: matchedText));
    }

    private static (bool hasPII, string? pattern, string? matchedText) DetectPII(string text)
    {
        // Check email
        var emailMatch = EmailAddressPattern().Match(text);
        if (emailMatch.Success)
        {
            return (true, "email", emailMatch.Value);
        }

        // Check credit card
        var ccMatch = CreditCardPattern().Match(text);
        if (ccMatch.Success)
        {
            return (true, "credit_card", MaskSensitive(ccMatch.Value));
        }

        // Check SSN
        var ssnMatch = SSNPattern().Match(text);
        if (ssnMatch.Success)
        {
            return (true, "ssn", MaskSensitive(ssnMatch.Value));
        }

        // Check phone numbers
        var phoneMatch = PhoneNumberPattern().Match(text);
        if (phoneMatch.Success && phoneMatch.Value.Length >= 10)
        {
            return (true, "phone", MaskSensitive(phoneMatch.Value));
        }

        // Check API keys / secrets
        var apiKeyMatch = APIKeyPattern().Match(text);
        if (apiKeyMatch.Success)
        {
            return (true, "api_key", MaskSensitive(apiKeyMatch.Value));
        }

        return (false, null, null);
    }

    private static string MaskSensitive(string value)
    {
        if (value.Length <= 4)
        {
            return "****";
        }
        return value[..2] + new string('*', value.Length - 4) + value[^2..];
    }

    // Email address pattern
    [GeneratedRegex(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailAddressPattern();

    // Credit card number (major brands)
    [GeneratedRegex(
        @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|6(?:011|5[0-9]{2})[0-9]{12}|(?:2131|1800|35\d{3})\d{11})\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex CreditCardPattern();

    // SSN pattern
    [GeneratedRegex(
        @"\b(?!000|666|9\d{2})\d{3}[-\s]?(?!00)\d{2}[-\s]?(?!0000)\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex SSNPattern();

    // Phone number pattern for streaming (less strict for real-time detection)
    [GeneratedRegex(
        @"(?:\+\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex PhoneNumberPattern();

    // API key (general format)
    [GeneratedRegex(
        @"(?i)(api[_-]?key|apikey|secret[_-]?key|access[_-]?token|auth[_-]?token)\s*[:=]\s*['""]?[a-zA-Z0-9_-]{20,}['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex APIKeyPattern();
}
