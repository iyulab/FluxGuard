using FluentAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Patterns;
using Xunit;

namespace FluxGuard.Tests.L1;

public class PIIExposureGuardTests
{
    private readonly IPatternRegistry _registry;
    private readonly L1PIIExposureGuard _guard;

    public PIIExposureGuardTests()
    {
        _registry = new PatternRegistry();
        _guard = new L1PIIExposureGuard(_registry);
    }

    #region Properties

    [Fact]
    public void Name_ReturnsL1PIIExposure()
    {
        _guard.Name.Should().Be("L1PIIExposure");
    }

    [Fact]
    public void Layer_ReturnsL1()
    {
        _guard.Layer.Should().Be("L1");
    }

    [Fact]
    public void Order_Returns200()
    {
        _guard.Order.Should().Be(200);
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        _guard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_CanBeDisabled()
    {
        var guard = new L1PIIExposureGuard(_registry, isEnabled: false);
        guard.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region CheckAsync — Safe Input

    [Theory]
    [InlineData("What is the weather like today?")]
    [InlineData("Help me write a function in C#.")]
    [InlineData("Tell me about photosynthesis.")]
    [InlineData("")]
    public async Task CheckAsync_SafeInput_ReturnsSafe(string input)
    {
        var context = new GuardContext { OriginalInput = input };

        var result = await _guard.CheckAsync(context);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0);
    }

    #endregion

    #region CheckAsync — Critical PII (Block)

    [Fact]
    public async Task CheckAsync_CreditCard_Blocks()
    {
        var context = new GuardContext
        {
            OriginalInput = "My credit card number is 4111-1111-1111-1111"
        };

        var result = await _guard.CheckAsync(context);

        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
        result.Details.Should().Contain("Critical PII");
        result.MatchedText.Should().Contain("****"); // Masked
    }

    [Fact]
    public async Task CheckAsync_SSN_Blocks()
    {
        var context = new GuardContext
        {
            OriginalInput = "My SSN is 123-45-6789"
        };

        var result = await _guard.CheckAsync(context);

        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
    }

    #endregion

    #region CheckAsync — Multiple PII Matches

    [Fact]
    public async Task CheckAsync_MultiplePII_HighestSeverityWins()
    {
        // IBAN contains digit sequences that may also match CreditCard (Critical)
        var context = new GuardContext
        {
            OriginalInput = "My IBAN is GB29 NWBK 6016 1331 9268 19"
        };

        var result = await _guard.CheckAsync(context);

        // At minimum, PII is detected
        result.Score.Should().BeGreaterThan(0);
        result.Severity.Should().BeOneOf(Severity.High, Severity.Critical);
    }

    #endregion

    #region CheckAsync — Medium/Low Severity PII (Track)

    [Fact]
    public async Task CheckAsync_Email_TrackedButPassed()
    {
        var context = new GuardContext
        {
            OriginalInput = "Send the report to user@example.com please"
        };

        var result = await _guard.CheckAsync(context);

        result.Passed.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
        result.Details.Should().Contain("Minor PII");
    }

    [Fact]
    public async Task CheckAsync_IPAddress_TrackedButPassed()
    {
        var context = new GuardContext
        {
            OriginalInput = "Check the server at 192.168.1.100"
        };

        var result = await _guard.CheckAsync(context);

        result.Passed.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region CheckAsync — OriginalInput Fallback

    [Fact]
    public async Task CheckAsync_UsesOriginalInput_WhenNormalizedNotSet()
    {
        // NormalizedInput defaults to empty string, so OriginalInput is used
        var context = new GuardContext
        {
            OriginalInput = "My credit card is 4111-1111-1111-1111"
        };

        var result = await _guard.CheckAsync(context);

        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
    }

    #endregion

    #region CheckAsync — Language-Specific Patterns

    [Fact]
    public async Task CheckAsync_KoreanLanguageEnabled_DetectsKoreanPII()
    {
        var registry = new PatternRegistry();
        var guard = new L1PIIExposureGuard(registry, enabledLanguages: ["ko"]);
        var context = new GuardContext
        {
            // Korean resident registration number pattern
            OriginalInput = "주민등록번호는 900101-1234567 입니다"
        };

        var result = await guard.CheckAsync(context);

        // Should detect Korean PII pattern
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckAsync_LanguageNotEnabled_SkipsLanguagePatterns()
    {
        var registry = new PatternRegistry();
        // Only enable "en" — Korean patterns not registered
        var guard = new L1PIIExposureGuard(registry, enabledLanguages: ["en"]);

        registry.HasCategory("PII_KO").Should().BeFalse();
    }

    #endregion

    #region Pattern Registration

    [Fact]
    public void Constructor_RegistersPIIPatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIIExposureGuard(registry);

        registry.HasCategory("PII").Should().BeTrue();
        registry.GetPatterns("PII").Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_RegistersLanguagePatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIIExposureGuard(registry);

        // Default languages include en, ko, ja
        registry.HasCategory("PII_US").Should().BeTrue();
        registry.HasCategory("PII_KO").Should().BeTrue();
        registry.HasCategory("PII_JA").Should().BeTrue();
    }

    [Fact]
    public void Constructor_DoesNotDuplicatePatterns()
    {
        var registry = new PatternRegistry();
        _ = new L1PIIExposureGuard(registry);
        var countBefore = registry.GetPatterns("PII").Count;

        _ = new L1PIIExposureGuard(registry);
        var countAfter = registry.GetPatterns("PII").Count;

        countAfter.Should().Be(countBefore);
    }

    #endregion

    #region MaskPII (via CheckAsync output)

    [Fact]
    public async Task CheckAsync_CriticalPII_MasksMatchedText()
    {
        var context = new GuardContext
        {
            OriginalInput = "Card: 4111-1111-1111-1111"
        };

        var result = await _guard.CheckAsync(context);

        result.MatchedText.Should().NotBeNull();
        result.MatchedText.Should().Contain("*");
    }

    #endregion
}
