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
    /// What the PII leakage guard does with each pattern, by pattern name (case-insensitive) — for example
    /// <c>["EmailAddress"] = PIIAction.Block</c>. A pattern not named keeps its default: High and Critical PII blocks,
    /// everything else flags. Names are as for <see cref="InputGuardOptions.PIIExposureActions"/>; an unknown name throws
    /// <see cref="ArgumentException"/> when the guard is built.
    /// </summary>
    public IDictionary<string, PIIAction> PIILeakageActions { get; set; } =
        new Dictionary<string, PIIAction>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Enable refusal response guard (default: true)
    /// </summary>
    public bool EnableRefusal { get; set; } = true;

    /// <summary>
    /// Maximum output length (default: ~32000 tokens equivalent)
    /// </summary>
    public int MaxOutputLength { get; set; } = 128000;
}
