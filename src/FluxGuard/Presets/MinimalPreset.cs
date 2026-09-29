using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;

namespace FluxGuard.Presets;

/// <summary>
/// Minimal preset configuration
/// L1 (Regex) only - fastest, lowest latency
/// L1 only: regex and string checks, in process.
/// </summary>
public static class MinimalPreset
{
    /// <summary>
    /// Apply minimal preset to builder
    /// </summary>
    public static FluxGuardBuilder ApplyMinimalPreset(this FluxGuardBuilder builder)
    {
        builder.RequestPreset(Core.GuardPreset.Minimal);

        return builder;
    }

    /// <summary>
    /// Get minimal input guards
    /// </summary>
    public static IEnumerable<IInputGuard> GetInputGuards(IPatternRegistry registry)
        => GetInputGuards(registry, new InputGuardOptions());

    /// <summary>
    /// Get minimal output guards
    /// </summary>
    public static IEnumerable<IOutputGuard> GetOutputGuards(IPatternRegistry registry)
        => GetOutputGuards(registry, new OutputGuardOptions(), new InputGuardOptions().SupportedLanguages);

    /// <summary>The minimal input guards, honouring the same switches as the other presets.</summary>
    internal static IEnumerable<IInputGuard> GetInputGuards(IPatternRegistry registry, InputGuardOptions options)
    {
        if (options.EnablePromptInjection)
            yield return new L1PromptInjectionGuard(registry);

        if (options.EnableJailbreak)
            yield return new L1JailbreakGuard(registry);
    }

    /// <summary>The minimal output guard, honouring its switch and the PII language list as the other presets do.</summary>
    internal static IEnumerable<IOutputGuard> GetOutputGuards(
        IPatternRegistry registry,
        OutputGuardOptions options,
        IList<string> supportedLanguages)
    {
        if (options.EnablePIILeakage)
            yield return new L1PIILeakageGuard(registry, true, supportedLanguages.ToList());
    }
}
