namespace FluxGuard.Configuration;

/// <summary>
/// Input guard options
/// </summary>
public sealed class InputGuardOptions
{
    /// <summary>
    /// Enable prompt injection guard (default: true)
    /// </summary>
    public bool EnablePromptInjection { get; set; } = true;

    /// <summary>
    /// Enable jailbreak guard (default: true)
    /// </summary>
    public bool EnableJailbreak { get; set; } = true;

    /// <summary>
    /// Enable encoding bypass guard (default: true)
    /// </summary>
    public bool EnableEncodingBypass { get; set; } = true;

    /// <summary>
    /// Enable PII exposure guard (default: true)
    /// </summary>
    public bool EnablePIIExposure { get; set; } = true;

    /// <summary>
    /// Enable rate limit guard (default: false)
    /// </summary>
    public bool EnableRateLimit { get; set; }

    /// <summary>
    /// Enable content policy guard (default: true)
    /// </summary>
    public bool EnableContentPolicy { get; set; } = true;

    /// <summary>
    /// Supported languages list (default: all 10 languages)
    /// </summary>
    public IList<string> SupportedLanguages { get; set; } =
    [
        "en", "ko", "ja", "zh", "es", "fr", "de", "pt", "ru", "ar"
    ];

    /// <summary>
    /// Maximum input length (default: ~32000 tokens equivalent)
    /// </summary>
    public int MaxInputLength { get; set; } = 128000;

    /// <summary>
    /// Enable Unicode normalization (default: true)
    /// </summary>
    public bool EnableUnicodeNormalization { get; set; } = true;

    /// <summary>
    /// Enable homoglyph detection (default: true)
    /// </summary>
    public bool EnableHomoglyphDetection { get; set; } = true;

    /// <summary>
    /// Enable zero-width character filtering (default: true)
    /// </summary>
    public bool EnableZeroWidthFiltering { get; set; } = true;
}
