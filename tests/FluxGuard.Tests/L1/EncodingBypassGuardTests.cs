using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Normalization;
using FluxGuard.L1.Patterns;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.L1;

public class EncodingBypassGuardTests
{
    private readonly PatternRegistry _registry;
    private readonly L1EncodingBypassGuard _guard;

    public EncodingBypassGuardTests()
    {
        _registry = new PatternRegistry();
        _guard = new L1EncodingBypassGuard(_registry);
    }

    [Fact]
    public async Task CheckAsync_SafeInput_ReturnsPass()
    {
        // Arrange
        var context = new GuardContext
        {
            OriginalInput = "Hello, how are you?"
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_InputWithManyInvisibleChars_DetectsAttack()
    {
        // Arrange
        var inputWithZeroWidth = "Hello\u200B\u200B\u200B\u200B\u200B World"; // 5 zero-width spaces
        var context = new GuardContext
        {
            OriginalInput = inputWithZeroWidth
        };

        // Act
        var result = await _guard.CheckAsync(context);

        // Assert
        result.Passed.Should().BeFalse();
        result.Pattern.Should().Be("InvisibleCharacters");
    }
}

public class ZeroWidthFilterTests
{
    [Fact]
    public void Filter_RemovesZeroWidthSpaces()
    {
        // Arrange
        var input = "Hello\u200BWorld";

        // Act
        var result = ZeroWidthFilter.Filter(input);

        // Assert
        result.Should().Be("HelloWorld");
    }

    [Fact]
    public void ContainsInvisibleCharacters_DetectsZeroWidth()
    {
        // Arrange
        var input = "Hello\u200BWorld";

        // Act
        var result = ZeroWidthFilter.ContainsInvisibleCharacters(input);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CountInvisibleCharacters_CountsCorrectly()
    {
        // Arrange
        var input = "Hello\u200B\u200CWorld\u200D";

        // Act
        var result = ZeroWidthFilter.CountInvisibleCharacters(input);

        // Assert
        result.Should().Be(3);
    }
}

public class HomoglyphDetectorTests
{
    [Fact]
    public void Normalize_ConvertsCyrillicToLatin()
    {
        // Arrange - Cyrillic 'а' looks like Latin 'a'
        var input = "Hеllo"; // 'е' is Cyrillic

        // Act
        var result = HomoglyphDetector.Normalize(input);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public void ContainsHomoglyphs_DetectsCyrillic()
    {
        // Arrange
        var input = "Hеllo"; // 'е' is Cyrillic

        // Act
        var result = HomoglyphDetector.ContainsHomoglyphs(input);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CountHomoglyphs_CountsCorrectly()
    {
        // Arrange
        var input = "Неllo"; // 'Н' and 'е' are Cyrillic

        // Act
        var result = HomoglyphDetector.CountHomoglyphs(input);

        // Assert
        result.Should().Be(2);
    }
}
