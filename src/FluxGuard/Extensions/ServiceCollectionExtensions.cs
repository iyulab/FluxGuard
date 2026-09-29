using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Hooks;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;
using FluxGuard.Monitoring;
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

        // Options alone: the preset named in the options supplies the guards.
        return services.AddFluxGuardCore(configureBuilder: null);
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
        ArgumentNullException.ThrowIfNull(configureBuilder);
        return services.AddFluxGuardCore(configureBuilder);
    }

    /// <summary>
    /// The one registration every <c>AddFluxGuard</c> overload shares. The guard is seeded from the container —
    /// <see cref="FluxGuardOptions"/> (including what <c>AddFluxGuardRemote</c> configures), the pattern registry, hooks,
    /// logging, every registered <see cref="IRemoteGuard"/> and an <see cref="IGuardStatsCollector"/> if one is
    /// registered — and <paramref name="configureBuilder"/>, when given, runs last so it can override any of them.
    /// </summary>
    private static IServiceCollection AddFluxGuardCore(
        this IServiceCollection services,
        Action<FluxGuardBuilder, IServiceProvider>? configureBuilder)
    {
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
                .Configure(options.CopyTo)
                .WithHooks(hooks)
                .WithLogging(loggerFactory)
                .WithPatternRegistry(registry);

            // Register L3 remote guards from DI container
            foreach (var remoteGuard in sp.GetServices<IRemoteGuard>())
            {
                builder.AddRemoteGuard(remoteGuard);
            }

            // A registered statistics collector is fed by the pipeline; none registered, nothing is recorded.
            if (sp.GetService<IGuardStatsCollector>() is { } stats)
            {
                builder.WithStats(stats);
            }

            if (configureBuilder is null)
            {
                // The preset's guards are created in Build(), from these options and this registry - the same
                // path a hand-built FluxGuardBuilder takes.
                builder.RequestPreset(options.Preset);
            }
            else
            {
                // Like a hand-built builder: guards the action adds replace the preset's, and a preset it names wins.
                configureBuilder(builder, sp);
            }

            return builder.Build();
        });

        return services;
    }
}
