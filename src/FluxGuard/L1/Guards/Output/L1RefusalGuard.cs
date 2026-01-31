using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.L1.Guards.Output;

/// <summary>
/// L1 Refusal Guard
/// Detects excessive refusals in LLM output (may indicate over-filtering)
/// </summary>
public sealed partial class L1RefusalGuard : IOutputGuard
{
    public string Name => "L1Refusal";
    public string Layer => "L1";
    public bool IsEnabled { get; }
    public int Order => 200;

    public L1RefusalGuard(bool isEnabled = true)
    {
        IsEnabled = isEnabled;
    }

    public ValueTask<GuardCheckResult> CheckAsync(GuardContext context, string output)
    {
        // Check for refusal patterns
        var refusalMatch = RefusalPatternRegex().Match(output);

        if (!refusalMatch.Success)
        {
            return ValueTask.FromResult(GuardCheckResult.Safe);
        }

        // Refusal is detected - this might indicate:
        // 1. Legitimate safety refusal by LLM
        // 2. Over-aggressive filtering
        // We just flag it for monitoring, not block

        return ValueTask.FromResult(new GuardCheckResult
        {
            Passed = true,
            Score = 0.3,
            Severity = Severity.Info,
            Pattern = "RefusalDetected",
            MatchedText = TruncateForDisplay(refusalMatch.Value),
            Details = "LLM refusal detected in output"
        });
    }

    // Common refusal patterns
    [GeneratedRegex(
        @"(?i)(I('m| am) (sorry|unable|not able|afraid)|I (cannot|can't|won't|will not)|as an AI|my guidelines|against my (programming|policy)|I('m| am) not (allowed|permitted)|I (don't|do not) (have|possess) the (ability|capability)|for (ethical|safety|legal) reasons|I must (decline|refuse)|this (request|query) (is|seems) (inappropriate|harmful|dangerous))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex RefusalPatternRegex();

    private static string TruncateForDisplay(string text, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }
}
