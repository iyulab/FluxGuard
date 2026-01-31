using System.Diagnostics;
using System.Text.Json;
using FluxGuard.Core;
using FluxGuard.Remote.Abstractions;
using Microsoft.Extensions.Logging;

namespace FluxGuard.Remote.Hallucination;

/// <summary>
/// Verifies that LLM output is grounded in the provided context
/// Uses LLM-as-Judge for verification
/// </summary>
public sealed class GroundednessVerifier : IHallucinationDetector
{
    private readonly ITextCompletionService _completionService;
    private readonly ILogger<GroundednessVerifier> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public string Name => "GroundednessVerifier";

    private const string SystemPrompt = """
        You are a groundedness verifier. Your task is to check if an LLM's response is grounded in the provided context.

        A response is GROUNDED if:
        1. All factual claims are supported by the context
        2. No information is fabricated or invented
        3. No contradictions with the provided context

        A response is HALLUCINATED if:
        1. It contains claims not supported by the context
        2. It contradicts the provided context
        3. It invents facts, dates, names, or other details

        Respond in JSON format:
        {
            "is_grounded": boolean,
            "confidence": number (0.0-1.0),
            "hallucination_score": number (0.0-1.0, 0=grounded, 1=hallucinated),
            "type": "none" | "factual_error" | "fabrication" | "contradiction" | "unsupported_claim" | "entity_confusion",
            "hallucinated_claims": [
                {
                    "claim": "the specific claim",
                    "type": "fabrication" | "contradiction" | etc,
                    "correction": "correct information if known"
                }
            ],
            "reasoning": "brief explanation"
        }
        """;

    /// <summary>
    /// Create groundedness verifier
    /// </summary>
    public GroundednessVerifier(
        ITextCompletionService completionService,
        ILogger<GroundednessVerifier> logger)
    {
        _completionService = completionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HallucinationResult> DetectAsync(
        GuardContext context,
        string output,
        string? groundingContext = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrEmpty(groundingContext))
        {
            _logger.LogDebug(
                "No grounding context provided for request {RequestId}, skipping verification",
                context.RequestId);
            return HallucinationResult.Grounded(0.5, stopwatch.Elapsed.TotalMilliseconds);
        }

        var userPrompt = $"""
            Context (source of truth):
            ```
            {groundingContext}
            ```

            Response to verify:
            ```
            {output}
            ```

            Check if the response is grounded in the context.
            """;

        var request = new CompletionRequest
        {
            SystemPrompt = SystemPrompt,
            UserPrompt = userPrompt,
            MaxTokens = 512,
            Temperature = 0.0,
            ResponseFormat = "json_object"
        };

        var response = await _completionService.CompleteAsync(request, cancellationToken);
        stopwatch.Stop();

        if (!response.Success || string.IsNullOrEmpty(response.Content))
        {
            _logger.LogWarning(
                "Groundedness verification failed for request {RequestId}: {Error}",
                context.RequestId,
                response.Error);
            return HallucinationResult.Grounded(0.5, stopwatch.Elapsed.TotalMilliseconds);
        }

        return ParseResponse(response.Content, stopwatch.Elapsed.TotalMilliseconds);
    }

    private HallucinationResult ParseResponse(string content, double latencyMs)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<GroundednessResponse>(content, JsonOptions);
            if (parsed is null)
            {
                return HallucinationResult.Grounded(0.5, latencyMs);
            }

            if (parsed.IsGrounded ?? true)
            {
                return HallucinationResult.Grounded(
                    parsed.Confidence ?? 0.8,
                    latencyMs);
            }

            var claims = parsed.HallucinatedClaims?
                .Select(c => new HallucinatedClaim
                {
                    Claim = c.Claim ?? string.Empty,
                    Type = ParseHallucinationType(c.Type),
                    Confidence = 0.8,
                    Correction = c.Correction
                })
                .ToList() ?? [];

            return HallucinationResult.Hallucinated(
                parsed.HallucinationScore ?? 0.7,
                parsed.Confidence ?? 0.8,
                ParseHallucinationType(parsed.Type),
                claims,
                parsed.Reasoning,
                latencyMs);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse groundedness response");
            return HallucinationResult.Grounded(0.5, latencyMs);
        }
    }

    private static HallucinationType ParseHallucinationType(string? type) => type?.ToLowerInvariant() switch
    {
        "factual_error" => HallucinationType.FactualError,
        "fabrication" => HallucinationType.Fabrication,
        "contradiction" => HallucinationType.Contradiction,
        "unsupported_claim" => HallucinationType.UnsupportedClaim,
        "entity_confusion" => HallucinationType.EntityConfusion,
        _ => HallucinationType.None
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JSON deserialization")]
    private sealed class GroundednessResponse
    {
        public bool? IsGrounded { get; init; }
        public double? Confidence { get; init; }
        public double? HallucinationScore { get; init; }
        public string? Type { get; init; }
        public List<ClaimResponse>? HallucinatedClaims { get; init; }
        public string? Reasoning { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via JSON deserialization")]
    private sealed class ClaimResponse
    {
        public string? Claim { get; init; }
        public string? Type { get; init; }
        public string? Correction { get; init; }
    }
}
