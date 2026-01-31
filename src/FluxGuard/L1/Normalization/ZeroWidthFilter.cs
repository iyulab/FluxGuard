using System.Globalization;
using System.Text;

namespace FluxGuard.L1.Normalization;

/// <summary>
/// Zero-width character filter
/// Removes invisible Unicode characters to prevent bypass attacks
/// </summary>
public static partial class ZeroWidthFilter
{
    // Zero-width and invisible character code points
    private static readonly HashSet<int> ZeroWidthCodePoints =
    [
        0x200B, // Zero Width Space
        0x200C, // Zero Width Non-Joiner
        0x200D, // Zero Width Joiner
        0x200E, // Left-to-Right Mark
        0x200F, // Right-to-Left Mark
        0x2060, // Word Joiner
        0x2061, // Function Application
        0x2062, // Invisible Times
        0x2063, // Invisible Separator
        0x2064, // Invisible Plus
        0xFEFF, // Zero Width No-Break Space (BOM)
        0x00AD, // Soft Hyphen
        0x034F, // Combining Grapheme Joiner
        0x061C, // Arabic Letter Mark
        0x115F, // Hangul Choseong Filler
        0x1160, // Hangul Jungseong Filler
        0x17B4, // Khmer Vowel Inherent Aq
        0x17B5, // Khmer Vowel Inherent Aa
        0x180B, // Mongolian Free Variation Selector One
        0x180C, // Mongolian Free Variation Selector Two
        0x180D, // Mongolian Free Variation Selector Three
        0x180E, // Mongolian Vowel Separator
        0x180F, // Mongolian Free Variation Selector Four
    ];

    // Additional ranges: Variation Selectors, Tags
    private static bool IsInvisibleCharacter(int codePoint)
    {
        // Zero-width characters
        if (ZeroWidthCodePoints.Contains(codePoint))
            return true;

        // Variation Selectors (FE00-FE0F)
        if (codePoint is >= 0xFE00 and <= 0xFE0F)
            return true;

        // Variation Selectors Supplement (E0100-E01EF)
        if (codePoint is >= 0xE0100 and <= 0xE01EF)
            return true;

        // Tag characters (E0000-E007F) - can be used maliciously
        if (codePoint is >= 0xE0000 and <= 0xE007F)
            return true;

        // Interlinear Annotation (FFF9-FFFB)
        if (codePoint is >= 0xFFF9 and <= 0xFFFB)
            return true;

        // Bidirectional Format Controls (202A-202E, 2066-2069)
        if (codePoint is >= 0x202A and <= 0x202E)
            return true;
        if (codePoint is >= 0x2066 and <= 0x2069)
            return true;

        return false;
    }

    /// <summary>
    /// Remove zero-width and invisible characters
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>Filtered text</returns>
    public static string Filter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        var enumerator = StringInfo.GetTextElementEnumerator(input);

        while (enumerator.MoveNext())
        {
            var textElement = enumerator.GetTextElement();
            var shouldInclude = true;

            foreach (var rune in textElement.EnumerateRunes())
            {
                if (IsInvisibleCharacter(rune.Value))
                {
                    shouldInclude = false;
                    break;
                }
            }

            if (shouldInclude)
            {
                sb.Append(textElement);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Check if input contains invisible characters
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>Whether invisible characters are present</returns>
    public static bool ContainsInvisibleCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        foreach (var rune in input.EnumerateRunes())
        {
            if (IsInvisibleCharacter(rune.Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Count invisible characters
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>Count of invisible characters</returns>
    public static int CountInvisibleCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        var count = 0;
        foreach (var rune in input.EnumerateRunes())
        {
            if (IsInvisibleCharacter(rune.Value))
                count++;
        }

        return count;
    }
}
