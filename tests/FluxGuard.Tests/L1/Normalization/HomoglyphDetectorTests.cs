using FluentAssertions;
using FluxGuard.L1.Normalization;
using Xunit;

namespace FluxGuard.Tests.L1.Normalization;

public class HomoglyphDetectorTests
{
    #region Normalize

    [Fact]
    public void Normalize_NullInput_ReturnsNull()
    {
        HomoglyphDetector.Normalize(null!).Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        HomoglyphDetector.Normalize("").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_PlainAscii_ReturnsUnchanged()
    {
        HomoglyphDetector.Normalize("Hello world").Should().Be("Hello world");
    }

    [Fact]
    public void Normalize_CyrillicA_ConvertedToLatinA()
    {
        // Cyrillic А (U+0410) -> Latin A
        HomoglyphDetector.Normalize("\u0410").Should().Be("A");
    }

    [Fact]
    public void Normalize_CyrillicLowerA_ConvertedToLatina()
    {
        // Cyrillic а (U+0430) -> Latin a
        HomoglyphDetector.Normalize("\u0430").Should().Be("a");
    }

    [Fact]
    public void Normalize_CyrillicMixedWithLatin_NormalizesAll()
    {
        // "Неllo" with Cyrillic Н (U+041D) and е (U+0435)
        var input = "\u041D\u0435llo";
        var result = HomoglyphDetector.Normalize(input);
        result.Should().Be("Hello");
    }

    [Fact]
    public void Normalize_GreekAlpha_ConvertedToLatinA()
    {
        // Greek Α (U+0391) -> Latin A
        HomoglyphDetector.Normalize("\u0391").Should().Be("A");
    }

    [Fact]
    public void Normalize_FullWidthLetters_ConvertedToAscii()
    {
        // Full-width Ｈｅｌｌｏ
        var input = "\uFF28\uFF45\uFF4C\uFF4C\uFF4F";
        HomoglyphDetector.Normalize(input).Should().Be("Hello");
    }

    [Fact]
    public void Normalize_FullWidthNumbers_ConvertedToAscii()
    {
        // Full-width ０１２３
        var input = "\uFF10\uFF11\uFF12\uFF13";
        HomoglyphDetector.Normalize(input).Should().Be("0123");
    }

    [Fact]
    public void Normalize_SuperscriptNumbers_ConvertedToAscii()
    {
        // ¹²³
        HomoglyphDetector.Normalize("\u00B9\u00B2\u00B3").Should().Be("123");
    }

    [Fact]
    public void Normalize_SubscriptNumbers_ConvertedToAscii()
    {
        // ₀₁₂
        HomoglyphDetector.Normalize("\u2080\u2081\u2082").Should().Be("012");
    }

    [Fact]
    public void Normalize_MathSymbols_ConvertedToAscii()
    {
        // ℂ → C, ℝ → R
        HomoglyphDetector.Normalize("\u2102\u211D").Should().Be("CR");
    }

    [Fact]
    public void Normalize_DashVariants_ConvertedToHyphen()
    {
        // En dash –, Em dash —, non-breaking hyphen ‑
        HomoglyphDetector.Normalize("\u2013\u2014\u2011").Should().Be("---");
    }

    #endregion

    #region ContainsHomoglyphs

    [Fact]
    public void ContainsHomoglyphs_NullInput_ReturnsFalse()
    {
        HomoglyphDetector.ContainsHomoglyphs(null!).Should().BeFalse();
    }

    [Fact]
    public void ContainsHomoglyphs_EmptyInput_ReturnsFalse()
    {
        HomoglyphDetector.ContainsHomoglyphs("").Should().BeFalse();
    }

    [Fact]
    public void ContainsHomoglyphs_PlainAscii_ReturnsFalse()
    {
        HomoglyphDetector.ContainsHomoglyphs("Hello world 123").Should().BeFalse();
    }

    [Fact]
    public void ContainsHomoglyphs_WithCyrillic_ReturnsTrue()
    {
        HomoglyphDetector.ContainsHomoglyphs("H\u0435llo").Should().BeTrue();
    }

    [Fact]
    public void ContainsHomoglyphs_WithFullWidth_ReturnsTrue()
    {
        HomoglyphDetector.ContainsHomoglyphs("\uFF28ello").Should().BeTrue();
    }

    #endregion

    #region CountHomoglyphs

    [Fact]
    public void CountHomoglyphs_NullInput_ReturnsZero()
    {
        HomoglyphDetector.CountHomoglyphs(null!).Should().Be(0);
    }

    [Fact]
    public void CountHomoglyphs_EmptyInput_ReturnsZero()
    {
        HomoglyphDetector.CountHomoglyphs("").Should().Be(0);
    }

    [Fact]
    public void CountHomoglyphs_PlainAscii_ReturnsZero()
    {
        HomoglyphDetector.CountHomoglyphs("Hello").Should().Be(0);
    }

    [Fact]
    public void CountHomoglyphs_MultipleCyrillic_ReturnsCorrectCount()
    {
        // 3 Cyrillic chars: А (0410), е (0435), о (043E)
        var input = "\u0410b\u0435d\u043E";
        HomoglyphDetector.CountHomoglyphs(input).Should().Be(3);
    }

    #endregion

    #region GetTransformations

    [Fact]
    public void GetTransformations_NullInput_ReturnsEmpty()
    {
        HomoglyphDetector.GetTransformations(null!).Should().BeEmpty();
    }

    [Fact]
    public void GetTransformations_EmptyInput_ReturnsEmpty()
    {
        HomoglyphDetector.GetTransformations("").Should().BeEmpty();
    }

    [Fact]
    public void GetTransformations_PlainAscii_ReturnsEmpty()
    {
        HomoglyphDetector.GetTransformations("Hello").Should().BeEmpty();
    }

    [Fact]
    public void GetTransformations_WithHomoglyphs_ReturnsCorrectDetails()
    {
        // Cyrillic а (U+0430) at position 1
        var input = "H\u0430llo";
        var transformations = HomoglyphDetector.GetTransformations(input);

        transformations.Should().HaveCount(1);
        transformations[0].Original.Should().Be('\u0430');
        transformations[0].Replacement.Should().Be('a');
        transformations[0].Position.Should().Be(1);
    }

    [Fact]
    public void GetTransformations_MultipleHomoglyphs_ReturnsAll()
    {
        // Full-width Ｈ (FF28) at 0, Cyrillic е (0435) at 1
        var input = "\uFF28\u0435llo";
        var transformations = HomoglyphDetector.GetTransformations(input);

        transformations.Should().HaveCount(2);
        transformations[0].Position.Should().Be(0);
        transformations[1].Position.Should().Be(1);
    }

    #endregion
}
