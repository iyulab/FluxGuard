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
    /// Standard configuration - L1 + L2 enabled (default)
    /// L1 only, with the fuller guard set. No preset registers L2 - see <c>AddL2Guards</c>.
    /// </summary>
    Standard,

    /// <summary>
    /// Strict configuration - L1 + L2 + enhanced thresholds
    /// L1 only, with the strictest thresholds. No preset registers L2 - see <c>AddL2Guards</c>.
    /// <para>
    /// Selecting this preset also makes a guard error <b>block</b> the request
    /// (fail-closed) unless <c>FailMode</c> is set explicitly.
    /// </para>
    /// </summary>
    Strict
}
