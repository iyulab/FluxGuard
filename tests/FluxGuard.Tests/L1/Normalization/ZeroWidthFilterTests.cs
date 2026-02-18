using FluentAssertions;
using FluxGuard.L1.Normalization;
using Xunit;

namespace FluxGuard.Tests.L1.Normalization;

public class ZeroWidthFilterTests
{
    #region Filter

    [Fact]
    public void Filter_NullInput_ReturnsNull()
    {
        ZeroWidthFilter.Filter(null!).Should().BeNull();
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsEmpty()
    {
        ZeroWidthFilter.Filter("").Should().BeEmpty();
    }

    [Fact]
    public void Filter_PlainAscii_ReturnsUnchanged()
    {
        ZeroWidthFilter.Filter("Hello world").Should().Be("Hello world");
    }

    [Fact]
    public void Filter_ZeroWidthSpace_Removes()
    {
        // U+200B Zero Width Space
        var input = "He\u200Bllo";
        ZeroWidthFilter.Filter(input).Should().Be("Hello");
    }

    [Fact]
    public void Filter_ZeroWidthJoiner_RemovesTextElement()
    {
        // U+200D Zero Width Joiner — text element "e\u200D" is removed entirely
        var input = "te\u200Dst";
        ZeroWidthFilter.Filter(input).Should().Be("tst");
    }

    [Fact]
    public void Filter_ZeroWidthNonJoiner_RemovesTextElement()
    {
        // U+200C Zero Width Non-Joiner — text element "b\u200C" is removed entirely
        var input = "ab\u200Ccd";
        ZeroWidthFilter.Filter(input).Should().Be("acd");
    }

    [Fact]
    public void Filter_BOM_Removes()
    {
        // U+FEFF Byte Order Mark / Zero Width No-Break Space
        var input = "\uFEFFHello";
        ZeroWidthFilter.Filter(input).Should().Be("Hello");
    }

    [Fact]
    public void Filter_SoftHyphen_Removes()
    {
        // U+00AD Soft Hyphen
        var input = "ig\u00ADnore";
        ZeroWidthFilter.Filter(input).Should().Be("ignore");
    }

    [Fact]
    public void Filter_BidiControls_RemovesTextElement()
    {
        // U+202A Left-to-Right Embedding — standalone invisible char removed
        var input = "text\u202A here";
        var result = ZeroWidthFilter.Filter(input);
        // \u202A may merge with adjacent text element — verify invisible is gone
        result.Should().NotContain("\u202A");
    }

    [Fact]
    public void Filter_MultipleInvisibleChars_RemovesAffectedElements()
    {
        // Text element grouping removes elements containing invisible chars
        var input = "\u200BHello";
        var result = ZeroWidthFilter.Filter(input);
        // Standalone \u200B removed, "Hello" survives
        result.Should().Be("Hello");
    }

    [Fact]
    public void Filter_WordJoiner_Removes()
    {
        // U+2060 Word Joiner
        var input = "word\u2060joiner";
        ZeroWidthFilter.Filter(input).Should().Be("wordjoiner");
    }

    [Fact]
    public void Filter_VariationSelector_RemovesAffectedElement()
    {
        // U+FE0F Variation Selector-16 — may merge with preceding char
        var input = "star\uFE0F text";
        var result = ZeroWidthFilter.Filter(input);
        // Variation selector removed (with its text element)
        result.Should().NotContain("\uFE0F");
    }

    #endregion

    #region ContainsInvisibleCharacters

    [Fact]
    public void ContainsInvisibleCharacters_NullInput_ReturnsFalse()
    {
        ZeroWidthFilter.ContainsInvisibleCharacters(null!).Should().BeFalse();
    }

    [Fact]
    public void ContainsInvisibleCharacters_EmptyInput_ReturnsFalse()
    {
        ZeroWidthFilter.ContainsInvisibleCharacters("").Should().BeFalse();
    }

    [Fact]
    public void ContainsInvisibleCharacters_PlainText_ReturnsFalse()
    {
        ZeroWidthFilter.ContainsInvisibleCharacters("Hello world 123").Should().BeFalse();
    }

    [Fact]
    public void ContainsInvisibleCharacters_WithZeroWidth_ReturnsTrue()
    {
        ZeroWidthFilter.ContainsInvisibleCharacters("He\u200Bllo").Should().BeTrue();
    }

    [Fact]
    public void ContainsInvisibleCharacters_WithBOM_ReturnsTrue()
    {
        ZeroWidthFilter.ContainsInvisibleCharacters("\uFEFFtext").Should().BeTrue();
    }

    #endregion

    #region CountInvisibleCharacters

    [Fact]
    public void CountInvisibleCharacters_NullInput_ReturnsZero()
    {
        ZeroWidthFilter.CountInvisibleCharacters(null!).Should().Be(0);
    }

    [Fact]
    public void CountInvisibleCharacters_EmptyInput_ReturnsZero()
    {
        ZeroWidthFilter.CountInvisibleCharacters("").Should().Be(0);
    }

    [Fact]
    public void CountInvisibleCharacters_NoInvisible_ReturnsZero()
    {
        ZeroWidthFilter.CountInvisibleCharacters("Hello").Should().Be(0);
    }

    [Fact]
    public void CountInvisibleCharacters_MultipleInvisible_ReturnsCorrectCount()
    {
        // 3 invisible chars: U+200B, U+200C, U+FEFF
        var input = "\u200BHe\u200Cllo\uFEFF";
        ZeroWidthFilter.CountInvisibleCharacters(input).Should().Be(3);
    }

    #endregion
}
