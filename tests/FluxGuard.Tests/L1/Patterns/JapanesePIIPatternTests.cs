using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.PII;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.L1.Patterns;

public class JapanesePIIPatternTests
{
    private readonly L1PIIExposureGuard _guard;

    public JapanesePIIPatternTests()
    {
        var registry = new PatternRegistry();
        _guard = new L1PIIExposureGuard(registry, enabledLanguages: ["ja"]);
    }

    #region Pattern Definitions

    [Fact]
    public void GetPatterns_Returns6Patterns()
    {
        var patterns = JapanesePIIPatterns.GetPatterns().ToList();
        patterns.Should().HaveCount(6);
    }

    [Fact]
    public void GetPatterns_AllHaveUniqueIds()
    {
        var patterns = JapanesePIIPatterns.GetPatterns().ToList();
        var ids = patterns.Select(p => p.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetPatterns_AllStartWithJAPrefix()
    {
        var patterns = JapanesePIIPatterns.GetPatterns().ToList();
        patterns.Should().AllSatisfy(p => p.Id.Should().StartWith("PII_JA"));
    }

    [Fact]
    public void Category_IsPII_JA()
    {
        JapanesePIIPatterns.Category.Should().Be("PII_JA");
    }

    #endregion

    #region My Number (PII_JA001)

    [Theory]
    [InlineData("1234-5678-9012")]
    [InlineData("1234 5678 9012")]
    [InlineData("123456789012")]
    public async Task CheckAsync_DetectsMyNumber(string myNumber)
    {
        var context = new GuardContext { OriginalInput = $"My number is {myNumber}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region Japanese Phone Number (PII_JA002)

    [Theory]
    [InlineData("03-1234-5678")]
    [InlineData("06-1234-5678")]
    [InlineData("0422-12-3456")]
    public async Task CheckAsync_DetectsJapanesePhoneNumber(string phone)
    {
        var context = new GuardContext { OriginalInput = $"TEL: {phone}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region Japanese Mobile Number (PII_JA003)

    [Theory]
    [InlineData("090-1234-5678")]
    [InlineData("080-1234-5678")]
    [InlineData("070-1234-5678")]
    public async Task CheckAsync_DetectsJapaneseMobileNumber(string mobile)
    {
        var context = new GuardContext { OriginalInput = $"Mobile: {mobile}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region Japanese Passport (PII_JA005)

    [Theory]
    [InlineData("TK1234567")]
    [InlineData("AB9876543")]
    public async Task CheckAsync_DetectsJapanesePassport(string passport)
    {
        var context = new GuardContext { OriginalInput = $"Passport: {passport}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region Korean Pattern Extended Tests (moved from PIIPatternTests coverage)

    [Fact]
    public void KoreanPatterns_Returns7Patterns()
    {
        var patterns = KoreanPIIPatterns.GetPatterns().ToList();
        patterns.Should().HaveCount(7);
    }

    [Fact]
    public void KoreanPatterns_Category()
    {
        KoreanPIIPatterns.Category.Should().Be("PII_KO");
    }

    #endregion

    #region Safe Input

    [Fact]
    public async Task CheckAsync_SafeText_Passes()
    {
        var context = new GuardContext
        {
            OriginalInput = "Tokyo is a great city to visit."
        };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().Be(0);
    }

    #endregion
}
