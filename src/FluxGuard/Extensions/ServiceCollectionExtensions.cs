using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Hooks;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;
using FluxGuard.Presets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxGuard.Extensions;

/// <summary>
/// DI extension methods for FluxGuard
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add FluxGuard with default configuration (Standard preset)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFluxGuard(this IServiceCollection services)
    {
        return services.AddFluxGuard(_ => { });
    }

    /// <summary>
    /// Add FluxGuard with configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFluxGuard(
        this IServiceCollection services,
        Action<FluxGuardOptions> configure)
    {
        // Register options
        services.Configure(configure);

        // Register pattern registry as singleton
        services.TryAddSingleton<IPatternRegistry, PatternRegistry>();

        // Register hooks
        services.TryAddSingleton<IFluxGuardHooks, FluxGuardHooks>();

        // Register FluxGuard
        services.TryAddSingleton<IFluxGuard>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<FluxGuardOptions>>().Value;
            var registry = sp.GetRequiredService<IPatternRegistry>();
            var hooks = sp.GetRequiredService<IFluxGuardHooks>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var builder = FluxGuardBuilder.Create()
                .Configure(opts =>
                {
                    opts.Preset = options.Preset;
                    opts.FailMode = options.FailMode;
                    opts.LogLevel = options.LogLevel;
                    opts.EnableL2Guards = options.EnableL2Guards;
                    opts.EnableL3Escalation = options.EnableL3Escalation;
                    opts.BlockThreshold = options.BlockThreshold;
                    opts.FlagThreshold = options.FlagThreshold;
                    opts.EscalationThreshold = options.EscalationThreshold;
                    opts.GuardTimeoutMs = options.GuardTimeoutMs;
                    opts.InputGuards = options.InputGuards;
                    opts.OutputGuards = options.OutputGuards;
                })
                .WithHooks(hooks)
                .WithLogging(loggerFactory);

            // Apply preset-based guards
            ApplyPresetGuards(builder, registry, options);

            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Add FluxGuard with configuration from IConfiguration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFluxGuard(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FluxGuardOptions>(
            configuration.GetSection(FluxGuardOptions.SectionName));

        return services.AddFluxGuard(_ => { });
    }

    /// <summary>
    /// Add FluxGuard with builder configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureBuilder">Builder configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddFluxGuard(
        this IServiceCollection services,
        Action<FluxGuardBuilder, IServiceProvider> configureBuilder)
    {
        services.TryAddSingleton<IPatternRegistry, PatternRegistry>();
        services.TryAddSingleton<IFluxGuardHooks, FluxGuardHooks>();

        services.TryAddSingleton<IFluxGuard>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var builder = FluxGuardBuilder.Create()
                .WithLogging(loggerFactory);

            configureBuilder(builder, sp);

            return builder.Build();
        });

        return services;
    }

    private static void ApplyPresetGuards(
        FluxGuardBuilder builder,
        IPatternRegistry registry,
        FluxGuardOptions options)
    {
        switch (options.Preset)
        {
            case GuardPreset.Minimal:
                foreach (var guard in MinimalPreset.GetInputGuards(registry))
                    builder.AddInputGuard(guard);
                foreach (var guard in MinimalPreset.GetOutputGuards(registry))
                    builder.AddOutputGuard(guard);
                break;

            case GuardPreset.Standard:
                foreach (var guard in StandardPreset.GetInputGuards(registry, options.InputGuards))
                    builder.AddInputGuard(guard);
                foreach (var guard in StandardPreset.GetOutputGuards(
                    registry, options.OutputGuards, options.InputGuards.SupportedLanguages))
                    builder.AddOutputGuard(guard);
                break;

            case GuardPreset.Strict:
                foreach (var guard in StrictPreset.GetInputGuards(registry, options.InputGuards))
                    builder.AddInputGuard(guard);
                foreach (var guard in StrictPreset.GetOutputGuards(
                    registry, options.OutputGuards, options.InputGuards.SupportedLanguages))
                    builder.AddOutputGuard(guard);
                break;
        }
    }
}
