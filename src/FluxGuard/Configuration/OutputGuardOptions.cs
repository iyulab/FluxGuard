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
    /// Enable format compliance guard (default: false)
    /// </summary>
    public bool EnableFormatCompliance { get; set; }

    /// <summary>
    /// Enable refusal response guard (default: true)
    /// </summary>
    public bool EnableRefusal { get; set; } = true;

    /// <summary>
    /// Enable toxicity guard (default: true)
    /// </summary>
    public bool EnableToxicity { get; set; } = true;

    /// <summary>
    /// Maximum output length (default: ~32000 tokens equivalent)
    /// </summary>
    public int MaxOutputLength { get; set; } = 128000;

    /// <summary>
    /// Enable PII masking (default: false - block only)
    /// </summary>
    public bool EnablePIIMasking { get; set; }

    /// <summary>
    /// PII masking character (default: *)
    /// </summary>
    public char PIIMaskChar { get; set; } = '*';
}
