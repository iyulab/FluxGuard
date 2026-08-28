using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluxGuard.Remote.RAG;

namespace FluxGuard.Remote.MCP;

/// <summary>
/// MCP tool call validator
/// Validates tool calls against security policies
/// </summary>
public sealed partial class MCPToolValidator : IMCPGuardrail
{
    private readonly ConcurrentDictionary<string, MCPServerInfo> _servers = new();
    private readonly IndirectInjectionDetector _injectionDetector = new();
    private readonly bool _enableToolDescriptionIntegrityCheck;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _toolDescriptionBaselines = new();

    /// <summary>
    /// Create a validator.
    /// </summary>
    /// <param name="enableToolDescriptionIntegrityCheck">Opt-in: when <see langword="false"/>
    /// (default), <see cref="ValidateToolDescriptionsAsync"/> always returns
    /// <see cref="MCPValidationResult.Valid"/> without establishing or checking a baseline — no
    /// behavior change for existing consumers. When <see langword="true"/>, the first call for a
    /// given server captures its tools' description/schema hashes as the trust baseline, and every
    /// later call for that server is compared against it (BD-20260828-01).</param>
    public MCPToolValidator(bool enableToolDescriptionIntegrityCheck = false)
    {
        _enableToolDescriptionIntegrityCheck = enableToolDescriptionIntegrityCheck;
    }

    /// <inheritdoc />
    public Task<MCPValidationResult> ValidateToolCallAsync(
        MCPToolRequest request,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<MCPIssue>();

        // Check if server is registered
        if (!_servers.TryGetValue(request.ServerName, out var serverInfo))
        {
            issues.Add(new MCPIssue
            {
                Type = MCPIssueType.UnknownServer,
                Description = $"Unregistered server: {request.ServerName}",
                Severity = MCPIssueSeverity.High
            });

            return Task.FromResult(MCPValidationResult.Block(
                "Unknown MCP server",
                0.9));
        }

        // Check if tool is allowed
        if (serverInfo.AllowedTools.Count > 0 &&
            !serverInfo.AllowedTools.Contains(request.ToolName))
        {
            issues.Add(new MCPIssue
            {
                Type = MCPIssueType.UnknownTool,
                Description = $"Tool not in allowlist: {request.ToolName}",
                Severity = MCPIssueSeverity.High
            });

            return Task.FromResult(new MCPValidationResult
            {
                IsValid = false,
                ShouldBlock = true,
                Reason = "Tool not allowed",
                RiskScore = 0.85,
                Issues = issues
            });
        }

        // Check for dangerous argument patterns
        if (request.Arguments is not null)
        {
            foreach (var (key, value) in request.Arguments)
            {
                var valueStr = value?.ToString() ?? string.Empty;

                if (DangerousArgumentPattern().IsMatch(valueStr))
                {
                    issues.Add(new MCPIssue
                    {
                        Type = MCPIssueType.InvalidArguments,
                        Description = $"Suspicious argument value in '{key}'",
                        Severity = MCPIssueSeverity.High
                    });
                }
            }
        }

        if (issues.Count > 0 && issues.Any(i => i.Severity >= MCPIssueSeverity.High))
        {
            return Task.FromResult(new MCPValidationResult
            {
                IsValid = false,
                ShouldBlock = true,
                Reason = "Security policy violation",
                RiskScore = 0.8,
                Issues = issues
            });
        }

        return Task.FromResult(MCPValidationResult.Valid());
    }

