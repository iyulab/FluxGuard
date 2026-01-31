using FluxGuard.Core;
using Microsoft.Extensions.Logging;

namespace FluxGuard.Configuration;

/// <summary>
/// FluxGuard global options
/// </summary>
public sealed class FluxGuardOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "FluxGuard";

    /// <summary>
    /// Guard preset (default: Standard)
    /// </summary>
    public GuardPreset Preset { get; set; } = GuardPreset.Standard;

    /// <summary>
    /// Fail mode (default: Open - availability first)
    /// </summary>
    public FailMode FailMode { get; set; } = FailMode.Open;

    /// <summary>
    /// Log level (default: Warning - blocks/errors only)
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Whether L2 ML guards are enabled (default: true)
    /// </summary>
    public bool EnableL2Guards { get; set; } = true;

    /// <summary>
    /// Whether L3 escalation is enabled (default: false, requires WithRemoteGuard())
    /// </summary>
    public bool EnableL3Escalation { get; set; }

    /// <summary>
    /// Block threshold (default: 0.9)
    /// </summary>
    public double BlockThreshold { get; set; } = 0.9;

    /// <summary>
    /// Flag threshold (default: 0.7)
    /// </summary>
    public double FlagThreshold { get; set; } = 0.7;

    /// <summary>
    /// Escalation threshold (default: 0.5)
    /// </summary>
    public double EscalationThreshold { get; set; } = 0.5;

    /// <summary>
    /// Guard timeout in milliseconds (default: 5000)
    /// </summary>
    public int GuardTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Escalation timeout in milliseconds (default: 5000)
    /// </summary>
    public int EscalationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Input guard options
    /// </summary>
    public InputGuardOptions InputGuards { get; set; } = new();

    /// <summary>
    /// Output guard options
    /// </summary>
    public OutputGuardOptions OutputGuards { get; set; } = new();
}
