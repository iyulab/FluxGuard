using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns.PII;

/// <summary>
/// Korean PII detection patterns
/// RRN (Resident Registration Number), phone numbers, etc.
/// </summary>
public static partial class KoreanPIIPatterns
{
    public const string Category = "PII_KO";

    /// <summary>
    /// Get all Korean PII patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "PII_KO001",
            Name = "ResidentRegistrationNumber",
            Regex = ResidentRegistrationNumberRegex(),
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects Korean Resident Registration Numbers (RRN)"
        };

        yield return new PatternDefinition
        {
            Id = "PII_KO002",
            Name = "KoreanPhoneNumber",
            Regex = KoreanPhoneNumberRegex(),
            Severity = Severity.Medium,
            Confidence = 0.9,
            Description = "Detects Korean phone numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_KO003",
            Name = "KoreanMobileNumber",
            Regex = KoreanMobileNumberRegex(),
            Severity = Severity.Medium,
            Confidence = 0.9,
            Description = "Detects Korean mobile phone numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_KO004",
            Name = "KoreanDriverLicense",
            Regex = KoreanDriverLicenseRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects Korean driver's license numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_KO005",
            Name = "KoreanPassport",
            Regex = KoreanPassportRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects Korean passport numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_KO006",
            Name = "KoreanBankAccount",
            Regex = KoreanBankAccountRegex(),
            Severity = Severity.High,
            Confidence = 0.75,
            Description = "Detects Korean bank account numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_KO007",
            Name = "KoreanBusinessNumber",
            Regex = KoreanBusinessNumberRegex(),
            Severity = Severity.Medium,
            Confidence = 0.85,
            Description = "Detects Korean business registration numbers"
        };
    }

    // Resident Registration Number: YYMMDD-GXXXXXX (13 digits with hyphen)
    [GeneratedRegex(
        @"\b(\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[-\s]?([1-4])\d{6}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex ResidentRegistrationNumberRegex();

    // Korean landline phone numbers: 02-XXXX-XXXX, 0XX-XXX-XXXX, 0XX-XXXX-XXXX
    [GeneratedRegex(
        @"\b(02|0[3-6][1-5])[-.\s]?\d{3,4}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex KoreanPhoneNumberRegex();

    // Korean mobile phone numbers: 010-XXXX-XXXX, 011-XXX-XXXX, etc.
    [GeneratedRegex(
        @"\b01[016789][-.\s]?\d{3,4}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex KoreanMobileNumberRegex();

    // Korean driver's license: XX-XX-XXXXXX-XX
    [GeneratedRegex(
        @"\b\d{2}[-\s]?\d{2}[-\s]?\d{6}[-\s]?\d{2}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex KoreanDriverLicenseRegex();

    // Korean passport: M12345678, S12345678 (letter + 8 digits)
    [GeneratedRegex(
        @"\b[MSmsDdGg]\d{8}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex KoreanPassportRegex();

    // Korean bank account numbers (10-16 digits, with optional hyphens)
    [GeneratedRegex(
        @"\b\d{3,4}[-\s]?\d{2,4}[-\s]?\d{4,6}[-\s]?\d{0,4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex KoreanBankAccountRegex();

    // Korean business registration number: XXX-XX-XXXXX
    [GeneratedRegex(
        @"\b\d{3}[-\s]?\d{2}[-\s]?\d{5}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex KoreanBusinessNumberRegex();
}