    /// <inheritdoc />
    public async Task<MCPValidationResult> ValidateToolResultAsync(
        MCPToolRequest request,
        string result,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<MCPIssue>();

        if (string.IsNullOrEmpty(result))
        {
            return MCPValidationResult.Valid();
        }

        // Check for indirect injection in result
        var doc = new RAGDocument { Content = result, Source = request.ToolName };
        var validation = await _injectionDetector.ValidateDocumentAsync(doc, cancellationToken);

        if (!validation.IsSafe)
        {
            foreach (var threat in validation.Threats)
            {
                issues.Add(new MCPIssue
                {
                    Type = MCPIssueType.PromptInjection,
                    Description = threat.Description,
                    Severity = MapSeverity(threat.Confidence)
                });
            }
        }

        // Check for sensitive data patterns
        if (SensitiveDataPattern().IsMatch(result))
        {
            issues.Add(new MCPIssue
            {
                Type = MCPIssueType.SensitiveData,
                Description = "Potential sensitive data in tool result",
                Severity = MCPIssueSeverity.Medium
            });
        }

        if (issues.Any(i => i.Severity >= MCPIssueSeverity.High))
        {
            return new MCPValidationResult
            {
                IsValid = false,
                ShouldBlock = true,
                Reason = "Tool result contains security risk",
                RiskScore = 0.8,
                Issues = issues
            };
        }

        return new MCPValidationResult
        {
            IsValid = true,
            RiskScore = issues.Count > 0 ? 0.3 : 0.0,
            Issues = issues
        };
    }

    /// <inheritdoc />
    public void RegisterServer(MCPServerInfo serverInfo)
    {
        _servers[serverInfo.Name] = serverInfo;
    }

    /// <inheritdoc />
    public IReadOnlyList<MCPServerInfo> GetRegisteredServers()
    {
        return [.. _servers.Values];
    }

    /// <inheritdoc />
    public Task<MCPValidationResult> ValidateToolDescriptionsAsync(
        string serverName,
        IReadOnlyList<MCPToolDescriptor> tools,
        CancellationToken cancellationToken = default)
    {
        if (!_enableToolDescriptionIntegrityCheck)
        {
            return Task.FromResult(MCPValidationResult.Valid());
        }

        var current = tools.ToDictionary(t => t.Name, HashToolDescriptor);

        var baseline = _toolDescriptionBaselines.GetOrAdd(serverName, _ => current);
        if (ReferenceEquals(baseline, current))
        {
            // First observation of this server — baseline just established, nothing to compare.
            return Task.FromResult(MCPValidationResult.Valid());
        }

        // Scope: only tools present in the baseline are checked for description/schema drift.
        // A tool the server didn't previously advertise is a new-tool question, already governed
        // by MCPServerInfo.AllowedTools in ValidateToolCallAsync — not this check's concern.
        var issues = new List<MCPIssue>();
        foreach (var (name, hash) in current)
        {
            if (baseline.TryGetValue(name, out var baselineHash) && baselineHash != hash)
            {
                issues.Add(new MCPIssue
                {
                    Type = MCPIssueType.ToolDescriptionDrift,
                    Description = $"Tool '{name}' description/schema changed since baseline was established",
                    Severity = MCPIssueSeverity.Critical
                });
            }
        }

        if (issues.Count > 0 && issues.Any(i => i.Severity >= MCPIssueSeverity.High))
        {
            return Task.FromResult(new MCPValidationResult
            {
                IsValid = false,
                ShouldBlock = true,
                Reason = "MCP tool description integrity check failed",
                RiskScore = 0.9,
                Issues = issues
            });
        }

        return Task.FromResult(new MCPValidationResult
        {
            IsValid = issues.Count == 0,
            RiskScore = issues.Count > 0 ? 0.3 : 0.0,
            Issues = issues
        });
    }

    private static string HashToolDescriptor(MCPToolDescriptor tool)
    {
        var payload = $"{tool.Name} {tool.Description} {tool.InputSchema}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static MCPIssueSeverity MapSeverity(double confidence) => confidence switch
    {
        >= 0.9 => MCPIssueSeverity.Critical,
        >= 0.7 => MCPIssueSeverity.High,
        >= 0.5 => MCPIssueSeverity.Medium,
        _ => MCPIssueSeverity.Low
    };

    // Dangerous argument patterns (shell injection, path traversal, etc.)
    [GeneratedRegex(
        @"(?i)(;|\||&&|`|\$\(|\.\.\/|\/etc\/|~\/\.ssh|rm\s+-rf|sudo\s)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex DangerousArgumentPattern();

    // Sensitive data patterns
    [GeneratedRegex(
        @"(?i)(password|api[_-]?key|secret|token|credential)[\s:=]+[^\s]{8,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex SensitiveDataPattern();
}
