using System.Collections.Concurrent;
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
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DangerousArgumentPattern();

    // Sensitive data patterns
    [GeneratedRegex(
        @"(?i)(password|api[_-]?key|secret|token|credential)[\s:=]+[^\s]{8,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex SensitiveDataPattern();
}
