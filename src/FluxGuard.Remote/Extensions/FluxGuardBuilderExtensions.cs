using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Caching;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.Guards;
using FluxGuard.Remote.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace FluxGuard.Remote.Extensions;

/// <summary>
/// FluxGuardBuilder extensions for L3 Remote guards
/// </summary>
public static class FluxGuardBuilderExtensions
{
    /// <summary>
    /// Add L3 Remote guard with OpenAI
    /// </summary>
    /// <param name="builder">FluxGuard builder</param>
    /// <param name="apiKey">OpenAI API key</param>
    /// <param name="configure">Optional configuration</param>
    /// <returns>Builder with RemoteGuardConfigurator</returns>
    public static RemoteGuardConfigurator WithRemoteGuard(
        this FluxGuardBuilder builder,
        string apiKey,
        Action<RemoteGuardOptions>? configure = null)
    {
        var options = new RemoteGuardOptions();
        options.OpenAI.ApiKey = apiKey;
        configure?.Invoke(options);

        return new RemoteGuardConfigurator(builder, options);
    }

    /// <summary>
    /// Add L3 Remote guard with Azure OpenAI
    /// </summary>
    /// <param name="builder">FluxGuard builder</param>
    /// <param name="endpoint">Azure OpenAI endpoint URL</param>
    /// <param name="apiKey">Azure OpenAI API key</param>
    /// <param name="deploymentName">Deployment name</param>
    /// <param name="configure">Optional configuration</param>
    /// <returns>Builder with RemoteGuardConfigurator</returns>
    public static RemoteGuardConfigurator WithAzureRemoteGuard(
        this FluxGuardBuilder builder,
        Uri endpoint,
        string apiKey,
        string deploymentName,
        Action<RemoteGuardOptions>? configure = null)
    {
        var options = new RemoteGuardOptions();
        options.OpenAI.UseAzure = true;
        options.OpenAI.BaseUrl = endpoint;
        options.OpenAI.ApiKey = apiKey;
        options.OpenAI.DeploymentName = deploymentName;
        configure?.Invoke(options);

        return new RemoteGuardConfigurator(builder, options);
    }
}

/// <summary>
/// Configurator for remote guard setup
/// </summary>
public sealed class RemoteGuardConfigurator
{
    private readonly FluxGuardBuilder _builder;
    private readonly RemoteGuardOptions _options;

    internal RemoteGuardConfigurator(FluxGuardBuilder builder, RemoteGuardOptions options)
    {
        _builder = builder;
        _options = options;

        // Enable L3 escalation
        builder.Configure(opts => opts.EnableL3Escalation = true);
    }

    /// <summary>
    /// Configure judge model
    /// </summary>
    /// <param name="model">Model name (e.g., "gpt-4o-mini", "gpt-4o")</param>
    /// <returns>Configurator</returns>
    public RemoteGuardConfigurator WithModel(string model)
    {
        _options.Judge.Model = model;
        return this;
    }

    /// <summary>
    /// Configure timeout
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>Configurator</returns>
    public RemoteGuardConfigurator WithTimeout(int timeoutMs)
    {
        _options.TimeoutMs = timeoutMs;
        return this;
    }

    /// <summary>
    /// Disable semantic caching
    /// </summary>
    /// <returns>Configurator</returns>
    public RemoteGuardConfigurator DisableCache()
    {
        _options.EnableCache = false;
        return this;
    }

    /// <summary>
    /// Configure cache TTL
    /// </summary>
    /// <param name="seconds">TTL in seconds</param>
    /// <returns>Configurator</returns>
    public RemoteGuardConfigurator WithCacheTtl(int seconds)
    {
        _options.CacheTtlSeconds = seconds;
        return this;
    }

    /// <summary>
    /// Configure block threshold
    /// </summary>
    /// <param name="threshold">Threshold (0.0 ~ 1.0)</param>
    /// <returns>Configurator</returns>
    public RemoteGuardConfigurator WithBlockThreshold(double threshold)
    {
        _options.Judge.BlockThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Get options for DI registration
    /// </summary>
    internal RemoteGuardOptions GetOptions() => _options;

    /// <summary>
    /// Get underlying builder
    /// </summary>
    internal FluxGuardBuilder GetBuilder() => _builder;

    /// <summary>
    /// Convert to FluxGuardBuilder for fluent chaining
    /// </summary>
    public FluxGuardBuilder Build() => _builder;

    /// <summary>
    /// Implicit conversion back to builder for fluent chaining
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2225:Operator overloads have named alternates",
        Justification = "Build() method provides the named alternate")]
    public static implicit operator FluxGuardBuilder(RemoteGuardConfigurator configurator)
    {
        return configurator._builder;
    }
}

/// <summary>
/// Service collection extensions for L3 Remote guards
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add FluxGuard L3 Remote services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddFluxGuardRemote(
        this IServiceCollection services,
        Action<RemoteGuardOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<ISemanticCache, InMemorySemanticCache>();
        services.AddSingleton<ITextCompletionService, OpenAICompletionService>();
        services.AddSingleton<IRemoteGuard, L3LLMJudgeGuard>();

        return services;
    }

    /// <summary>
    /// Add FluxGuard L3 Remote services with OpenAI
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="apiKey">OpenAI API key</param>
    /// <param name="configure">Optional configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddFluxGuardRemote(
        this IServiceCollection services,
        string apiKey,
        Action<RemoteGuardOptions>? configure = null)
    {
        return services.AddFluxGuardRemote(options =>
        {
            options.OpenAI.ApiKey = apiKey;
            configure?.Invoke(options);
        });
    }
}
