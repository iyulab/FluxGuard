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
        => builder.RequestPreset(Core.GuardPreset.Standard);

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
