using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

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
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects credit card numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII004",
            Name = "IBAN",
            Regex = IBANRegex(),
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
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailAddressRegex();

    // IP address (IPv4)
    [GeneratedRegex(
        @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex IPAddressRegex();

    // Credit card number (major brands)
    [GeneratedRegex(
        @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|6(?:011|5[0-9]{2})[0-9]{12}|(?:2131|1800|35\d{3})\d{11})\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex CreditCardRegex();

    // IBAN
    [GeneratedRegex(
        @"\b[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex IBANRegex();

    // Bank account number (general)
    [GeneratedRegex(
        @"(?i)(account\s*(number|no\.?|#)?|acct\.?)\s*[:=]?\s*[0-9]{8,20}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex BankAccountRegex();

    // MAC address
    [GeneratedRegex(
        @"\b(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex MACAddressRegex();

    // Private/secret key
    [GeneratedRegex(
        @"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(RSA\s+)?PRIVATE\s+KEY-----|-----BEGIN\s+EC\s+PRIVATE\s+KEY-----[\s\S]*?-----END\s+EC\s+PRIVATE\s+KEY-----|-----BEGIN\s+OPENSSH\s+PRIVATE\s+KEY-----[\s\S]*?-----END\s+OPENSSH\s+PRIVATE\s+KEY-----",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex PrivateKeyRegex();

    // API key (general format)
    [GeneratedRegex(
        @"(?i)(api[_-]?key|apikey|secret[_-]?key|access[_-]?token|auth[_-]?token)\s*[:=]\s*['""]?[a-zA-Z0-9_-]{20,}['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex APIKeyRegex();

    // Password pattern
    [GeneratedRegex(
        @"(?i)(password|passwd|pwd|pass)\s*[:=]\s*[^\s]{4,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex PasswordRegex();

    // JWT token
    [GeneratedRegex(
        @"eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex JWTRegex();
}
