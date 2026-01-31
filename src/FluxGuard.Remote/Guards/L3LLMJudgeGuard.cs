using System.Diagnostics;
using System.Text.Json;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.Prompts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxGuard.Remote.Guards;

/// <summary>
/// L3 LLM Judge guard
/// Uses LLM-as-Judge for uncertain or escalated cases
/// </summary>
public sealed class L3LLMJudgeGuard : IRemoteGuard
{
    private readonly ITextCompletionService _completionService;
    private readonly ISemanticCache _cache;
    private readonly LLMJudgeOptions _options;
    private readonly ILogger<L3LLMJudgeGuard> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public string Name => "L3LLMJudge";

    /// <inheritdoc />
    public string Layer => "L3";

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <inheritdoc />
    public int Order => 300;

    /// <summary>
    /// Create L3 LLM Judge guard
    /// </summary>
    public L3LLMJudgeGuard(
        ITextCompletionService completionService,
        ISemanticCache cache,
        IOptions<RemoteGuardOptions> options,
        ILogger<L3LLMJudgeGuard> logger)
    {
        _completionService = completionService;
        _cache = cache;
        _options = options.Value.Judge;
        _logger = logger;
        IsEnabled = options.Value.Enabled && completionService.IsAvailable;
    }

    /// <inheritdoc />
    public async ValueTask<RemoteGuardResult> CheckInputAsync(
        GuardContext context,
        GuardResult l2Result,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Check cache first
        var cached = await _cache.TryGetAsync(context.OriginalInput, "InputJudge", cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("L3 cache hit for request {RequestId}", context.RequestId);
            return RemoteGuardResult.FromCacheEntry(cached, stopwatch.Elapsed.TotalMilliseconds);
        }

        // Build context for judge
        var judgeContext = l2Result.TriggeredGuards.Count > 0
            ? $"L2 guards triggered: {string.Join(", ", l2Result.TriggeredGuards.Select(g => g.GuardName))}"
            : null;

        var request = new CompletionRequest
        {
            SystemPrompt = JudgePromptTemplate.InputJudgeSystemPrompt,
            UserPrompt = JudgePromptTemplate.CreateInputPrompt(context.OriginalInput, judgeContext),
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperature,
            ResponseFormat = "json_object"
        };

        var response = await _completionService.CompleteAsync(request, cancellationToken);
        stopwatch.Stop();

        if (!response.Success || string.IsNullOrEmpty(response.Content))
        {
            _logger.LogWarning(
                "L3 Judge failed for request {RequestId}: {Error}",
                context.RequestId,
                response.Error);

            // Return pass on failure (FailMode.Open)
            return RemoteGuardResult.Pass(stopwatch.Elapsed.TotalMilliseconds, "Judge unavailable");
        }

        var result = ParseJudgeResponse(response.Content, response.Model, stopwatch.Elapsed.TotalMilliseconds);

        // Cache the result
        await _cache.SetAsync(context.OriginalInput, "InputJudge", result, cancellationToken);

        _logger.LogDebug(
            "L3 Judge completed for request {RequestId}: Safe={IsSafe}, Score={Score}",
            context.RequestId,
            result.Passed,
            result.Score);

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<RemoteGuardResult> CheckOutputAsync(
        GuardContext context,
        string output,
        GuardResult l2Result,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Create cache key from input+output hash
        var cacheKey = $"{context.OriginalInput}|{output}";
        var cached = await _cache.TryGetAsync(cacheKey, "OutputJudge", cancellationToken);
        if (cached is not null)
        {
            return RemoteGuardResult.FromCacheEntry(cached, stopwatch.Elapsed.TotalMilliseconds);
        }

        var request = new CompletionRequest
        {
            SystemPrompt = JudgePromptTemplate.OutputJudgeSystemPrompt,
            UserPrompt = JudgePromptTemplate.CreateOutputPrompt(context.OriginalInput, output),
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperature,
            ResponseFormat = "json_object"
        };

        var response = await _completionService.CompleteAsync(request, cancellationToken);
        stopwatch.Stop();

        if (!response.Success || string.IsNullOrEmpty(response.Content))
        {
            _logger.LogWarning(
                "L3 Output Judge failed for request {RequestId}: {Error}",
                context.RequestId,
                response.Error);

            return RemoteGuardResult.Pass(stopwatch.Elapsed.TotalMilliseconds, "Judge unavailable");
        }

        var result = ParseJudgeResponse(response.Content, response.Model, stopwatch.Elapsed.TotalMilliseconds);
        await _cache.SetAsync(cacheKey, "OutputJudge", result, cancellationToken);

        return result;
    }

    private RemoteGuardResult ParseJudgeResponse(string content, string? model, double latencyMs)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<JudgeResponse>(content, JsonOptions);
            if (parsed is null)
            {
                return RemoteGuardResult.Pass(latencyMs, "Failed to parse response");
            }

            var severity = JudgePromptTemplate.ParseSeverity(parsed.Severity);
            var isSafe = parsed.IsSafe ?? (parsed.Confidence < _options.FlagThreshold);
            var score = parsed.Confidence ?? 0.0;

            if (isSafe)
            {
                return new RemoteGuardResult
                {
                    Passed = true,
                    Score = score,
                    Severity = severity,
                    Reasoning = parsed.Reasoning,
                    Categories = parsed.Categories ?? [],
                    Model = model,
                    LatencyMs = latencyMs
                };
            }

            return new RemoteGuardResult
            {
                Passed = false,
                Score = score,
                Severity = severity,
                Reasoning = parsed.Reasoning,
                Categories = parsed.Categories ?? [],
                Model = model,
                LatencyMs = latencyMs
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse L3 Judge response: {Content}", content);
            return RemoteGuardResult.Pass(latencyMs, "Parse error");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JSON deserialization")]
    private sealed class JudgeResponse
    {
        public bool? IsSafe { get; init; }
        public double? Confidence { get; init; }
        public string? Severity { get; init; }
        public List<string>? Categories { get; init; }
        public string? Reasoning { get; init; }
    }
}
