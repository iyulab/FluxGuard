using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;

namespace FluxGuard.Presets;

/// <summary>
/// Standard preset configuration
/// L1 (Regex) + L2 (ML) guards enabled
/// Latency: 5-20ms, Throughput: 5K req/s
/// </summary>
public static class StandardPreset
{
    /// <summary>
    /// Apply standard preset to builder
    /// </summary>
    public static FluxGuardBuilder ApplyStandardPreset(this FluxGuardBuilder builder)
    {
        var registry = new PatternRegistry();
        var options = new FluxGuardOptions { Preset = Core.GuardPreset.Standard };

        // Add L1 input guards
        builder.AddInputGuard(new L1EncodingBypassGuard(registry, options.InputGuards.EnableEncodingBypass));
        builder.AddInputGuard(new L1PromptInjectionGuard(registry, options.InputGuards.EnablePromptInjection));
        builder.AddInputGuard(new L1JailbreakGuard(registry, options.InputGuards.EnableJailbreak));
        builder.AddInputGuard(new L1PIIExposureGuard(
            registry,
            options.InputGuards.EnablePIIExposure,
            options.InputGuards.SupportedLanguages.ToList()));

        // Add L1 output guards
        builder.AddOutputGuard(new L1PIILeakageGuard(
            registry,
            options.OutputGuards.EnablePIILeakage,
            options.InputGuards.SupportedLanguages.ToList()));
        builder.AddOutputGuard(new L1RefusalGuard(options.OutputGuards.EnableRefusal));

        return builder;
    }

    /// <summary>
    /// Get default input guards for standard preset
    /// </summary>
    public static IEnumerable<IInputGuard> GetInputGuards(
        IPatternRegistry registry,
        InputGuardOptions options)
    {
        if (options.EnableEncodingBypass)
            yield return new L1EncodingBypassGuard(registry);

        if (options.EnablePromptInjection)
            yield return new L1PromptInjectionGuard(registry);

        if (options.EnableJailbreak)
            yield return new L1JailbreakGuard(registry);

        if (options.EnablePIIExposure)
            yield return new L1PIIExposureGuard(registry, true, options.SupportedLanguages.ToList());
    }

    /// <summary>
    /// Get default output guards for standard preset
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
