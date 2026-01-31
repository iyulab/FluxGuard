using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;

namespace FluxGuard.Presets;

/// <summary>
/// Strict preset configuration
/// L1 (Regex) + L2 (ML) + enhanced thresholds
/// Latency: 10-30ms, Throughput: 3K req/s
/// </summary>
public static class StrictPreset
{
    /// <summary>
    /// Apply strict preset to builder
    /// </summary>
    public static FluxGuardBuilder ApplyStrictPreset(this FluxGuardBuilder builder)
    {
        var registry = new PatternRegistry();
        var options = new FluxGuardOptions { Preset = Core.GuardPreset.Strict };

        // All L1 input guards with lower thresholds
        builder.AddInputGuard(new L1EncodingBypassGuard(
            registry,
            isEnabled: true,
            invisibleCharThreshold: 3,  // Stricter
            homoglyphThreshold: 5));    // Stricter

        builder.AddInputGuard(new L1PromptInjectionGuard(
            registry,
            isEnabled: true,
            escalationThreshold: 0.3)); // Lower threshold

        builder.AddInputGuard(new L1JailbreakGuard(
            registry,
            isEnabled: true,
            escalationThreshold: 0.3)); // Lower threshold

        builder.AddInputGuard(new L1PIIExposureGuard(
            registry,
            isEnabled: true,
            options.InputGuards.SupportedLanguages.ToList()));

        // All output guards
        builder.AddOutputGuard(new L1PIILeakageGuard(
            registry,
            isEnabled: true,
            options.InputGuards.SupportedLanguages.ToList()));
        builder.AddOutputGuard(new L1RefusalGuard(true));

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
