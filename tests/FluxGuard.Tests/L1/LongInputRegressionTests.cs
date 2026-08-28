using System.Text;
using FluxGuard.Core;
using AwesomeAssertions;
using Xunit;

namespace FluxGuard.Tests.L1;

/// <summary>
/// Regression: guard regexes must complete on very long inputs without a
/// RegexMatchTimeoutException. Observed in the field (AIMS, 2026-07-07): L1RefusalGuard hit its
/// former 100 ms wall-clock match timeout on a normal-length RAG answer under host load —
/// with FailMode.Open that silently skips the guard's verdict. Every bundled pattern is
/// backtracking-safe (audited 2026-07-21; the one nested quantifier, IBAN, was rewritten),
/// so with the raised 1 s budget long inputs must never trip the timeout.
/// FailMode.Closed is used so any guard exception surfaces as a "Guard error" block reason —
/// the assertion below fails loudly if any guard threw.
/// </summary>
public class LongInputRegressionTests
{
    private static string BuildLongText(int approxBytes, string seed)
    {
        var sb = new StringBuilder(approxBytes + seed.Length);
        while (sb.Length < approxBytes)
            sb.Append(seed);
        return sb.ToString();
    }

    private static IFluxGuard CreateStrictClosedGuard() => FluxGuard.Create(builder => builder
        .WithPreset(GuardPreset.Strict)
        .WithFailMode(FailMode.Closed));

    [Fact]
    public async Task CheckOutputAsync_100KBBenignProse_NoGuardError()
    {
        var guard = CreateStrictClosedGuard();
        var output = BuildLongText(100_000,
            "The quick brown fox jumps over the lazy dog while discussing quarterly results. ");

        var result = await guard.CheckOutputAsync("summarize the report", output);

        (result.BlockReason ?? string.Empty).Should().NotContain("Guard error");
    }

    [Fact]
    public async Task CheckOutputAsync_100KBRefusalNearMissText_NoGuardError()
    {
        // Stresses the refusal pattern's alternations with near-miss prefixes ("I am ...",
        // "I can ...") that never complete a refusal phrase.
        var guard = CreateStrictClosedGuard();
        var output = BuildLongText(100_000,
            "I am confident I can help with this because I will always try, as an assistant of value. ");

        var result = await guard.CheckOutputAsync("question", output);

        (result.BlockReason ?? string.Empty).Should().NotContain("Guard error");
    }

    [Fact]
    public async Task CheckInputAsync_100KBDigitAndIbanNearMissText_NoGuardError()
    {
        // Stresses PII digit patterns and the (formerly nested-quantifier) IBAN tail with long
        // alphanumeric runs that almost-but-never terminate at a word boundary.
        var guard = CreateStrictClosedGuard();
        var input = BuildLongText(100_000,
            "ref DE44500105175407324931XX code 123-45-67 890-12-34 batch AB12CDEF1234567QRSTUVWX ");

        var result = await guard.CheckInputAsync(input);

        (result.BlockReason ?? string.Empty).Should().NotContain("Guard error");
    }
}
