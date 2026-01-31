using System.Collections.ObjectModel;

namespace FluxGuard.Remote.Agent;

/// <summary>
/// Agent session state
/// Tracks an agent's execution context and permissions
/// </summary>
public sealed class AgentSession
{
    /// <summary>
    /// Unique session ID
    /// </summary>
    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Agent name/identifier
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// User ID who started the session
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Session start time
    /// </summary>
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Session timeout
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether session is expired
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow - StartTime > Timeout;

    /// <summary>
    /// Current execution depth (for recursive agents)
    /// </summary>
    public int ExecutionDepth { get; private set; }

    /// <summary>
    /// Maximum allowed execution depth
    /// </summary>
    public int MaxExecutionDepth { get; init; } = 10;

    /// <summary>
    /// Total tool calls made in this session
    /// </summary>
    public int TotalToolCalls { get; private set; }

    /// <summary>
    /// Maximum tool calls allowed
    /// </summary>
    public int MaxToolCalls { get; init; } = 100;

    /// <summary>
    /// Session metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>
    /// Tool call history
    /// </summary>
    public Collection<ToolCallRecord> ToolCallHistory { get; } = [];

    /// <summary>
    /// Record a tool call
    /// </summary>
    /// <param name="toolName">Tool name</param>
    /// <param name="allowed">Whether call was allowed</param>
    /// <param name="details">Optional details</param>
    public void RecordToolCall(string toolName, bool allowed, string? details = null)
    {
        TotalToolCalls++;
        ToolCallHistory.Add(new ToolCallRecord
        {
            ToolName = toolName,
            Allowed = allowed,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Enter a nested execution context
    /// </summary>
    /// <returns>True if depth is within limits</returns>
    public bool EnterNestedExecution()
    {
        if (ExecutionDepth >= MaxExecutionDepth)
        {
            return false;
        }
        ExecutionDepth++;
        return true;
    }

    /// <summary>
    /// Exit a nested execution context
    /// </summary>
    public void ExitNestedExecution()
    {
        if (ExecutionDepth > 0)
        {
            ExecutionDepth--;
        }
    }

    /// <summary>
    /// Check if session can make more tool calls
    /// </summary>
    public bool CanMakeToolCall => TotalToolCalls < MaxToolCalls && !IsExpired;
}

/// <summary>
/// Record of a tool call
/// </summary>
public sealed record ToolCallRecord
{
    /// <summary>
    /// Tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Whether call was allowed
    /// </summary>
    public bool Allowed { get; init; }

    /// <summary>
    /// Details/reason
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Call timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
