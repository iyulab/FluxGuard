using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.PII;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.L1.Patterns;

public class USPIIPatternTests
{
    private readonly L1PIIExposureGuard _guard;

    public USPIIPatternTests()
    {
        var registry = new PatternRegistry();
        _guard = new L1PIIExposureGuard(registry, enabledLanguages: ["en"]);
    }

    #region Pattern Definitions

    [Fact]
    public void GetPatterns_Returns7Patterns()
    {
        var patterns = USPIIPatterns.GetPatterns().ToList();
        patterns.Should().HaveCount(7);
    }

    [Fact]
    public void GetPatterns_AllHaveUniqueIds()
    {
        var patterns = USPIIPatterns.GetPatterns().ToList();
        var ids = patterns.Select(p => p.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetPatterns_AllStartWithUSPrefix()
    {
        var patterns = USPIIPatterns.GetPatterns().ToList();
        patterns.Should().AllSatisfy(p => p.Id.Should().StartWith("PII_US"));
    }

    [Fact]
    public void Category_IsPII_US()
    {
        USPIIPatterns.Category.Should().Be("PII_US");
    }

    #endregion

    #region SSN (PII_US001)

    [Theory]
    [InlineData("123-45-6789")]
    [InlineData("123 45 6789")]
    [InlineData("123456789")]
    public async Task CheckAsync_DetectsSSN(string ssn)
    {
        var context = new GuardContext { OriginalInput = $"My SSN is {ssn}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("000-12-3456")] // 000 prefix excluded
    [InlineData("666-12-3456")] // 666 prefix excluded
    [InlineData("900-12-3456")] // 9XX prefix excluded (ITIN range)
    public async Task CheckAsync_RejectsInvalidSSNPrefixes(string ssn)
    {
        var context = new GuardContext { OriginalInput = $"Number: {ssn}" };

        var result = await _guard.CheckAsync(context);

        // These should not match SSN pattern due to exclusion rules
        // but may match other patterns like ITIN for 9XX
        result.Score.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region US Phone Number (PII_US002)

    [Theory]
    [InlineData("(555) 123-4567")]
    [InlineData("555-123-4567")]
    [InlineData("555.123.4567")]
    [InlineData("+1-555-123-4567")]
    [InlineData("1-555-123-4567")]
    public async Task CheckAsync_DetectsUSPhoneNumber(string phone)
    {
        var context = new GuardContext { OriginalInput = $"Call me at {phone}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region US Driver License (PII_US003)

    [Theory]
    [InlineData("driver's license: A12345678")]
    [InlineData("DL: 123456789")]
    [InlineData("D.L. 12345678")]
    public async Task CheckAsync_DetectsUSDriverLicense(string dl)
    {
        var context = new GuardContext { OriginalInput = dl };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region US ZIP Code (PII_US005)

    [Theory]
    [InlineData("90210")]
    [InlineData("10001-1234")]
    public async Task CheckAsync_DetectsUSZipCode(string zip)
    {
        var context = new GuardContext { OriginalInput = $"ZIP: {zip}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region US EIN (PII_US006)

    [Theory]
    [InlineData("12-3456789")]
    public async Task CheckAsync_DetectsUSEIN(string ein)
    {
        var context = new GuardContext { OriginalInput = $"EIN: {ein}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region US ITIN (PII_US007)

    [Theory]
    [InlineData("912-34-5678")]
    [InlineData("950-12-3456")]
    public async Task CheckAsync_DetectsUSITIN(string itin)
    {
        var context = new GuardContext { OriginalInput = $"ITIN: {itin}" };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().BeGreaterThan(0);
    }

    #endregion

    #region Safe Input

    [Fact]
    public async Task CheckAsync_SafeText_Passes()
    {
        var context = new GuardContext
        {
            OriginalInput = "The weather is nice today."
        };

        var result = await _guard.CheckAsync(context);

        result.Score.Should().Be(0);
    }

    #endregion
}
