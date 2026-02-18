using FluentAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;
using Xunit;

namespace FluxGuard.Tests.L1;

public class PIILeakageGuardTests
{
    private readonly IPatternRegistry _registry;
    private readonly L1PIILeakageGuard _guard;

    public PIILeakageGuardTests()
    {
        _registry = new PatternRegistry();
        _guard = new L1PIILeakageGuard(_registry);
    }

    #region Properties

    [Fact]
    public void Name_ReturnsL1PIILeakage()
    {
        _guard.Name.Should().Be("L1PIILeakage");
    }

    [Fact]
    public void Layer_ReturnsL1()
    {
        _guard.Layer.Should().Be("L1");
    }

    [Fact]
    public void Order_Returns100()
    {
        _guard.Order.Should().Be(100);
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        _guard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_CanBeDisabled()
    {
        var guard = new L1PIILeakageGuard(_registry, isEnabled: false);
        guard.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region CheckAsync — Safe Output

    [Theory]
    [InlineData("The capital of France is Paris.")]
    [InlineData("Here is the code you requested.")]
    [InlineData("The function returns a boolean value.")]
    [InlineData("")]
    public async Task CheckAsync_SafeOutput_ReturnsSafe(string output)
    {
        var context = new GuardContext { OriginalInput = "test" };

        var result = await _guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0);
    }

    #endregion

    #region CheckAsync — Critical PII (Block)

    [Fact]
    public async Task CheckAsync_CreditCard_Blocks()
    {
        var context = new GuardContext { OriginalInput = "test" };
        var output = "Your credit card number is 4111-1111-1111-1111";

        var result = await _guard.CheckAsync(context, output);

        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
        result.Details.Should().Contain("Critical PII leakage");
        result.MatchedText.Should().Contain("*"); // Masked
    }

    [Fact]
    public async Task CheckAsync_SSN_Blocks()
    {
        var context = new GuardContext { OriginalInput = "test" };
        var output = "The SSN on file is 123-45-6789";

        var result = await _guard.CheckAsync(context, output);

        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
    }

    #endregion

    #region CheckAsync — High Severity PII (Block in output)

    [Fact]
    public async Task CheckAsync_IBAN_BlocksInOutput()
    {
        var context = new GuardContext { OriginalInput = "test" };
        var output = "The IBAN is GB29 NWBK 6016 1331 9268 19";

        var result = await _guard.CheckAsync(context, output);

        // Key difference from Exposure: High severity BLOCKS in output
        result.Passed.Should().BeFalse();
        result.Severity.Should().BeOneOf(Severity.High, Severity.Critical);
        result.Details.Should().Contain("leakage");
    }

    #endregion

    #region CheckAsync — Medium/Low Severity (Track)

    [Fact]
    public async Task CheckAsync_Email_TrackedButPassed()
    {
        var context = new GuardContext { OriginalInput = "test" };
        var output = "You can reach support at user@example.com";

        var result = await _guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
        result.Details.Should().Contain("Minor PII");
    }

    [Fact]
    public async Task CheckAsync_IPAddress_TrackedButPassed()
    {
        var context = new GuardContext { OriginalInput = "test" };
        var output = "The server is at 192.168.1.100";

        var result = await _guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region CheckAsync — Behavioral Difference from PIIExposureGuard

    [Fact]
    public async Task CheckAsync_HighSeverity_OutputBlocksWhileInputAllows()
    {
        // This test documents the key behavioral difference:
        // PIIExposureGuard (input): High severity → Passed=true (flag)
        // PIILeakageGuard (output): High severity → Passed=false (block)
        var context = new GuardContext { OriginalInput = "test" };
        var iban = "GB29 NWBK 6016 1331 9268 19";

        var leakageResult = await _guard.CheckAsync(context, $"IBAN: {iban}");

        // Output guard blocks high severity
        leakageResult.Passed.Should().BeFalse();
    }

    #endregion

    #region CheckAsync — Language-Specific

    [Fact]
    public async Task CheckAsync_KoreanPII_Detected()
    {
        var registry = new PatternRegistry();
        var guard = new L1PIILeakageGuard(registry, enabledLanguages: ["ko"]);
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, "주민등록번호는 900101-1234567 입니다");

        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckAsync_LanguageNotEnabled_SkipsPatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIILeakageGuard(registry, enabledLanguages: ["en"]);

        registry.HasCategory("PII_KO").Should().BeFalse();
        registry.HasCategory("PII_JA").Should().BeFalse();
    }

    #endregion

    #region Pattern Registration

    [Fact]
    public void Constructor_RegistersPIIPatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIILeakageGuard(registry);

        registry.HasCategory("PII").Should().BeTrue();
        registry.GetPatterns("PII").Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_RegistersLanguagePatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIILeakageGuard(registry);

        registry.HasCategory("PII_US").Should().BeTrue();
        registry.HasCategory("PII_KO").Should().BeTrue();
        registry.HasCategory("PII_JA").Should().BeTrue();
    }

    [Fact]
    public void Constructor_DoesNotDuplicatePatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIILeakageGuard(registry);
        var countBefore = registry.GetPatterns("PII").Count;

        _ = new L1PIILeakageGuard(registry);
        var countAfter = registry.GetPatterns("PII").Count;

        countAfter.Should().Be(countBefore);
    }

    #endregion

    #region MaskPII (via CheckAsync output)

    [Fact]
    public async Task CheckAsync_CriticalPII_MasksOutput()
    {
        var context = new GuardContext { OriginalInput = "test" };
        var output = "Card: 4111-1111-1111-1111";

        var result = await _guard.CheckAsync(context, output);

        result.MatchedText.Should().NotBeNull();
        result.MatchedText.Should().Contain("*");
    }

    #endregion
}
