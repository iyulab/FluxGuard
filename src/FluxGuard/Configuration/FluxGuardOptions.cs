using FluxGuard.Core;

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
    /// Whether L3 escalation is enabled (default: false, requires WithRemoteGuard())
    /// </summary>
    public bool EnableL3Escalation { get; set; }

    /// <summary>
    /// Score at or above which a check blocks. When left unset, it is derived from <see cref="Preset"/>:
    /// 0.8 for <see cref="GuardPreset.Strict"/>, 0.9 otherwise. Assigning it always wins, whichever order it is set in.
    /// </summary>
    public double BlockThreshold
    {
        get => _blockThreshold ?? (Preset == GuardPreset.Strict ? 0.8 : 0.9);
        set => _blockThreshold = value;
    }

    private double? _blockThreshold;

    /// <summary>
    /// Score at or above which a check is flagged. When left unset, it is derived from <see cref="Preset"/>:
    /// 0.5 for <see cref="GuardPreset.Strict"/>, 0.7 otherwise. Assigning it always wins, whichever order it is set in.
    /// </summary>
    public double FlagThreshold
    {
        get => _flagThreshold ?? (Preset == GuardPreset.Strict ? 0.5 : 0.7);
        set => _flagThreshold = value;
    }

    private double? _flagThreshold;

    /// <summary>
    /// Score at or above which an uncertain check is escalated to the remote (L3) guard. When left unset, it is
    /// derived from <see cref="Preset"/>: 0.3 for <see cref="GuardPreset.Strict"/>, 0.5 otherwise. Assigning it always
    /// wins, whichever order it is set in.
    /// </summary>
    public double EscalationThreshold
    {
        get => _escalationThreshold ?? (Preset == GuardPreset.Strict ? 0.3 : 0.5);
        set => _escalationThreshold = value;
    }

    private double? _escalationThreshold;

    /// <summary>
    /// How long the pipeline waits for one guard, in milliseconds (default: 5000; 0 = no timeout).
    /// A guard that outlives it is handled as a guard error: skipped under <see cref="FailMode.Open"/>,
    /// blocking under <see cref="FailMode.Closed"/>, and reported to <c>OnGuardErrorAsync</c> as a
    /// <see cref="TimeoutException"/> either way.
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
        target.EnableL3Escalation = EnableL3Escalation;
        // Same for the thresholds: an unset one keeps following the preset.
        target._blockThreshold = _blockThreshold;
        target._flagThreshold = _flagThreshold;
        target._escalationThreshold = _escalationThreshold;
        target.EscalationTimeoutMs = EscalationTimeoutMs;
        target.GuardTimeoutMs = GuardTimeoutMs;
        target.InputGuards = InputGuards;
        target.OutputGuards = OutputGuards;
    }
}
