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
    /// Fail mode. When left unset, it is derived from <see cref="Preset"/>:
    /// <see cref="GuardPreset.Strict"/> resolves to <see cref="FailMode.Closed"/>,
    /// every other preset to <see cref="FailMode.Open"/> (availability first).
    /// Assigning this property always wins, whichever order it is set in.
    /// <para>
    /// <b>Security note:</b> with <see cref="FailMode.Open"/>, a guard that throws (e.g. a regex
    /// match timeout on a very long input) is logged and skipped — that request passes without the
    /// failed guard's verdict. When guard verdicts are enforced (blocking mode), configure
    /// <see cref="FailMode.Closed"/> so a guard error blocks the request instead of silently
    /// bypassing detection.
    /// </para>
    /// </summary>
    public FailMode FailMode
    {
        get => _failMode ?? DefaultFailModeFor(Preset);
        set => _failMode = value;
    }

    private FailMode? _failMode;

    /// <summary>
    /// Whether <see cref="FailMode"/> was assigned explicitly rather than derived from the preset.
    /// </summary>
    internal bool IsFailModeExplicitlySet => _failMode.HasValue;

    /// <summary>
    /// Choosing <see cref="GuardPreset.Strict"/> states "security over availability"; the fail mode
    /// follows that intent unless the consumer says otherwise.
    /// </summary>
    private static FailMode DefaultFailModeFor(GuardPreset preset)
        => preset == GuardPreset.Strict ? FailMode.Closed : FailMode.Open;

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

    /// <summary>
    /// Copies this configuration onto <paramref name="target"/>.
    /// Used when options resolved from DI are handed to a builder.
    /// </summary>
    internal void CopyTo(FluxGuardOptions target)
    {
        target.Preset = Preset;
        // Copy only an explicit fail mode — an unset one must stay unset so it keeps resolving
        // from the preset on the other side of this hop.
        if (IsFailModeExplicitlySet)
        {
            target.FailMode = FailMode;
        }
        target.LogLevel = LogLevel;
        target.EnableL2Guards = EnableL2Guards;
        target.EnableL3Escalation = EnableL3Escalation;
        target.BlockThreshold = BlockThreshold;
        target.FlagThreshold = FlagThreshold;
        target.EscalationThreshold = EscalationThreshold;
        target.EscalationTimeoutMs = EscalationTimeoutMs;
        target.GuardTimeoutMs = GuardTimeoutMs;
        target.InputGuards = InputGuards;
        target.OutputGuards = OutputGuards;
    }
}
