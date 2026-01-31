using FluxGuard.SDK.AI.ChatClient;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FluxGuard.SDK.AI.Extensions;

/// <summary>
/// Chat client builder extensions for FluxGuard
/// </summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>
    /// Add FluxGuard protection to the chat client pipeline
    /// </summary>
    /// <param name="builder">Chat client builder</param>
    /// <param name="options">FluxGuard options</param>
    /// <returns>Chat client builder</returns>
    public static ChatClientBuilder UseFluxGuard(
        this ChatClientBuilder builder,
        FluxGuardChatClientOptions? options = null)
    {
        return builder.Use((innerClient, services) =>
        {
            var guard = services.GetRequiredService<IFluxGuard>();
            var logger = services.GetRequiredService<ILogger<FluxGuardChatClient>>();
            return new FluxGuardChatClient(innerClient, guard, logger, options);
        });
    }

    /// <summary>
    /// Add FluxGuard protection with custom guard
    /// </summary>
    /// <param name="builder">Chat client builder</param>
    /// <param name="guard">FluxGuard instance</param>
    /// <param name="logger">Logger</param>
    /// <param name="options">Options</param>
    /// <returns>Chat client builder</returns>
    public static ChatClientBuilder UseFluxGuard(
        this ChatClientBuilder builder,
        IFluxGuard guard,
        ILogger<FluxGuardChatClient> logger,
        FluxGuardChatClientOptions? options = null)
    {
        return builder.Use(innerClient =>
            new FluxGuardChatClient(innerClient, guard, logger, options));
    }
}
