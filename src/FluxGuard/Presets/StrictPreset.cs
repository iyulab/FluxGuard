using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;

namespace FluxGuard.Presets;

/// <summary>
/// Strict preset configuration
/// L1 (Regex) + L2 (ML) + enhanced thresholds
/// L1 only, with the strictest thresholds. No preset registers L2 - see <c>AddL2Guards</c>.
/// </summary>
public static class StrictPreset
{
    /// <summary>
    /// Apply strict preset to builder
    /// </summary>
    public static FluxGuardBuilder ApplyStrictPreset(this FluxGuardBuilder builder)
    {
        builder.RequestPreset(Core.GuardPreset.Strict);

        // Configure stricter thresholds
        builder.Configure(opts =>
        {
            opts.Preset = Core.GuardPreset.Strict;
            opts.BlockThreshold = 0.8;      // Lower than standard (0.9)
            opts.FlagThreshold = 0.5;       // Lower than standard (0.7)
            opts.EscalationThreshold = 0.3; // Lower than standard (0.5)
        });

        return builder;
    }

    /// <summary>
    /// Get strict input guards
    /// </summary>
    public static IEnumerable<IInputGuard> GetInputGuards(
        IPatternRegistry registry,
        InputGuardOptions options)
    {
        yield return new L1EncodingBypassGuard(
            registry,
            options.EnableEncodingBypass,
            invisibleCharThreshold: 3,
            homoglyphThreshold: 5);

        yield return new L1PromptInjectionGuard(
            registry,
            options.EnablePromptInjection,
            escalationThreshold: 0.3);

        yield return new L1JailbreakGuard(
            registry,
            options.EnableJailbreak,
            escalationThreshold: 0.3);

        if (options.EnablePIIExposure)
            yield return new L1PIIExposureGuard(registry, true, options.SupportedLanguages.ToList());
    }

    /// <summary>
    /// Get strict output guards
    /// </summary>
    public static IEnumerable<IOutputGuard> GetOutputGuards(
        IPatternRegistry registry,
        OutputGuardOptions options,
        IList<string> supportedLanguages)
    {
        if (options.EnablePIILeakage)
            yield return new L1PIILeakageGuard(registry, true, supportedLanguages.ToList());

        if (options.EnableRefusal)
            yield return new L1RefusalGuard();
    }
}
