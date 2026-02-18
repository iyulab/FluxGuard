using FluentAssertions;
using FluxGuard.L1.Normalization;
using Xunit;

namespace FluxGuard.Tests.L1.Normalization;

public class UnicodeNormalizerTests
{
    #region Normalize - All features enabled

    [Fact]
    public void Normalize_NullInput_ReturnsNull()
    {
        var normalizer = new UnicodeNormalizer();
        normalizer.Normalize(null!).Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        var normalizer = new UnicodeNormalizer();
        normalizer.Normalize("").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_PlainAscii_ReturnsUnchanged()
    {
        var normalizer = new UnicodeNormalizer();
        normalizer.Normalize("Hello world").Should().Be("Hello world");
    }

    [Fact]
    public void Normalize_ZeroWidthChars_Removed()
    {
        var normalizer = new UnicodeNormalizer();
        normalizer.Normalize("He\u200Bllo").Should().Be("Hello");
    }

    [Fact]
    public void Normalize_CyrillicHomoglyphs_Converted()
    {
        var normalizer = new UnicodeNormalizer();
        // Cyrillic а (0430) -> Latin a
        normalizer.Normalize("H\u0430llo").Should().Be("Hallo");
    }

    [Fact]
    public void Normalize_CombinedAttack_AllNormalized()
    {
        var normalizer = new UnicodeNormalizer();
        // Zero-width + Cyrillic homoglyph
        var input = "\u200BH\u0435llo";
        normalizer.Normalize(input).Should().Be("Hello");
    }

    #endregion

    #region Normalize - Feature toggles

    [Fact]
    public void Normalize_ZeroWidthDisabled_KeepsInvisibleChars()
    {
        var normalizer = new UnicodeNormalizer(
            enableZeroWidthFiltering: false,
            enableNormalization: false,
            enableHomoglyphDetection: false);

        var input = "He\u200Bllo";
        normalizer.Normalize(input).Should().Be(input);
    }

    [Fact]
    public void Normalize_NormalizationDisabled_SkipsNFKC()
    {
        var normalizer = new UnicodeNormalizer(
            enableNormalization: false,
            enableZeroWidthFiltering: false,
            enableHomoglyphDetection: false);

        var input = "Hello";
        normalizer.Normalize(input).Should().Be("Hello");
    }

    [Fact]
    public void Normalize_HomoglyphDisabled_SkipsHomoglyph()
    {
        var normalizer = new UnicodeNormalizer(
            enableHomoglyphDetection: false,
            enableZeroWidthFiltering: false,
            enableNormalization: false);

        // Cyrillic а should remain
        var input = "H\u0430llo";
        normalizer.Normalize(input).Should().Be("H\u0430llo");
    }

    [Fact]
    public void Normalize_OnlyZeroWidthEnabled_OnlyRemovesInvisible()
    {
        var normalizer = new UnicodeNormalizer(
            enableZeroWidthFiltering: true,
            enableNormalization: false,
            enableHomoglyphDetection: false);

        // Zero-width removed, Cyrillic kept
        var input = "\u200BH\u0430llo";
        normalizer.Normalize(input).Should().Be("H\u0430llo");
    }

    [Fact]
    public void Normalize_OnlyHomoglyphEnabled_OnlyConvertsHomoglyphs()
    {
        var normalizer = new UnicodeNormalizer(
            enableZeroWidthFiltering: false,
            enableNormalization: false,
            enableHomoglyphDetection: true);

        // Zero-width kept, Cyrillic converted
        var input = "\u200BH\u0430llo";
        normalizer.Normalize(input).Should().Be("\u200BHallo");
    }

    #endregion

    #region HasHomoglyphTransformation

    [Fact]
    public void HasHomoglyphTransformation_PlainAscii_ReturnsFalse()
    {
        var normalizer = new UnicodeNormalizer();
        normalizer.HasHomoglyphTransformation("Hello", "Hello").Should().BeFalse();
    }

    [Fact]
    public void HasHomoglyphTransformation_WithCyrillic_ReturnsTrue()
    {
        var normalizer = new UnicodeNormalizer();
        // Original has Cyrillic а
        var original = "H\u0430llo";
        var normalized = "Hallo";
        normalizer.HasHomoglyphTransformation(original, normalized).Should().BeTrue();
    }

    [Fact]
    public void HasHomoglyphTransformation_HomoglyphDisabled_ReturnsFalse()
    {
        var normalizer = new UnicodeNormalizer(enableHomoglyphDetection: false);
        var original = "H\u0430llo";
        normalizer.HasHomoglyphTransformation(original, "Hallo").Should().BeFalse();
    }

    #endregion
}
