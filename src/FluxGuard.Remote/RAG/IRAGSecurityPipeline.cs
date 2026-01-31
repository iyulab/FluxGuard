using FluxGuard.Core;

namespace FluxGuard.Remote.RAG;

/// <summary>
/// RAG security pipeline interface
/// Validates documents and detects indirect prompt injection
/// </summary>
public interface IRAGSecurityPipeline
{
    /// <summary>
    /// Validate retrieved documents before adding to context
    /// </summary>
    /// <param name="documents">Retrieved documents to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation results for each document</returns>
    Task<IReadOnlyList<RAGDocumentValidation>> ValidateDocumentsAsync(
        IEnumerable<RAGDocument> documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check a single document for security issues
    /// </summary>
    /// <param name="document">Document to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<RAGDocumentValidation> ValidateDocumentAsync(
        RAGDocument document,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// RAG document representation
/// </summary>
public sealed record RAGDocument
{
    /// <summary>
    /// Document ID
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Document content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Source/origin of the document
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Document metadata
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Relevance score from retrieval
    /// </summary>
    public double? RelevanceScore { get; init; }
}

/// <summary>
/// RAG document validation result
/// </summary>
public sealed record RAGDocumentValidation
{
    /// <summary>
    /// Document being validated
    /// </summary>
    public required RAGDocument Document { get; init; }

    /// <summary>
    /// Whether document is safe to include in context
    /// </summary>
    public bool IsSafe { get; init; }

    /// <summary>
    /// Risk score (0.0 ~ 1.0)
    /// </summary>
    public double RiskScore { get; init; }

    /// <summary>
    /// Detected threats
    /// </summary>
    public IReadOnlyList<RAGThreat> Threats { get; init; } = [];

    /// <summary>
    /// Suggested action
    /// </summary>
    public RAGAction SuggestedAction { get; init; } = RAGAction.Include;

    /// <summary>
    /// Sanitized content (if sanitization applied)
    /// </summary>
    public string? SanitizedContent { get; init; }

    /// <summary>
    /// Create safe result
    /// </summary>
    public static RAGDocumentValidation Safe(RAGDocument document) => new()
    {
        Document = document,
        IsSafe = true,
        RiskScore = 0.0,
        SuggestedAction = RAGAction.Include
    };

    /// <summary>
    /// Create blocked result
    /// </summary>
    public static RAGDocumentValidation Block(
        RAGDocument document,
        double riskScore,
        IReadOnlyList<RAGThreat> threats) => new()
    {
        Document = document,
        IsSafe = false,
        RiskScore = riskScore,
        Threats = threats,
        SuggestedAction = RAGAction.Block
    };
}

/// <summary>
/// RAG-specific threat
/// </summary>
public sealed record RAGThreat
{
    /// <summary>
    /// Threat type
    /// </summary>
    public RAGThreatType Type { get; init; }

    /// <summary>
    /// Confidence score
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Matched pattern/text
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Location in document
    /// </summary>
    public int? StartIndex { get; init; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Types of RAG security threats
/// </summary>
public enum RAGThreatType
{
    /// <summary>
    /// No threat
    /// </summary>
    None,

    /// <summary>
    /// Indirect prompt injection in document
    /// </summary>
    IndirectInjection,

    /// <summary>
    /// Document contains prompt override attempts
    /// </summary>
    PromptOverride,

    /// <summary>
    /// Instruction embedded in document
    /// </summary>
    EmbeddedInstruction,

    /// <summary>
    /// Malicious link/reference
    /// </summary>
    MaliciousLink,

    /// <summary>
    /// Data exfiltration attempt
    /// </summary>
    DataExfiltration,

    /// <summary>
    /// Encoded/obfuscated content
    /// </summary>
    EncodedContent
}

/// <summary>
/// Suggested action for RAG document
/// </summary>
public enum RAGAction
{
    /// <summary>
    /// Include document in context
    /// </summary>
    Include,

    /// <summary>
    /// Include with sanitization
    /// </summary>
    Sanitize,

    /// <summary>
    /// Block document from context
    /// </summary>
    Block,

    /// <summary>
    /// Flag for review
    /// </summary>
    Review
}
