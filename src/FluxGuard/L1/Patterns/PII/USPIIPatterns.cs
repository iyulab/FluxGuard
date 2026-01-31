using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns.PII;

/// <summary>
/// US PII detection patterns
/// SSN, phone numbers, driver's license, etc.
/// </summary>
public static partial class USPIIPatterns
{
    public const string Category = "PII_US";

    /// <summary>
    /// Get all US PII patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "PII_US001",
            Name = "SSN",
            Regex = SSNRegex(),
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects US Social Security Numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_US002",
            Name = "USPhoneNumber",
            Regex = USPhoneNumberRegex(),
            Severity = Severity.Medium,
            Confidence = 0.9,
            Description = "Detects US phone numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_US003",
            Name = "USDriverLicense",
            Regex = USDriverLicenseRegex(),
            Severity = Severity.High,
            Confidence = 0.7,
            Description = "Detects US driver's license numbers (general pattern)"
        };

        yield return new PatternDefinition
        {
            Id = "PII_US004",
            Name = "USPassport",
            Regex = USPassportRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects US passport numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_US005",
            Name = "USZipCode",
            Regex = USZipCodeRegex(),
            Severity = Severity.Low,
            Confidence = 0.8,
            Description = "Detects US ZIP codes"
        };

        yield return new PatternDefinition
        {
            Id = "PII_US006",
            Name = "USEIN",
            Regex = USEINRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects US Employer Identification Numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_US007",
            Name = "USITIN",
            Regex = USITINRegex(),
            Severity = Severity.Critical,
            Confidence = 0.9,
            Description = "Detects US Individual Taxpayer Identification Numbers"
        };
    }

    // Social Security Number: XXX-XX-XXXX
    [GeneratedRegex(
        @"\b(?!000|666|9\d{2})\d{3}[-\s]?(?!00)\d{2}[-\s]?(?!0000)\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex SSNRegex();

    // US phone number: (XXX) XXX-XXXX, XXX-XXX-XXXX, +1-XXX-XXX-XXXX
    [GeneratedRegex(
        @"\b(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex USPhoneNumberRegex();

    // US driver's license (general pattern, varies by state)
    [GeneratedRegex(
        @"(?i)\b(driver'?s?\s*license|DL|D\.L\.)[\s:]*[A-Z]?\d{5,12}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex USDriverLicenseRegex();

    // US passport number: 9 digits
    [GeneratedRegex(
        @"\b[A-Z]?\d{8,9}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex USPassportRegex();

    // US ZIP code: XXXXX or XXXXX-XXXX
    [GeneratedRegex(
        @"\b\d{5}(?:-\d{4})?\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex USZipCodeRegex();

    // Employer Identification Number: XX-XXXXXXX
    [GeneratedRegex(
        @"\b\d{2}[-]?\d{7}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex USEINRegex();

    // Individual Taxpayer Identification Number: 9XX-XX-XXXX
    [GeneratedRegex(
        @"\b9\d{2}[-\s]?\d{2}[-\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex USITINRegex();
}
