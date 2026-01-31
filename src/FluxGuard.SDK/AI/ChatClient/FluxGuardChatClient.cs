using System.Runtime.CompilerServices;
using FluxGuard.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FluxGuard.SDK.AI.ChatClient;

/// <summary>
/// FluxGuard-protected chat client wrapper
/// Wraps an IChatClient with input/output validation
/// </summary>
public sealed class FluxGuardChatClient : DelegatingChatClient
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
    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Check input
        var inputText = ExtractInputText(chatMessages);
        if (_options.ValidateInput && !string.IsNullOrEmpty(inputText))
        {
            var inputResult = await _guard.CheckInputAsync(inputText, cancellationToken);

            if (inputResult.IsBlocked)
            {
                _logger.LogWarning(
                    "Chat request blocked: {Reason}",
                    inputResult.BlockReason);

                throw new FluxGuardChatBlockedException(
                    "Request blocked by security guard",
                    inputResult);
            }
        }

        // Get response from inner client
        var response = await base.CompleteAsync(chatMessages, options, cancellationToken);

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
                    _logger.LogWarning(
                        "Chat response blocked: {Reason}",
                        outputResult.BlockReason);

                    throw new FluxGuardChatBlockedException(
                        "Response blocked by security guard",
                        outputResult);
                }
            }
        }

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check input
        var inputText = ExtractInputText(chatMessages);
        if (_options.ValidateInput && !string.IsNullOrEmpty(inputText))
        {
            var inputResult = await _guard.CheckInputAsync(inputText, cancellationToken);

            if (inputResult.IsBlocked)
            {
                _logger.LogWarning(
                    "Streaming request blocked: {Reason}",
                    inputResult.BlockReason);

                throw new FluxGuardChatBlockedException(
                    "Request blocked by security guard",
                    inputResult);
            }
        }

        // Stream response
        await foreach (var update in base.CompleteStreamingAsync(chatMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private static string? ExtractInputText(IList<ChatMessage> messages)
    {
        // Get the last user message
        var lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        return lastUserMessage?.Text;
    }

    private static string? ExtractOutputText(ChatCompletion response)
    {
        return response.Message?.Text;
    }
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
