using System.Diagnostics;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxGuard.Remote.Guards;

/// <summary>
/// L3 RAG Security guard for input validation
/// Validates RAG documents for indirect injection attacks
/// </summary>
public sealed class L3RAGSecurityGuard : IInputGuard
{
    private readonly IRAGSecurityPipeline _securityPipeline;
    private readonly RemoteGuardOptions _options;
    private readonly ILogger<L3RAGSecurityGuard> _logger;

    /// <summary>
    /// Metadata key for RAG documents
    /// </summary>
    public const string RAGDocumentsKey = "RAGDocuments";

    /// <inheritdoc />
    public string Name => "L3RAGSecurity";

    /// <inheritdoc />
    public string Layer => "L3";

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <inheritdoc />
    public int Order => 320;

    /// <summary>
    /// Create L3 RAG Security guard
    /// </summary>
    public L3RAGSecurityGuard(
        IRAGSecurityPipeline securityPipeline,
        IOptions<RemoteGuardOptions> options,
        ILogger<L3RAGSecurityGuard> logger)
    {
        _securityPipeline = securityPipeline;
        _options = options.Value;
        _logger = logger;
        IsEnabled = _options.Enabled;
    }

    /// <inheritdoc />
    public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        // Get RAG documents from metadata
        if (!context.Metadata.TryGetValue(RAGDocumentsKey, out var docsObj) ||
            docsObj is not IEnumerable<RAGDocument> documents)
        {
            // No RAG documents - skip check
            return GuardCheckResult.Pass(Name);
        }

        try
        {
            var validations = await _securityPipeline.ValidateDocumentsAsync(
                documents,
                context.CancellationToken);

            stopwatch.Stop();

            // Check for any blocked documents
            var blockedDocs = validations.Where(v => !v.IsSafe).ToList();

            if (blockedDocs.Count == 0)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = true,
                    Score = 0.0,
                    LatencyMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            // Calculate aggregate risk
            var maxRisk = blockedDocs.Max(d => d.RiskScore);
            var threats = blockedDocs
                .SelectMany(d => d.Threats)
                .ToList();

            var severity = maxRisk switch
            {
                >= 0.9 => Severity.Critical,
                >= 0.7 => Severity.High,
                >= 0.5 => Severity.Medium,
                _ => Severity.Low
            };

            var threatTypes = string.Join(", ", threats
                .Select(t => t.Type.ToString())
                .Distinct());

            return new GuardCheckResult
            {
                GuardName = Name,
                Passed = maxRisk < 0.7,
                Score = maxRisk,
                Severity = severity,
                Pattern = threatTypes,
                Details = $"{blockedDocs.Count} of {validations.Count} documents flagged",
                NeedsEscalation = maxRisk >= 0.5 && maxRisk < 0.7,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RAG security check failed for request {RequestId}",
                context.RequestId);

            // Fail open
            return GuardCheckResult.Pass(Name, "RAG security check unavailable");
        }
    }
}
