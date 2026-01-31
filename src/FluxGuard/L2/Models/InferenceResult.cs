namespace FluxGuard.L2.Models;

/// <summary>
/// Result of an ML inference operation
/// </summary>
public record InferenceResult
{
    /// <summary>
    /// The predicted label
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// All class probabilities
    /// </summary>
    public IReadOnlyDictionary<string, double> Probabilities { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Inference latency in milliseconds
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Whether the inference was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if inference failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Model ID that produced this result
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// Creates a failed inference result
    /// </summary>
    public static InferenceResult Failed(string errorMessage, string? modelId = null)
    {
        return new InferenceResult
        {
            Label = "unknown",
            Score = 0,
            Success = false,
            ErrorMessage = errorMessage,
            ModelId = modelId
        };
    }
}
