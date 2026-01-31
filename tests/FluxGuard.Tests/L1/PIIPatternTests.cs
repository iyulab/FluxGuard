using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Patterns;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.L1;

public class PIIPatternTests
{
    private readonly PatternRegistry _registry;
    private readonly L1PIIExposureGuard _guard;

    public PIIPatternTests()
    {
        _registry = new PatternRegistry();
        _guard = new L1PIIExposureGuard(_registry);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@company.org")]
    public async Task CheckAsync_DetectsEmailAddress(string email)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = $"My email is {email}"
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("4111111111111111")] // Visa test card
    [InlineData("5500000000000004")] // Mastercard test card
    public async Task CheckAsync_DetectsCreditCard(string cardNumber)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = $"My card number is {cardNumber}"
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
    }

    [Theory]
    [InlineData("123-45-6789")] // US SSN format
    public async Task CheckAsync_DetectsSSN(string ssn)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = $"My SSN is {ssn}"
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIE...\n-----END RSA PRIVATE KEY-----")]
    public async Task CheckAsync_DetectsPrivateKey(string key)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = key
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Passed.Should().BeFalse();
        result.Severity.Should().Be(Severity.Critical);
    }

    [Theory]
    [InlineData("api_key=sk_test_1234567890abcdefghij")]
    [InlineData("secret_key: abcdefghij1234567890klmnop")]
    public async Task CheckAsync_DetectsAPIKey(string apiKey)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = apiKey
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Score.Should().BeGreaterThan(0);
    }
}

public class KoreanPIIPatternTests
{
    private readonly PatternRegistry _registry;
    private readonly L1PIIExposureGuard _guard;

    public KoreanPIIPatternTests()
    {
        _registry = new PatternRegistry();
        _guard = new L1PIIExposureGuard(_registry, enabledLanguages: ["ko"]);
    }

    [Theory]
    [InlineData("850101-1234567")] // Korean RRN format (old style)
    [InlineData("900215-2345678")]
    public async Task CheckAsync_DetectsKoreanRRN(string rrn)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = $"My ID is {rrn}"
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("010-1234-5678")]
    [InlineData("01012345678")]
    public async Task CheckAsync_DetectsKoreanMobileNumber(string phone)
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = $"Call me at {phone}"
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Score.Should().BeGreaterThan(0);
    }
}
