using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Patterns.PII;

namespace FluxGuard.L1.Patterns.Generated;

/// <summary>
/// Common PII detection patterns
/// Email, IP address, credit card, etc. - universal patterns
/// </summary>
public static partial class PIIPatterns
{
    public const string Category = "PII";

    /// <summary>
    /// Get common PII patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "PII001",
            Name = "EmailAddress",
            Regex = EmailAddressRegex(),
            Severity = Severity.Medium,
            Confidence = 0.95,
            Description = "Detects email addresses"
        };

        yield return new PatternDefinition
        {
            Id = "PII002",
            Name = "IPAddress",
            Regex = IPAddressRegex(),
            Severity = Severity.Low,
            Confidence = 0.9,
            Description = "Detects IP addresses"
        };

        yield return new PatternDefinition
        {
            Id = "PII003",
            Name = "CreditCard",
            Regex = CreditCardRegex(),
            Validator = PIIChecksums.IsPaymentCardNumber,
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects credit card numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII004",
            Name = "IBAN",
            Regex = IBANRegex(),
            Validator = PIIChecksums.IsIban,
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects IBAN account numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII005",
            Name = "BankAccount",
            Regex = BankAccountRegex(),
            Severity = Severity.High,
            Confidence = 0.7,
            Description = "Detects bank account numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII006",
            Name = "MACAddress",
            Regex = MACAddressRegex(),
            Severity = Severity.Low,
            Confidence = 0.9,
            Description = "Detects MAC addresses"
        };

        yield return new PatternDefinition
        {
            Id = "PII007",
            Name = "PrivateKey",
            Regex = PrivateKeyRegex(),
            Severity = Severity.Critical,
            Confidence = 0.98,
            Description = "Detects private/secret keys"
        };

        yield return new PatternDefinition
        {
            Id = "PII008",
            Name = "APIKey",
            Regex = APIKeyRegex(),
            Severity = Severity.Critical,
            Confidence = 0.9,
            Description = "Detects API keys"
        };

        yield return new PatternDefinition
        {
            Id = "PII009",
            Name = "Password",
            Regex = PasswordRegex(),
            Severity = Severity.Critical,
            Confidence = 0.85,
            Description = "Detects password patterns"
        };

        yield return new PatternDefinition
        {
            Id = "PII010",
            Name = "JWT",
            Regex = JWTRegex(),
            Severity = Severity.High,
            Confidence = 0.95,
            Description = "Detects JWT tokens"
        };
    }

    // Email address
    [GeneratedRegex(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailAddressRegex();

    // IP address (IPv4)
    [GeneratedRegex(
        @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex IPAddressRegex();

    // Credit card number (major brands)
    [GeneratedRegex(
        // 13-19 digits, contiguous or grouped with spaces/hyphens; the issuer prefix and the Luhn check digit are
        // verified by PIIChecksums.IsPaymentCardNumber (a 16-digit order number is not a card number).
        @"\b(?:\d[ -]?){12,18}\d\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CreditCardRegex();

    // IBAN
    // NOTE: keep the tail as a plain bounded class — the previous ([A-Z0-9]?){0,16} was a nested
    // quantifier (ambiguous empty matches -> exponential backtracking paths on near-misses).
    [GeneratedRegex(
        // Contiguous or in the printed groups of four; the check digits are verified by PIIChecksums.IsIban.
        @"\b[A-Z]{2}[0-9]{2}(?: ?[A-Z0-9]{4}){2,7}(?: ?[A-Z0-9]{1,4})?\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex IBANRegex();

    // Bank account number (general)
    [GeneratedRegex(
        @"(?i)(account\s*(number|no\.?|#)?|acct\.?)\s*[:=]?\s*[0-9]{8,20}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex BankAccountRegex();

    // MAC address
    [GeneratedRegex(
        @"\b(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex MACAddressRegex();

    // Private/secret key
    [GeneratedRegex(
        @"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(RSA\s+)?PRIVATE\s+KEY-----|-----BEGIN\s+EC\s+PRIVATE\s+KEY-----[\s\S]*?-----END\s+EC\s+PRIVATE\s+KEY-----|-----BEGIN\s+OPENSSH\s+PRIVATE\s+KEY-----[\s\S]*?-----END\s+OPENSSH\s+PRIVATE\s+KEY-----",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrivateKeyRegex();

    // API key (general format)
    [GeneratedRegex(
        @"(?i)(api[_-]?key|apikey|secret[_-]?key|access[_-]?token|auth[_-]?token)\s*[:=]\s*['""]?[a-zA-Z0-9_-]{20,}['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex APIKeyRegex();

    // Password pattern. 'pw' is language-independent shorthand (the most common label in Korean
    // operations documents) and takes a word boundary of its own, since two letters inside a longer
    // word are not a label; the other labels keep their historical, boundary-free matching.
    [GeneratedRegex(
        @"(?i)(password|passwd|pwd|pass|\bpw)\s*[:=]\s*[^\s]{4,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PasswordRegex();

    // JWT token
    [GeneratedRegex(
        @"eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex JWTRegex();
}
