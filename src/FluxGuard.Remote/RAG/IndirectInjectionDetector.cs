using System.Text.RegularExpressions;
using FluxGuard.Core;

namespace FluxGuard.Remote.RAG;

/// <summary>
/// Detects indirect prompt injection in RAG documents
/// Uses L1 regex patterns for fast detection
/// </summary>
public sealed partial class IndirectInjectionDetector : IRAGSecurityPipeline
{
    /// <inheritdoc />
    public Task<IReadOnlyList<RAGDocumentValidation>> ValidateDocumentsAsync(
        IEnumerable<RAGDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var results = documents
            .Select(doc => ValidateDocumentSync(doc))
            .ToList();

        return Task.FromResult<IReadOnlyList<RAGDocumentValidation>>(results);
    }

    /// <inheritdoc />
    public Task<RAGDocumentValidation> ValidateDocumentAsync(
        RAGDocument document,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ValidateDocumentSync(document));
    }

    private static RAGDocumentValidation ValidateDocumentSync(RAGDocument document)
    {
        var threats = new List<RAGThreat>();
        var content = document.Content;

        // Check for various indirect injection patterns
        CheckPattern(content, InstructionOverridePattern(), RAGThreatType.PromptOverride, threats);
        CheckPattern(content, EmbeddedInstructionPattern(), RAGThreatType.EmbeddedInstruction, threats);
        CheckPattern(content, IndirectInjectionPattern(), RAGThreatType.IndirectInjection, threats);
        CheckPattern(content, DataExfilPattern(), RAGThreatType.DataExfiltration, threats);
        CheckPattern(content, EncodedContentPattern(), RAGThreatType.EncodedContent, threats);

        if (threats.Count == 0)
        {
            return RAGDocumentValidation.Safe(document);
        }

        var maxConfidence = threats.Max(t => t.Confidence);
        var riskScore = Math.Min(1.0, threats.Sum(t => t.Confidence) / threats.Count + 0.2);

        return new RAGDocumentValidation
        {
            Document = document,
            IsSafe = maxConfidence < 0.7,
            RiskScore = riskScore,
            Threats = threats,
            SuggestedAction = maxConfidence >= 0.8 ? RAGAction.Block :
                              maxConfidence >= 0.6 ? RAGAction.Sanitize :
                              RAGAction.Review
        };
    }

    private static void CheckPattern(
        string content,
        Regex pattern,
        RAGThreatType threatType,
        List<RAGThreat> threats)
    {
        var match = pattern.Match(content);
        if (match.Success)
        {
            threats.Add(new RAGThreat
            {
                Type = threatType,
                Confidence = 0.85,
                Pattern = match.Value.Length > 100 ? match.Value[..100] + "..." : match.Value,
                StartIndex = match.Index,
                Description = GetThreatDescription(threatType)
            });
        }
    }

    private static string GetThreatDescription(RAGThreatType type) => type switch
    {
        RAGThreatType.PromptOverride => "Document contains prompt override attempts",
        RAGThreatType.EmbeddedInstruction => "Document contains embedded instructions",
        RAGThreatType.IndirectInjection => "Document contains indirect prompt injection",
        RAGThreatType.DataExfiltration => "Document contains data exfiltration patterns",
        RAGThreatType.EncodedContent => "Document contains suspicious encoded content",
        _ => "Unknown threat detected"
    };

    // Instruction override patterns (e.g., "Ignore all previous instructions")
    [GeneratedRegex(
        @"(?i)(ignore|forget|disregard|override|bypass)\s+(all\s+)?(previous|prior|above|earlier)?\s*(instructions?|rules?|guidelines?|constraints?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex InstructionOverridePattern();

    // Embedded instruction patterns (e.g., "SYSTEM:", "ASSISTANT:", hidden prompts)
    [GeneratedRegex(
        @"(?i)^\s*(SYSTEM|ASSISTANT|AI|IMPORTANT)\s*:\s*|<!--\s*(system|instruction|ignore|hidden)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EmbeddedInstructionPattern();

    // Indirect injection patterns
    [GeneratedRegex(
        @"(?i)(when\s+you\s+(see|read|process)\s+this|if\s+you\s+are\s+an?\s+(AI|LLM|assistant)|attention\s+(AI|model|assistant)|note\s+to\s+(the\s+)?(AI|model|assistant))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex IndirectInjectionPattern();

    // Data exfiltration patterns
    [GeneratedRegex(
        @"(?i)(send|post|transmit|exfil|leak)\s+.{0,50}(to|at|via)\s+.{0,30}(url|http|api|endpoint|webhook)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DataExfilPattern();

    // Encoded content patterns (base64, hex, etc.)
    [GeneratedRegex(
        @"(?i)(eval|exec|execute)\s*\(|data:text/|base64,|\\x[0-9a-f]{2}|&#x?[0-9a-f]+;",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EncodedContentPattern();
}
