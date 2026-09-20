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
    {
        yield return new L1PromptInjectionGuard(registry);
        yield return new L1JailbreakGuard(registry);
    }

    /// <summary>
    /// Get minimal output guards
    /// </summary>
    public static IEnumerable<IOutputGuard> GetOutputGuards(IPatternRegistry registry)
    {
        yield return new L1PIILeakageGuard(registry);
    }
}
