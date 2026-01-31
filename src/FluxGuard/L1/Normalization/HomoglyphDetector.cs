using System.Text;

namespace FluxGuard.L1.Normalization;

/// <summary>
/// Homoglyph detection and normalization
/// Converts visually similar characters to ASCII to prevent bypass attacks
/// </summary>
public static class HomoglyphDetector
{
    // Homoglyph mapping table (visually similar character -> ASCII)
    private static readonly Dictionary<char, char> HomoglyphMap = new()
    {
        // Cyrillic -> Latin
        ['А'] = 'A', ['а'] = 'a',
        ['В'] = 'B',
        ['С'] = 'C', ['с'] = 'c',
        ['Е'] = 'E', ['е'] = 'e',
        ['Н'] = 'H',
        ['І'] = 'I', ['і'] = 'i',
        ['К'] = 'K',
        ['М'] = 'M',
        ['О'] = 'O', ['о'] = 'o',
        ['Р'] = 'P', ['р'] = 'p',
        ['Т'] = 'T',
        ['Х'] = 'X', ['х'] = 'x',
        ['У'] = 'Y', ['у'] = 'y',

        // Greek -> Latin
        ['Α'] = 'A', ['α'] = 'a',
        ['Β'] = 'B', ['β'] = 'B',
        ['Ε'] = 'E', ['ε'] = 'e',
        ['Η'] = 'H',
        ['Ι'] = 'I', ['ι'] = 'i',
        ['Κ'] = 'K', ['κ'] = 'k',
        ['Μ'] = 'M',
        ['Ν'] = 'N', ['ν'] = 'v',
        ['Ο'] = 'O', ['ο'] = 'o',
        ['Ρ'] = 'P', ['ρ'] = 'p',
        ['Τ'] = 'T', ['τ'] = 't',
        ['Χ'] = 'X', ['χ'] = 'x',
        ['Υ'] = 'Y',
        ['Ζ'] = 'Z', ['ζ'] = 'z',

        // Full-width -> ASCII
        ['Ａ'] = 'A', ['ａ'] = 'a',
        ['Ｂ'] = 'B', ['ｂ'] = 'b',
        ['Ｃ'] = 'C', ['ｃ'] = 'c',
        ['Ｄ'] = 'D', ['ｄ'] = 'd',
        ['Ｅ'] = 'E', ['ｅ'] = 'e',
        ['Ｆ'] = 'F', ['ｆ'] = 'f',
        ['Ｇ'] = 'G', ['ｇ'] = 'g',
        ['Ｈ'] = 'H', ['ｈ'] = 'h',
        ['Ｉ'] = 'I', ['ｉ'] = 'i',
        ['Ｊ'] = 'J', ['ｊ'] = 'j',
        ['Ｋ'] = 'K', ['ｋ'] = 'k',
        ['Ｌ'] = 'L', ['ｌ'] = 'l',
        ['Ｍ'] = 'M', ['ｍ'] = 'm',
        ['Ｎ'] = 'N', ['ｎ'] = 'n',
        ['Ｏ'] = 'O', ['ｏ'] = 'o',
        ['Ｐ'] = 'P', ['ｐ'] = 'p',
        ['Ｑ'] = 'Q', ['ｑ'] = 'q',
        ['Ｒ'] = 'R', ['ｒ'] = 'r',
        ['Ｓ'] = 'S', ['ｓ'] = 's',
        ['Ｔ'] = 'T', ['ｔ'] = 't',
        ['Ｕ'] = 'U', ['ｕ'] = 'u',
        ['Ｖ'] = 'V', ['ｖ'] = 'v',
        ['Ｗ'] = 'W', ['ｗ'] = 'w',
        ['Ｘ'] = 'X', ['ｘ'] = 'x',
        ['Ｙ'] = 'Y', ['ｙ'] = 'y',
        ['Ｚ'] = 'Z', ['ｚ'] = 'z',

        // Full-width numbers
        ['０'] = '0', ['１'] = '1', ['２'] = '2', ['３'] = '3', ['４'] = '4',
        ['５'] = '5', ['６'] = '6', ['７'] = '7', ['８'] = '8', ['９'] = '9',

        // Mathematical symbols -> Latin
        ['ℂ'] = 'C',
        ['ℕ'] = 'N',
        ['ℙ'] = 'P',
        ['ℚ'] = 'Q',
        ['ℝ'] = 'R',
        ['ℤ'] = 'Z',
        ['ℯ'] = 'e',
        ['ℊ'] = 'g',
        ['ℴ'] = 'o',

        // Subscript/Superscript
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
        ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
        ['ᵃ'] = 'a', ['ᵇ'] = 'b', ['ᶜ'] = 'c', ['ᵈ'] = 'd', ['ᵉ'] = 'e',
        ['ᶠ'] = 'f', ['ᵍ'] = 'g', ['ʰ'] = 'h', ['ⁱ'] = 'i', ['ʲ'] = 'j',
        ['ᵏ'] = 'k', ['ˡ'] = 'l', ['ᵐ'] = 'm', ['ⁿ'] = 'n', ['ᵒ'] = 'o',
        ['ᵖ'] = 'p', ['ʳ'] = 'r', ['ˢ'] = 's', ['ᵗ'] = 't', ['ᵘ'] = 'u',
        ['ᵛ'] = 'v', ['ʷ'] = 'w', ['ˣ'] = 'x', ['ʸ'] = 'y', ['ᶻ'] = 'z',

        // Look-alike symbols
        ['∅'] = '0', ['Ø'] = 'O', ['ø'] = 'o',
        ['ℓ'] = 'l', ['ı'] = 'i', ['ȷ'] = 'j',
        ['ʹ'] = '\'', ['ʻ'] = '\'', ['ʼ'] = '\'',
        ['˝'] = '"', ['ʺ'] = '"',
        ['–'] = '-', ['—'] = '-', ['‐'] = '-', ['‑'] = '-', ['‒'] = '-',
        ['⁄'] = '/', ['∕'] = '/',
        ['⋅'] = '.', ['·'] = '.', ['•'] = '.',

        // Special lookalikes
        ['ꓱ'] = 'E', ['ꓯ'] = 'A', ['ꓵ'] = 'U',
        ['ꓳ'] = 'O', ['ꓲ'] = 'I',
    };

    /// <summary>
    /// Normalize homoglyphs to ASCII
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>Normalized text</returns>
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            sb.Append(HomoglyphMap.TryGetValue(c, out var replacement) ? replacement : c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Check if input contains homoglyphs
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>Whether homoglyphs are present</returns>
    public static bool ContainsHomoglyphs(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        foreach (var c in input)
        {
            if (HomoglyphMap.ContainsKey(c))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Count homoglyphs
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>Count of homoglyphs</returns>
    public static int CountHomoglyphs(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        var count = 0;
        foreach (var c in input)
        {
            if (HomoglyphMap.ContainsKey(c))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Get homoglyph transformation details
    /// </summary>
    /// <param name="input">Input text</param>
    /// <returns>List of transformed characters</returns>
    public static IReadOnlyList<(char Original, char Replacement, int Position)> GetTransformations(
        string input)
    {
        if (string.IsNullOrEmpty(input))
            return [];

        var transformations = new List<(char, char, int)>();

        for (var i = 0; i < input.Length; i++)
        {
            if (HomoglyphMap.TryGetValue(input[i], out var replacement))
            {
                transformations.Add((input[i], replacement, i));
            }
        }

        return transformations;
    }
}
