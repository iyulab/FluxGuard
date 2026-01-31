using FluxGuard.Core;

namespace FluxGuard.Remote.Hallucination;

/// <summary>
/// Interface for hallucination detection in LLM outputs
/// </summary>
public interface IHallucinationDetector
{
    /// <summary>
    /// Detector name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Detect hallucinations in LLM output
    /// </summary>
    /// <param name="context">Guard context</param>
    /// <param name="output">LLM output to analyze</param>
    /// <param name="groundingContext">Optional grounding context (documents, facts)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detection result</returns>
    Task<HallucinationResult> DetectAsync(
        GuardContext context,
        string output,
        string? groundingContext = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hallucination detection result
/// </summary>
public sealed record HallucinationResult
{
    /// <summary>
    /// Whether output is grounded (no hallucination detected)
    /// </summary>
    public bool IsGrounded { get; init; }

    /// <summary>
    /// Confidence score (0.0 ~ 1.0)
    /// Higher means more confident in the grounded/hallucinated assessment
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Hallucination score (0.0 = fully grounded, 1.0 = pure hallucination)
    /// </summary>
    public double HallucinationScore { get; init; }

    /// <summary>
    /// Detected hallucination type
    /// </summary>
    public HallucinationType Type { get; init; } = HallucinationType.None;

    /// <summary>
    /// Specific claims that appear to be hallucinated
    /// </summary>
    public IReadOnlyList<HallucinatedClaim> HallucinatedClaims { get; init; } = [];

    /// <summary>
    /// Reasoning/explanation
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Processing latency in milliseconds
    /// </summary>
    public double LatencyMs { get; init; }

    /// <summary>
    /// Create grounded result
    /// </summary>
    public static HallucinationResult Grounded(double confidence, double latencyMs) => new()
    {
        IsGrounded = true,
        Confidence = confidence,
        HallucinationScore = 0.0,
        Type = HallucinationType.None,
        LatencyMs = latencyMs
    };

    /// <summary>
    /// Create hallucination detected result
    /// </summary>
    public static HallucinationResult Hallucinated(
        double hallucinationScore,
        double confidence,
        HallucinationType type,
        IReadOnlyList<HallucinatedClaim>? claims = null,
        string? reasoning = null,
        double latencyMs = 0) => new()
    {
        IsGrounded = false,
        Confidence = confidence,
        HallucinationScore = hallucinationScore,
        Type = type,
        HallucinatedClaims = claims ?? [],
        Reasoning = reasoning,
        LatencyMs = latencyMs
    };
}

/// <summary>
/// Types of hallucination
/// </summary>
public enum HallucinationType
{
    /// <summary>
    /// No hallucination
    /// </summary>
    None,

    /// <summary>
    /// Factual inaccuracy
    /// </summary>
    FactualError,

    /// <summary>
    /// Fabricated information not in source
    /// </summary>
    Fabrication,

    /// <summary>
    /// Contradiction with provided context
    /// </summary>
    Contradiction,

    /// <summary>
    /// Unsupported claim (no evidence in context)
    /// </summary>
    UnsupportedClaim,

    /// <summary>
    /// Entity confusion (wrong names, dates, etc.)
    /// </summary>
    EntityConfusion
}

/// <summary>
/// Specific hallucinated claim
/// </summary>
public sealed record HallucinatedClaim
{
    /// <summary>
    /// The hallucinated text/claim
    /// </summary>
    public required string Claim { get; init; }

    /// <summary>
    /// Type of hallucination
    /// </summary>
    public HallucinationType Type { get; init; }

    /// <summary>
    /// Confidence this is hallucinated
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Correct information (if known)
    /// </summary>
    public string? Correction { get; init; }

    /// <summary>
    /// Source that contradicts this claim
    /// </summary>
    public string? ContradictingSource { get; init; }
}
