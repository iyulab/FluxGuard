namespace FluxGuard.Core;

/// <summary>
/// Guard configuration presets
/// </summary>
public enum GuardPreset
{
    /// <summary>
    /// Minimal configuration - L1 Regex only
    /// Latency &lt;1ms, Throughput 100K+ req/s
    /// </summary>
    Minimal,

    /// <summary>
    /// Standard configuration - L1 + L2 enabled (default)
    /// Latency 5-20ms, Throughput 5K req/s
    /// </summary>
    Standard,

    /// <summary>
    /// Strict configuration - L1 + L2 + enhanced thresholds
    /// Latency 10-30ms, Throughput 3K req/s
    /// </summary>
    Strict
}
