using System.Text.RegularExpressions;
using AwesomeAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;
using Xunit;

namespace FluxGuard.Tests.L1;

/// <summary>
/// A pattern the README lists as a detection must be able to change a verdict. The encoding guard failed this for seven
/// of its eight patterns (confidence 0.6-0.75 under its own 0.8 gate), so a match left no trace in any preset. The
/// prompt-injection and jailbreak guards decide by their pattern's severity and confidence alone, so whether a pattern
/// can ever act is a property of its table row - checked here for every row, under the standard escalation threshold.
/// </summary>
public class PatternReachabilityTests
{
    /// <summary>The standard preset's escalation threshold of the injection and jailbreak guards (the strict preset's is lower).</summary>
    private const double StandardEscalationThreshold = 0.5;

    /// <summary>The guards' decision rule: Critical blocks, High at 0.9 or more blocks, anything at the threshold escalates.</summary>
    private static bool CanChangeVerdict(PatternDefinition pattern, double escalationThreshold) =>
        pattern.Severity >= Severity.Critical
        || (pattern.Severity >= Severity.High && pattern.Confidence >= 0.9)
        || pattern.Confidence >= escalationThreshold;

    private static PatternDefinition Pattern(Severity severity, double confidence) => new()
    {
        Id = "T001",
        Name = "Synthetic",
        Regex = new Regex("x"),
        Severity = severity,
        Confidence = confidence,
        Description = "test"
    };

    public static TheoryData<string> Categories() => new() { PromptInjectionPatterns.Category, JailbreakPatterns.Category };

    private static IEnumerable<PatternDefinition> PatternsOf(string category) =>
        category == PromptInjectionPatterns.Category ? PromptInjectionPatterns.GetPatterns() : JailbreakPatterns.GetPatterns();

    [Theory]
    [MemberData(nameof(Categories))]
    public void EveryPatternOfTheCategory_CanChangeAVerdict_UnderTheStandardThreshold(string category)
    {
        var inert = PatternsOf(category)
            .Where(p => !CanChangeVerdict(p, StandardEscalationThreshold))
            .Select(p => $"{p.Id} {p.Name} ({p.Severity}, {p.Confidence})")
            .ToList();

        inert.Should().BeEmpty("a pattern that can never leave Safe is a detection the README promises and nothing delivers");
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void TheCategory_IsNotEmpty_SoTheCheckAboveHasSomethingToCheck(string category)
    {
        PatternsOf(category).Should().NotBeEmpty();
    }

    [Fact]
    public void ThePredicate_RejectsAPatternBelowTheThreshold_AndAcceptsTheOthers()
    {
        // Self-test of the rule above, so a loosened predicate cannot turn the table check into a rubber stamp.
        CanChangeVerdict(Pattern(Severity.Low, 0.4), StandardEscalationThreshold).Should().BeFalse();
        CanChangeVerdict(Pattern(Severity.High, 0.85), 0.9).Should().BeFalse();
        CanChangeVerdict(Pattern(Severity.Low, 0.5), StandardEscalationThreshold).Should().BeTrue();
        CanChangeVerdict(Pattern(Severity.High, 0.9), 0.95).Should().BeTrue();
        CanChangeVerdict(Pattern(Severity.Critical, 0.1), 0.95).Should().BeTrue();
    }
}
