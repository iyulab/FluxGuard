using System.Text;

namespace FluxGuard.L1.Normalization;

/// <summary>
/// Unicode normalizer
/// NFKC normalization, zero-width character removal, homoglyph detection
/// </summary>
public sealed class UnicodeNormalizer
{
    private readonly bool _enableNormalization;
    private readonly bool _enableZeroWidthFiltering;
    private readonly bool _enableHomoglyphDetection;

    public UnicodeNormalizer(
        bool enableNormalization = true,
        bool enableZeroWidthFiltering = true,
        bool enableHomoglyphDetection = true)
    {
        _enableNormalization = enableNormalization;
        _enableZeroWidthFiltering = enableZeroWidthFiltering;
        _enableHomoglyphDetection = enableHomoglyphDetection;
    }

    /// <summary>
    /// Perform text normalization
    /// </summary>
    /// <param name="input">Original text</param>
    /// <returns>Normalized text</returns>
    public string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;

        // 1. Remove zero-width characters
        if (_enableZeroWidthFiltering)
        {
            result = ZeroWidthFilter.Filter(result);
        }

        // 2. NFKC normalization (compatibility decomposition followed by canonical composition)
        if (_enableNormalization)
        {
            result = result.Normalize(NormalizationForm.FormKC);
        }

        // 3. Homoglyph normalization
        if (_enableHomoglyphDetection)
        {
            result = HomoglyphDetector.Normalize(result);
        }

        return result;
    }

    /// <summary>
    /// Check if homoglyph transformation occurred
    /// </summary>
    /// <param name="original">Original text</param>
    /// <param name="normalized">Normalized text</param>
    /// <returns>Whether homoglyph was detected</returns>
    public bool HasHomoglyphTransformation(string original, string normalized)
    {
        if (!_enableHomoglyphDetection)
            return false;

        var normalizedOriginal = _enableNormalization
            ? original.Normalize(NormalizationForm.FormKC)
            : original;

        return normalizedOriginal != HomoglyphDetector.Normalize(normalizedOriginal);
    }
}
