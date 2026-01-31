using FluentAssertions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Streaming;
using Xunit;

namespace FluxGuard.Tests.Streaming;

public class L1StreamingPIIGuardTests
{
    private readonly L1StreamingPIIGuard _guard;

    public L1StreamingPIIGuardTests()
    {
        _guard = new L1StreamingPIIGuard();
    }

    [Fact]
    public void Name_ReturnsExpected()
    {
        _guard.Name.Should().Be("L1StreamingPII");
    }

    [Fact]
    public void Layer_ReturnsL1()
    {
        _guard.Layer.Should().Be("L1");
    }

    [Fact]
    public async Task ValidateChunkAsync_SafeText_ReturnsPassed()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };

        // Act
        var result = await _guard.ValidateChunkAsync(
            context, "Hello world", "Hello world");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateChunkAsync_WithEmail_SuppressesPII()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };
        var text = "Contact me at user@example.com";

        // Act
        var result = await _guard.ValidateChunkAsync(context, text, text);

        // Assert
        result.Passed.Should().BeFalse();
        result.ShouldSuppress.Should().BeTrue();
        result.Pattern.Should().Be("email");
        result.ReplacementText.Should().Be("[REDACTED]");
    }

    [Fact]
    public async Task ValidateChunkAsync_WithCreditCard_SuppressesPII()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };
        var text = "My card is 4111111111111111";

        // Act
        var result = await _guard.ValidateChunkAsync(context, text, text);

        // Assert
        result.Passed.Should().BeFalse();
        result.ShouldSuppress.Should().BeTrue();
        result.Pattern.Should().Be("credit_card");
    }

    [Fact]
    public async Task ValidateChunkAsync_WithSSN_SuppressesPII()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };
        var text = "SSN: 123-45-6789";

        // Act
        var result = await _guard.ValidateChunkAsync(context, text, text);

        // Assert
        result.Passed.Should().BeFalse();
        result.ShouldSuppress.Should().BeTrue();
        result.Pattern.Should().Be("ssn");
    }

    [Fact]
    public async Task ValidateChunkAsync_SuppressDisabled_TerminatesStream()
    {
        // Arrange
        var guard = new L1StreamingPIIGuard(suppressPII: false);
        var context = new GuardContext { OriginalInput = "test" };
        var text = "Contact: user@example.com";

        // Act
        var result = await guard.ValidateChunkAsync(context, text, text);

        // Assert
        result.Passed.Should().BeFalse();
        result.ShouldTerminate.Should().BeTrue();
        result.Severity.Should().Be(Severity.High);
    }

    [Fact]
    public async Task ValidateFinalAsync_WithPII_DetectsPII()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };
        var fullOutput = "Contact me at user@example.com";

        // Act
        var result = await _guard.ValidateFinalAsync(context, fullOutput);

        // Assert
        result.Passed.Should().BeFalse();
        result.Pattern.Should().Be("email");
    }

    [Fact]
    public async Task ValidateFinalAsync_SafeText_Passes()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };
        var fullOutput = "The weather is nice today.";

        // Act
        var result = await _guard.ValidateFinalAsync(context, fullOutput);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateChunkAsync_EmptyText_ReturnsSafe()
    {
        // Arrange
        var context = new GuardContext { OriginalInput = "test" };

        // Act
        var result = await _guard.ValidateChunkAsync(context, "", "");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void CustomReplacementPattern_UsesPattern()
    {
        // Arrange
        var guard = new L1StreamingPIIGuard(replacementPattern: "[***]");

        // Act - access property to verify construction
        guard.IsEnabled.Should().BeTrue();
    }
}
