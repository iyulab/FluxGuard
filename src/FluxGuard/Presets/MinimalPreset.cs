using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;

namespace FluxGuard.Presets;

/// <summary>
/// Minimal preset configuration
/// L1 (Regex) only - fastest, lowest latency
/// Latency: &lt;1ms, Throughput: 100K+ req/s
/// </summary>
public static class MinimalPreset
{
    /// <summary>
    /// Apply minimal preset to builder
    /// </summary>
    public static FluxGuardBuilder ApplyMinimalPreset(this FluxGuardBuilder builder)
    {
        var registry = new PatternRegistry();

        // Only critical L1 guards
        builder.AddInputGuard(new L1PromptInjectionGuard(registry));
        builder.AddInputGuard(new L1JailbreakGuard(registry));

        // Minimal output guards
        builder.AddOutputGuard(new L1PIILeakageGuard(registry));

        // Disable L2 guards
        builder.DisableL2Guards();

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
