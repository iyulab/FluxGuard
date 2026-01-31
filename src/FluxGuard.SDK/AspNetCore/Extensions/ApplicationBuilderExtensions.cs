using FluxGuard.SDK.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FluxGuard.SDK.AspNetCore.Extensions;

/// <summary>
/// ASP.NET Core application builder extensions
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Add FluxGuard middleware to the pipeline
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseFluxGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<FluxGuardMiddleware>();
    }

    /// <summary>
    /// Add FluxGuard middleware with configuration
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseFluxGuard(
        this IApplicationBuilder app,
        Action<FluxGuardMiddlewareOptions> configure)
    {
        var options = new FluxGuardMiddlewareOptions();
        configure(options);

        return app.UseMiddleware<FluxGuardMiddleware>(
            Microsoft.Extensions.Options.Options.Create(options));
    }
}

/// <summary>
/// Service collection extensions for ASP.NET Core
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add FluxGuard services for ASP.NET Core
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddFluxGuardMiddleware(this IServiceCollection services)
    {
        services.AddOptions<FluxGuardMiddlewareOptions>();
        return services;
    }

    /// <summary>
    /// Add FluxGuard services with configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddFluxGuardMiddleware(
        this IServiceCollection services,
        Action<FluxGuardMiddlewareOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}
