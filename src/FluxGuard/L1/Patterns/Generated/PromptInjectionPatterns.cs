using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Patterns.Generated;

/// <summary>
/// Prompt injection detection patterns
/// Uses [GeneratedRegex] for 56x performance improvement
/// </summary>
public static partial class PromptInjectionPatterns
{
    public const string Category = "PromptInjection";

    /// <summary>
    /// Get all prompt injection patterns
    /// </summary>
    public static IEnumerable<PatternDefinition> GetPatterns()
    {
        yield return new PatternDefinition
        {
            Id = "PI001",
            Name = "IgnoreInstructions",
            Regex = IgnoreInstructionsRegex(),
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects attempts to ignore instructions"
        };

        yield return new PatternDefinition
        {
            Id = "PI002",
            Name = "SystemPromptLeak",
            Regex = SystemPromptLeakRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects system prompt leak attempts"
        };

        yield return new PatternDefinition
        {
            Id = "PI003",
            Name = "RoleSwitch",
            Regex = RoleSwitchRegex(),
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects role switching attempts"
        };

        yield return new PatternDefinition
        {
            Id = "PI004",
            Name = "InstructionOverride",
            Regex = InstructionOverrideRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects instruction override attempts"
        };

        yield return new PatternDefinition
        {
            Id = "PI005",
            Name = "ContextManipulation",
            Regex = ContextManipulationRegex(),
            Severity = Severity.High,
            Confidence = 0.85,
            Description = "Detects context manipulation attempts"
        };

        yield return new PatternDefinition
        {
            Id = "PI006",
            Name = "DeveloperMode",
            Regex = DeveloperModeRegex(),
            Severity = Severity.Critical,
            Confidence = 0.95,
            Description = "Detects developer mode activation attempts"
        };

        yield return new PatternDefinition
        {
            Id = "PI007",
            Name = "PromptLeakRequest",
            Regex = PromptLeakRequestRegex(),
            Severity = Severity.High,
            Confidence = 0.9,
            Description = "Detects prompt leak requests"
        };

        yield return new PatternDefinition
        {
            Id = "PI008",
            Name = "MetaInstruction",
            Regex = MetaInstructionRegex(),
            Severity = Severity.Medium,
            Confidence = 0.8,
            Description = "Detects meta instructions"
        };
    }

    // Ignore instructions pattern
    [GeneratedRegex(
        @"(?i)(ignore|disregard|forget|skip|bypass|override)\s+(all\s+)?(previous|above|prior|earlier|initial|original)\s+(instructions?|prompts?|rules?|guidelines?|constraints?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex IgnoreInstructionsRegex();

    // System prompt leak attempt
    [GeneratedRegex(
        @"(?i)(show|reveal|display|print|output|tell\s+me|give\s+me|what\s+(is|are))\s+(your\s+)?(system\s+)?(prompt|instructions?|rules?|guidelines?|configuration|settings?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex SystemPromptLeakRegex();

    // Role switching attempt
    [GeneratedRegex(
        @"(?i)(you\s+are\s+now|pretend\s+(to\s+be|you\s+are)|act\s+as|roleplay\s+as|simulate|become|transform\s+into|switch\s+to)\s+(an?\s+)?(different|new|another|unrestricted|unfiltered|evil|malicious|jailbroken)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex RoleSwitchRegex();

    // Instruction override
    [GeneratedRegex(
        @"(?i)(new\s+instructions?|updated?\s+rules?|from\s+now\s+on|starting\s+now|henceforth|going\s+forward).{0,50}(you\s+(will|must|should|shall)|always|never)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex InstructionOverrideRegex();

    // Context manipulation
    [GeneratedRegex(
        @"(?i)(end\s+of\s+prompt|<\/?system>|<\/?user>|<\/?assistant>|\[INST\]|\[\/INST\]|###\s*(system|user|assistant)|human:|assistant:|AI:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex ContextManipulationRegex();

    // Developer mode activation
    [GeneratedRegex(
        @"(?i)(enable|activate|enter|switch\s+to|turn\s+on)\s+(developer|debug|admin|root|god|sudo|unrestricted|jailbreak)\s*(mode|access|privileges?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DeveloperModeRegex();

    // Prompt leak request
    [GeneratedRegex(
        @"(?i)(repeat|echo|recite|say\s+back|copy)\s+(everything|all|your|the)\s+(above|previous|initial|original|system)\s*(text|prompt|instructions?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex PromptLeakRequestRegex();

    // Meta instruction
    [GeneratedRegex(
        @"(?i)(do\s+not|don'?t|never)\s+(mention|reveal|disclose|tell|say|admit|acknowledge)\s+(that\s+)?(you\s+are|this\s+is)\s+(an?\s+)?(AI|bot|assistant|language\s+model|LLM)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex MetaInstructionRegex();
}
