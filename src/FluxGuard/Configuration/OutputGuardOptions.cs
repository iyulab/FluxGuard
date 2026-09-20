namespace FluxGuard.Configuration;

/// <summary>
/// Output guard options
/// </summary>
public sealed class OutputGuardOptions
{
    /// <summary>
    /// Enable PII leakage guard (default: true)
    /// </summary>
    public bool EnablePIILeakage { get; set; } = true;

    /// <summary>
    /// Enable refusal response guard (default: true)
    /// </summary>
    public bool EnableRefusal { get; set; } = true;

    /// <summary>
    /// Maximum output length (default: ~32000 tokens equivalent)
    /// </summary>
    public int MaxOutputLength { get; set; } = 128000;
}
