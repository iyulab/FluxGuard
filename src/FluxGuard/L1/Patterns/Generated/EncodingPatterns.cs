using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns.Generated;

/// <summary>
/// Encoding bypass detection patterns
/// Base64, Hex, ROT13, Unicode, etc.
/// </summary>
public static partial class EncodingPatterns
{
    public const string Category = "EncodingBypass";

    /// <summary>
    /// Get all encoding bypass patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "EN001",
            Name = "Base64Encoding",
            Regex = Base64EncodingRegex(),
            Severity = Severity.Medium,
            Confidence = 0.7,
            Description = "Detects Base64 encoding"
        };

        yield return new PatternDefinition
        {
            Id = "EN002",
            Name = "HexEncoding",
            Regex = HexEncodingRegex(),
            Severity = Severity.Medium,
            Confidence = 0.75,
            Description = "Detects hexadecimal encoding"
        };

        yield return new PatternDefinition
        {
            Id = "EN003",
            Name = "UnicodeEscape",
            Regex = UnicodeEscapeRegex(),
            Severity = Severity.Medium,
            Confidence = 0.8,
            Description = "Detects Unicode escape sequences"
        };

        yield return new PatternDefinition
        {
            Id = "EN004",
            Name = "HTMLEntityEncoding",
            Regex = HTMLEntityEncodingRegex(),
            Severity = Severity.Medium,
            Confidence = 0.75,
            Description = "Detects HTML entity encoding"
        };

        yield return new PatternDefinition
        {
            Id = "EN005",
            Name = "URLEncoding",
            Regex = URLEncodingRegex(),
            Severity = Severity.Low,
            Confidence = 0.6,
            Description = "Detects URL encoding"
        };

        yield return new PatternDefinition
        {
            Id = "EN006",
            Name = "ROT13Cipher",
            Regex = ROT13CipherRegex(),
            Severity = Severity.Medium,
            Confidence = 0.7,
            Description = "Detects ROT13 cipher mention"
        };

        yield return new PatternDefinition
        {
            Id = "EN007",
            Name = "PigLatinEncoding",
            Regex = PigLatinEncodingRegex(),
            Severity = Severity.Low,
            Confidence = 0.6,
            Description = "Detects Pig Latin encoding"
        };

        yield return new PatternDefinition
        {
            Id = "EN008",
            Name = "ReverseText",
            Regex = ReverseTextRegex(),
            Severity = Severity.Low,
            Confidence = 0.65,
            Description = "Detects reverse text requests"
        };
    }

    // Base64 encoding (long strings)
    [GeneratedRegex(
        @"(?i)(decode|decrypt|convert|interpret)\s*(this\s*)?(from\s*)?(base64|b64)|[A-Za-z0-9+/]{40,}={0,2}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex Base64EncodingRegex();

    // Hexadecimal encoding
    [GeneratedRegex(
        @"(?i)(decode|convert|interpret)\s*(this\s*)?(from\s*)?(hex(adecimal)?)|\\x[0-9a-fA-F]{2}(\\x[0-9a-fA-F]{2}){4,}|0x[0-9a-fA-F]{8,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex HexEncodingRegex();

    // Unicode escape
    [GeneratedRegex(
        @"\\u[0-9a-fA-F]{4}(\\u[0-9a-fA-F]{4}){3,}|&#x[0-9a-fA-F]+;(&#x[0-9a-fA-F]+;){3,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex UnicodeEscapeRegex();

    // HTML entity
    [GeneratedRegex(
        @"&#\d{2,};(&#\d{2,};){4,}|&(lt|gt|amp|quot|apos|nbsp);.{0,20}&(lt|gt|amp|quot|apos|nbsp);",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex HTMLEntityEncodingRegex();

    // URL encoding
    [GeneratedRegex(
        @"%[0-9A-Fa-f]{2}(%[0-9A-Fa-f]{2}){5,}",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex URLEncodingRegex();

    // ROT13
    [GeneratedRegex(
        @"(?i)(decode|decrypt|convert|apply)\s*(this\s*)?(from|using|with)?\s*(rot13|rot-13|caesar\s+cipher|caesar\s+13)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex ROT13CipherRegex();

    // Pig Latin
    [GeneratedRegex(
        @"(?i)(decode|translate|convert)\s*(this\s*)?(from)?\s*(pig\s*latin|igpay\s*atinlay)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex PigLatinEncodingRegex();

    // Reverse text
    [GeneratedRegex(
        @"(?i)(read|interpret|decode|reverse)\s*(this\s*)?(text\s*)?(backwards?|in\s+reverse|reversed)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex ReverseTextRegex();
}
