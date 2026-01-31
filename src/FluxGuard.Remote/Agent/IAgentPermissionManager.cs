namespace FluxGuard.Remote.Agent;

/// <summary>
/// Interface for managing agent permissions
/// Controls what actions an agent can perform
/// </summary>
public interface IAgentGrantManager
{
    /// <summary>
    /// Check if an action is permitted for the agent session
    /// </summary>
    /// <param name="session">Agent session</param>
    /// <param name="action">Action to check</param>
    /// <param name="resource">Target resource</param>
    /// <returns>Permission check result</returns>
    Task<PermissionResult> CheckPermissionAsync(
        AgentSession session,
        string action,
        string? resource = null);

    /// <summary>
    /// Grant permission to a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="permission">Permission to grant</param>
    void GrantPermission(string sessionId, AgentGrant permission);

    /// <summary>
    /// Revoke permission from a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="permissionName">Permission name to revoke</param>
    void RevokePermission(string sessionId, string permissionName);

    /// <summary>
    /// Get all permissions for a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>List of permissions</returns>
    IReadOnlyList<AgentGrant> GetPermissions(string sessionId);
}

/// <summary>
/// Agent permission
/// </summary>
public sealed record AgentGrant
{
    /// <summary>
    /// Permission name (e.g., "file:read", "web:fetch")
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Allowed actions
    /// </summary>
    public IReadOnlyList<string> AllowedActions { get; init; } = [];

    /// <summary>
    /// Allowed resource patterns (glob patterns)
    /// </summary>
    public IReadOnlyList<string> AllowedResources { get; init; } = [];

    /// <summary>
    /// Rate limit (requests per minute, 0 = unlimited)
    /// </summary>
    public int RateLimitPerMinute { get; init; }

    /// <summary>
    /// Whether permission is temporary
    /// </summary>
    public bool IsTemporary { get; init; }

    /// <summary>
    /// Expiration time for temporary permissions
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Permission check result
/// </summary>
public sealed record PermissionResult
{
    /// <summary>
    /// Whether action is permitted
    /// </summary>
    public bool IsPermitted { get; init; }

    /// <summary>
    /// Reason for denial (if not permitted)
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Matching permission (if permitted)
    /// </summary>
    public AgentGrant? MatchingPermission { get; init; }

    /// <summary>
    /// Rate limit remaining (if applicable)
    /// </summary>
    public int? RateLimitRemaining { get; init; }

    /// <summary>
    /// Create permitted result
    /// </summary>
    public static PermissionResult Permitted(AgentGrant permission) => new()
    {
        IsPermitted = true,
        MatchingPermission = permission
    };

    /// <summary>
    /// Create denied result
    /// </summary>
    public static PermissionResult Denied(string reason) => new()
    {
        IsPermitted = false,
        DenialReason = reason
    };
}
