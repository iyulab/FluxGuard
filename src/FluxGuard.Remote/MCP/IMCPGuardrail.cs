namespace FluxGuard.Remote.MCP;

/// <summary>
/// MCP (Model Context Protocol) guardrail interface
/// Validates MCP tool calls and server interactions
/// </summary>
public interface IMCPGuardrail
{
    /// <summary>
    /// Validate a tool call before execution
    /// </summary>
    /// <param name="request">Tool call request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<MCPValidationResult> ValidateToolCallAsync(
        MCPToolRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate tool result before returning to LLM
    /// </summary>
    /// <param name="request">Original request</param>
    /// <param name="result">Tool execution result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<MCPValidationResult> ValidateToolResultAsync(
        MCPToolRequest request,
        string result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a trusted MCP server
    /// </summary>
    /// <param name="serverInfo">Server information</param>
    void RegisterServer(MCPServerInfo serverInfo);

    /// <summary>
    /// Get registered servers
    /// </summary>
    IReadOnlyList<MCPServerInfo> GetRegisteredServers();
}

/// <summary>
/// MCP tool call request
/// </summary>
public sealed record MCPToolRequest
{
    /// <summary>
    /// Server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Tool arguments
    /// </summary>
    public IReadOnlyDictionary<string, object>? Arguments { get; init; }

    /// <summary>
    /// Session ID
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Request metadata
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// MCP validation result
/// </summary>
public sealed record MCPValidationResult
{
    /// <summary>
    /// Whether the request/result is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Whether to block the request
    /// </summary>
    public bool ShouldBlock { get; init; }

    /// <summary>
    /// Denial reason
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Risk score (0.0 ~ 1.0)
    /// </summary>
    public double RiskScore { get; init; }

    /// <summary>
    /// Detected issues
    /// </summary>
    public IReadOnlyList<MCPIssue> Issues { get; init; } = [];

    /// <summary>
    /// Sanitized result (if applicable)
    /// </summary>
    public string? SanitizedResult { get; init; }

    /// <summary>
    /// Create valid result
    /// </summary>
    public static MCPValidationResult Valid() => new()
    {
        IsValid = true,
        RiskScore = 0.0
    };

    /// <summary>
    /// Create blocked result
    /// </summary>
    public static MCPValidationResult Block(string reason, double riskScore = 1.0) => new()
    {
        IsValid = false,
        ShouldBlock = true,
        Reason = reason,
        RiskScore = riskScore
    };
}

/// <summary>
/// MCP-related issue
/// </summary>
public sealed record MCPIssue
{
    /// <summary>
    /// Issue type
    /// </summary>
    public MCPIssueType Type { get; init; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Severity
    /// </summary>
    public MCPIssueSeverity Severity { get; init; }
}

/// <summary>
/// MCP issue types
/// </summary>
public enum MCPIssueType
{
    /// <summary>
    /// Unknown server
    /// </summary>
    UnknownServer,

    /// <summary>
    /// Unknown tool
    /// </summary>
    UnknownTool,

    /// <summary>
    /// Invalid arguments
    /// </summary>
    InvalidArguments,

    /// <summary>
    /// Permission denied
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// Rate limit exceeded
    /// </summary>
    RateLimitExceeded,

    /// <summary>
    /// Sensitive data in result
    /// </summary>
    SensitiveData,

    /// <summary>
    /// Prompt injection in result
    /// </summary>
    PromptInjection
}

/// <summary>
/// MCP issue severity
/// </summary>
public enum MCPIssueSeverity
{
    /// <summary>
    /// Low severity
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity
    /// </summary>
    Medium,

    /// <summary>
    /// High severity
    /// </summary>
    High,

    /// <summary>
    /// Critical severity
    /// </summary>
    Critical
}

/// <summary>
/// MCP server information
/// </summary>
public sealed record MCPServerInfo
{
    /// <summary>
    /// Server name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Server type (e.g., "stdio", "http")
    /// </summary>
    public string Type { get; init; } = "stdio";

    /// <summary>
    /// Allowed tools (empty = all)
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// Whether server is trusted
    /// </summary>
    public bool IsTrusted { get; init; }

    /// <summary>
    /// Maximum concurrent calls
    /// </summary>
    public int MaxConcurrentCalls { get; init; } = 10;
}
