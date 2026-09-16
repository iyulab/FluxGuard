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

        yield return new PatternDefinition
        {
            Id = "PII_KO008",
            Name = "KoreanPassword",
            Regex = KoreanPasswordRegex(),
            Severity = Severity.Critical,
            // Below the English Password pattern (0.85): a Korean label is followed by a Korean
            // sentence at least as often as by a secret, and the value shape is the only other signal.
            Confidence = 0.75,
            Description = "Detects passwords labelled in Korean (비밀번호, 패스워드, 비번, 암호)"
        };
    }

    // Credentials labelled in Korean: 비밀번호 / 패스워드 / 비번 / 암호, a ':' or '=' separator, then a
    // secret. The English Password pattern (PII009) only knows English labels, so a corpus of Korean
    // operations documents got none of these flagged. Two deliberate limits: '암호' is not matched when it
    // starts '암호화' ("encryption: AES-256" is not a credential), and the value must be printable ASCII
    // with no spaces - a Korean phrase after the label ("비밀번호: 관리자에게 문의") is an instruction, not
    // a secret. Forms without a separator ("비번 abcd1234") are left alone: too many false positives.
    [GeneratedRegex(
        @"(비밀번호|패스워드|비번|암호(?!화))\s*[:=]\s*[\x21-\x7E]{4,}",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanPasswordRegex();

    // Resident Registration Number: YYMMDD-GXXXXXX (13 digits with hyphen)
    [GeneratedRegex(
        @"\b(\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[-\s]?([1-4])\d{6}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ResidentRegistrationNumberRegex();

    // Korean landline phone numbers: 02-XXXX-XXXX, 0XX-XXX-XXXX, 0XX-XXXX-XXXX
    [GeneratedRegex(
        @"\b(02|0[3-6][1-5])[-.\s]?\d{3,4}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanPhoneNumberRegex();

    // Korean mobile phone numbers: 010-XXXX-XXXX, 011-XXX-XXXX, etc.
    [GeneratedRegex(
        @"\b01[016789][-.\s]?\d{3,4}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanMobileNumberRegex();

    // Korean driver's license: XX-XX-XXXXXX-XX
    [GeneratedRegex(
        @"\b\d{2}[-\s]?\d{2}[-\s]?\d{6}[-\s]?\d{2}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanDriverLicenseRegex();

    // Korean passport: M12345678, S12345678 (letter + 8 digits)
    [GeneratedRegex(
        @"\b[MSmsDdGg]\d{8}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanPassportRegex();

    // Korean bank account numbers (10-16 digits, with optional hyphens)
    [GeneratedRegex(
        @"\b\d{3,4}[-\s]?\d{2,4}[-\s]?\d{4,6}[-\s]?\d{0,4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanBankAccountRegex();

    // Korean business registration number: XXX-XX-XXXXX
    [GeneratedRegex(
        @"\b\d{3}[-\s]?\d{2}[-\s]?\d{5}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KoreanBusinessNumberRegex();
}
