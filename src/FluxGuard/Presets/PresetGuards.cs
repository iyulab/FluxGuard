using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.L1.Patterns;

namespace FluxGuard.Presets;

/// <summary>
/// The one place a <see cref="GuardPreset"/> becomes a list of guards. Both ways of building a guard - the builder and
/// the service collection - go through it, so the same options give the same pipeline.
/// </summary>
internal static class PresetGuards
{
    public static IEnumerable<IInputGuard> InputGuards(
        GuardPreset preset, IPatternRegistry registry, FluxGuardOptions options) => preset switch
    {
        GuardPreset.Minimal => MinimalPreset.GetInputGuards(registry),
        GuardPreset.Standard => StandardPreset.GetInputGuards(registry, options.InputGuards),
        GuardPreset.Strict => StrictPreset.GetInputGuards(registry, options.InputGuards),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown guard preset")
    };

    public static IEnumerable<IOutputGuard> OutputGuards(
        GuardPreset preset, IPatternRegistry registry, FluxGuardOptions options) => preset switch
    {
        GuardPreset.Minimal => MinimalPreset.GetOutputGuards(registry),
        GuardPreset.Standard => StandardPreset.GetOutputGuards(
            registry, options.OutputGuards, options.InputGuards.SupportedLanguages),
        GuardPreset.Strict => StrictPreset.GetOutputGuards(
            registry, options.OutputGuards, options.InputGuards.SupportedLanguages),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown guard preset")
    };
}
