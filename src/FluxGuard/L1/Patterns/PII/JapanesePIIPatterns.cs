using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns.PII;

/// <summary>
/// Japanese PII detection patterns
/// My Number, phone numbers, etc.
/// </summary>
public static partial class JapanesePIIPatterns
{
    public const string Category = "PII_JA";

    /// <summary>
    /// Get all Japanese PII patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "PII_JA001",
            Name = "MyNumber",
            Regex = MyNumberRegex(),
            Severity = Severity.Critical,
            Confidence = 0.9,
            Description = "Detects Japanese My Number (Individual Number)"
        };

        yield return new PatternDefinition
        {
            Id = "PII_JA002",
            Name = "JapanesePhoneNumber",
            Regex = JapanesePhoneNumberRegex(),
            Severity = Severity.Medium,
            Confidence = 0.85,
            Description = "Detects Japanese phone numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_JA003",
            Name = "JapaneseMobileNumber",
            Regex = JapaneseMobileNumberRegex(),
            Severity = Severity.Medium,
            Confidence = 0.9,
            Description = "Detects Japanese mobile phone numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_JA004",
            Name = "JapaneseDriverLicense",
            Regex = JapaneseDriverLicenseRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects Japanese driver's license numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_JA005",
            Name = "JapanesePassport",
            Regex = JapanesePassportRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects Japanese passport numbers"
        };

        yield return new PatternDefinition
        {
            Id = "PII_JA006",
            Name = "JapaneseBankAccount",
            Regex = JapaneseBankAccountRegex(),
            Severity = Severity.High,
            Confidence = 0.75,
            Description = "Detects Japanese bank account numbers"
        };
    }

    // My Number: 12 digits
    [GeneratedRegex(
        @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex MyNumberRegex();

    // Japanese landline phone numbers: 0X-XXXX-XXXX, 0XX-XXX-XXXX
    [GeneratedRegex(
        @"\b0\d{1,4}[-.\s]?\d{1,4}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex JapanesePhoneNumberRegex();

    // Japanese mobile phone numbers: 090-XXXX-XXXX, 080-XXXX-XXXX, 070-XXXX-XXXX
    [GeneratedRegex(
        @"\b0[789]0[-.\s]?\d{4}[-.\s]?\d{4}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex JapaneseMobileNumberRegex();

    // Japanese driver's license: 12 digits
    [GeneratedRegex(
        @"\b\d{12}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex JapaneseDriverLicenseRegex();

    // Japanese passport: 2 letters + 7 digits
    [GeneratedRegex(
        @"\b[A-Z]{2}\d{7}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex JapanesePassportRegex();

    // Japanese bank account numbers (7 digits typically)
    [GeneratedRegex(
        @"\b\d{7}\b",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex JapaneseBankAccountRegex();
}
