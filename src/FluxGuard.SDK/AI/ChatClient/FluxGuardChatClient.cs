using System.Runtime.CompilerServices;
using FluxGuard.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FluxGuard.SDK.AI.ChatClient;

/// <summary>
/// FluxGuard-protected chat client wrapper
/// Wraps an IChatClient with input/output validation
/// </summary>
public sealed partial class FluxGuardChatClient : DelegatingChatClient
{
    private readonly IFluxGuard _guard;
    private readonly ILogger<FluxGuardChatClient> _logger;
    private readonly FluxGuardChatClientOptions _options;

    /// <summary>
    /// Create FluxGuard chat client
    /// </summary>
    public FluxGuardChatClient(
        IChatClient innerClient,
        IFluxGuard guard,
        ILogger<FluxGuardChatClient> logger,
        FluxGuardChatClientOptions? options = null)
        : base(innerClient)
    {
        _guard = guard;
        _logger = logger;
        _options = options ?? new FluxGuardChatClientOptions();
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Check input
        var inputText = ExtractInputText(messages);
        if (_options.ValidateInput && !string.IsNullOrEmpty(inputText))
        {
            var inputResult = await _guard.CheckInputAsync(inputText, cancellationToken);

            if (inputResult.IsBlocked)
            {
                LogChatRequestBlocked(_logger, inputResult.BlockReason);

                throw new FluxGuardChatBlockedException(
                    "Request blocked by security guard",
                    inputResult);
            }
        }

        // Get response from inner client
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // Check output
        if (_options.ValidateOutput)
        {
            var outputText = ExtractOutputText(response);
            if (!string.IsNullOrEmpty(outputText))
            {
                var outputResult = await _guard.CheckOutputAsync(
                    inputText ?? string.Empty,
                    outputText,
                    cancellationToken);

                if (outputResult.IsBlocked)
                {
                    LogChatResponseBlocked(_logger, outputResult.BlockReason);

                    throw new FluxGuardChatBlockedException(
                        "Response blocked by security guard",
                        outputResult);
                }
            }
        }

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check input
        var inputText = ExtractInputText(messages);
        if (_options.ValidateInput && !string.IsNullOrEmpty(inputText))
        {
            var inputResult = await _guard.CheckInputAsync(inputText, cancellationToken);

            if (inputResult.IsBlocked)
            {
                LogStreamingRequestBlocked(_logger, inputResult.BlockReason);

                throw new FluxGuardChatBlockedException(
                    "Request blocked by security guard",
                    inputResult);
            }
        }

        // Stream response
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private static string? ExtractInputText(IEnumerable<ChatMessage> messages)
    {
        // Get the last user message
        var lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        return lastUserMessage?.Text;
    }

    private static string? ExtractOutputText(ChatResponse response)
    {
        return response.Messages.LastOrDefault()?.Text;
    }

    [LoggerMessage(LogLevel.Warning, "Chat request blocked: {Reason}")]
    private static partial void LogChatRequestBlocked(ILogger logger, string? reason);

    [LoggerMessage(LogLevel.Warning, "Chat response blocked: {Reason}")]
    private static partial void LogChatResponseBlocked(ILogger logger, string? reason);

    [LoggerMessage(LogLevel.Warning, "Streaming request blocked: {Reason}")]
    private static partial void LogStreamingRequestBlocked(ILogger logger, string? reason);
}

/// <summary>
/// FluxGuard chat client options
/// </summary>
public sealed class FluxGuardChatClientOptions
{
    /// <summary>
    /// Whether to validate inputs (default: true)
    /// </summary>
    public bool ValidateInput { get; set; } = true;

    /// <summary>
    /// Whether to validate outputs (default: true)
    /// </summary>
    public bool ValidateOutput { get; set; } = true;

    /// <summary>
    /// Whether to validate streaming outputs (default: false for performance)
    /// </summary>
    public bool ValidateStreamingOutput { get; set; }
}

/// <summary>
/// Exception thrown when FluxGuard blocks a chat request/response
/// </summary>
public sealed class FluxGuardChatBlockedException : Exception
{
    /// <summary>
    /// Guard result
    /// </summary>
    public GuardResult Result { get; }

    /// <summary>
    /// Create blocked exception
    /// </summary>
    public FluxGuardChatBlockedException()
        : base("Request blocked by security guard")
    {
        Result = GuardResult.Block("unknown", "Blocked", 1.0, Severity.High, [], 0);
    }

    /// <summary>
    /// Create blocked exception with message
    /// </summary>
    public FluxGuardChatBlockedException(string message)
        : base(message)
    {
        Result = GuardResult.Block("unknown", message, 1.0, Severity.High, [], 0);
    }

    /// <summary>
    /// Create blocked exception with message and inner exception
    /// </summary>
    public FluxGuardChatBlockedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Result = GuardResult.Block("unknown", message, 1.0, Severity.High, [], 0);
    }

    /// <summary>
    /// Create blocked exception with result
    /// </summary>
    public FluxGuardChatBlockedException(string message, GuardResult result)
        : base(message)
    {
        Result = result;
    }
}
