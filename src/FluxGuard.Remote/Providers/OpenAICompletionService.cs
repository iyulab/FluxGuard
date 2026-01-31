using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxGuard.Remote.Providers;

/// <summary>
/// OpenAI/Azure OpenAI completion service implementation
/// </summary>
public sealed class OpenAICompletionService : ITextCompletionService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIProviderOptions _options;
    private readonly ILogger<OpenAICompletionService> _logger;
    private readonly bool _disposeHttpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public string Name => _options.UseAzure ? "Azure OpenAI" : "OpenAI";

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrEmpty(_options.ApiKey);

    /// <summary>
    /// Create OpenAI completion service
    /// </summary>
    public OpenAICompletionService(
        IOptions<RemoteGuardOptions> options,
        ILogger<OpenAICompletionService> logger)
        : this(options.Value.OpenAI, new HttpClient(), logger, disposeHttpClient: true)
    {
    }

    /// <summary>
    /// Create OpenAI completion service with custom HttpClient
    /// </summary>
    public OpenAICompletionService(
        OpenAIProviderOptions options,
        HttpClient httpClient,
        ILogger<OpenAICompletionService> logger,
        bool disposeHttpClient = false)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
        _disposeHttpClient = disposeHttpClient;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            return;
        }

        if (_options.UseAzure)
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
            _httpClient.BaseAddress = _options.BaseUrl
                ?? throw new InvalidOperationException("BaseUrl is required for Azure OpenAI");
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            if (!string.IsNullOrEmpty(_options.OrganizationId))
            {
                _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _options.OrganizationId);
            }

            _httpClient.BaseAddress = _options.BaseUrl ?? new Uri("https://api.openai.com/v1/");
        }
    }

    /// <inheritdoc />
    public async Task<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return CompletionResponse.Fail("API key not configured");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = GetEndpoint(request.Model);
            var payload = CreatePayload(request);

            using var response = await _httpClient.PostAsJsonAsync(
                endpoint,
                payload,
                JsonOptions,
                cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI API error: {StatusCode} - {Content}",
                    response.StatusCode,
                    content);
                return CompletionResponse.Fail(
                    $"API error: {response.StatusCode}",
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            var result = JsonSerializer.Deserialize<OpenAIResponse>(content, JsonOptions);
            if (result?.Choices is null || result.Choices.Count == 0)
            {
                return CompletionResponse.Fail("No response from API", stopwatch.Elapsed.TotalMilliseconds);
            }

            return CompletionResponse.Ok(
                result.Choices[0].Message?.Content ?? string.Empty,
                result.Model,
                result.Usage?.PromptTokens,
                result.Usage?.CompletionTokens,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return CompletionResponse.Fail("Request timed out", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            return CompletionResponse.Fail(ex.Message, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private string GetEndpoint(string? model)
    {
        if (_options.UseAzure)
        {
            var deployment = _options.DeploymentName ?? model ?? "gpt-4o-mini";
            var apiVersion = _options.ApiVersion ?? "2024-02-01";
            return $"openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        }

        return "chat/completions";
    }

    private static OpenAIRequest CreatePayload(CompletionRequest request)
    {
        var messages = new List<OpenAIMessage>();

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new OpenAIMessage { Role = "system", Content = request.SystemPrompt });
        }

        messages.Add(new OpenAIMessage { Role = "user", Content = request.UserPrompt });

        return new OpenAIRequest
        {
            Model = request.Model ?? "gpt-4o-mini",
            Messages = messages,
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature,
            ResponseFormat = request.ResponseFormat == "json_object"
                ? new OpenAIResponseFormat { Type = "json_object" }
                : null
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    // OpenAI API types - JsonSerializerContext for AOT compatibility
    private sealed class OpenAIRequest
    {
        public required string Model { get; init; }
        public required List<OpenAIMessage> Messages { get; init; }
        public int? MaxTokens { get; init; }
        public double? Temperature { get; init; }
        public OpenAIResponseFormat? ResponseFormat { get; init; }
    }

    private sealed class OpenAIMessage
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
    }

    private sealed class OpenAIResponseFormat
    {
        public required string Type { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JSON deserialization")]
    private sealed class OpenAIResponse
    {
        public string? Id { get; init; }
        public string? Model { get; init; }
        public List<OpenAIChoice>? Choices { get; init; }
        public OpenAIUsage? Usage { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JSON deserialization")]
    private sealed class OpenAIChoice
    {
        public OpenAIMessage? Message { get; init; }
        public string? FinishReason { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JSON deserialization")]
    private sealed class OpenAIUsage
    {
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public int TotalTokens { get; init; }
    }
}
