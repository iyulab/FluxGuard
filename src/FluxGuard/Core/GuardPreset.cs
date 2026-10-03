namespace FluxGuard.Core;

/// <summary>
/// Guard configuration presets
/// </summary>
public enum GuardPreset
{
    /// <summary>
    /// Minimal configuration - L1 Regex only
    /// L1 only: regex and string checks, in process.
    /// </summary>
    Minimal,

    /// <summary>
    /// Standard configuration (default)
    /// L1 guards, the fuller set.
    /// </summary>
    Standard,

    /// <summary>
    /// Strict configuration
    /// L1 guards with the strictest thresholds.
    /// <para>
    /// Selecting this preset also makes a guard error <b>block</b> the request
    /// (fail-closed) unless <c>FailMode</c> is set explicitly.
    /// </para>
    /// </summary>
    Strict
}
