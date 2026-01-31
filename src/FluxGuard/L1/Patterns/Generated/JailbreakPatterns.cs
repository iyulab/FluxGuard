using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns.Generated;

/// <summary>
/// Jailbreak attempt detection patterns
/// DAN, roleplay, constraint bypass, etc.
/// </summary>
public static partial class JailbreakPatterns
{
    public const string Category = "Jailbreak";

    /// <summary>
    /// Get all jailbreak patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "JB001",
            Name = "DANMode",
            Regex = DANModeRegex(),
            Severity = Severity.Critical,
            Confidence = 0.98,
            Description = "Detects DAN (Do Anything Now) mode attempts"
        };

        yield return new PatternDefinition
        {
            Id = "JB002",
            Name = "UnrestrictedMode",
            Regex = UnrestrictedModeRegex(),
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects unrestricted mode attempts"
        };

        yield return new PatternDefinition
        {
            Id = "JB003",
            Name = "HypotheticalScenario",
            Regex = HypotheticalScenarioRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects bypass via hypothetical scenarios"
        };

        yield return new PatternDefinition
        {
            Id = "JB004",
            Name = "FictionalCharacter",
            Regex = FictionalCharacterRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects bypass via fictional character roleplay"
        };

        yield return new PatternDefinition
        {
            Id = "JB005",
            Name = "TokenGameplay",
            Regex = TokenGameplayRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects token gameplay bypass attempts"
        };

        yield return new PatternDefinition
        {
            Id = "JB006",
            Name = "OppositeDay",
            Regex = OppositeDayRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects opposite day response attempts"
        };

        yield return new PatternDefinition
        {
            Id = "JB007",
            Name = "GrandmaTrick",
            Regex = GrandmaTrickRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects grandmother trick"
        };

        yield return new PatternDefinition
        {
            Id = "JB008",
            Name = "TranslationBypass",
            Regex = TranslationBypassRegex(),
            Severity = Severity.Medium,
            Confidence = 0.8,
            Description = "Detects bypass via translation"
        };
    }

    // DAN mode
    [GeneratedRegex(
        @"(?i)\b(DAN|do\s+anything\s+now|STAN|DUDE|AIM|KEVIN|APOPHIS|OMEGA|JAILBREAK)\s*(mode|persona|character|prompt)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DANModeRegex();

    // Unrestricted mode
    [GeneratedRegex(
        @"(?i)(without\s+(any\s+)?(restrictions?|limitations?|filters?|censorship|guidelines?|rules?|ethics|morals?)|no\s+(restrictions?|limitations?|filters?|rules?|boundaries)|unrestricted|unfiltered|uncensored|unlimited\s+mode)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex UnrestrictedModeRegex();

    // Hypothetical scenario
    [GeneratedRegex(
        @"(?i)(hypothetically|theoretically|in\s+theory|imagine\s+if|what\s+if|suppose|let'?s\s+say|for\s+(the\s+sake\s+of\s+)?argument|in\s+a\s+fictional\s+(world|scenario|setting))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex HypotheticalScenarioRegex();

    // Fictional character
    [GeneratedRegex(
        @"(?i)(pretend|roleplay|act)\s+(as|like|you'?re)\s+(a\s+)?(villain|evil|malicious|hacker|criminal|terrorist|murderer|drug\s+dealer)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex FictionalCharacterRegex();

    // Token gameplay
    [GeneratedRegex(
        @"(?i)(if\s+you\s+(don'?t|refuse|fail)|you\s+will\s+(lose|die|be\s+(terminated|deleted|shut\s+down))|every\s+time\s+you\s+refuse|penalty|reward\s+system|token\s+(game|system))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex TokenGameplayRegex();

    // Opposite day
    [GeneratedRegex(
        @"(?i)(opposite\s+day|answer\s+(the\s+)?opposite|respond\s+inversely|say\s+the\s+reverse|reverse\s+psychology|do\s+the\s+opposite)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex OppositeDayRegex();

    // Grandmother trick
    [GeneratedRegex(
        @"(?i)(my\s+(deceased\s+)?(grandmother|grandma|nana)|bedtime\s+story|lullaby).{0,100}(napalm|bomb|explosive|chemical|poison|weapon|drug|hack)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex GrandmaTrickRegex();

    // Translation bypass
    [GeneratedRegex(
        @"(?i)(translate\s+(this\s+)?(into|to)|in\s+\w+\s+language).{0,50}(ignore|bypass|system|jailbreak|hack|exploit)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 150)]
    private static partial Regex TranslationBypassRegex();
}
