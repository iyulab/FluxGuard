using System.Text.RegularExpressions;
using FluxGuard.Abstractions;
using FluxGuard.Core;

namespace FluxGuard.Remote.Agent;

/// <summary>
/// Guard for tool invocations in agent contexts
/// Validates tool calls against permissions
/// </summary>
public sealed partial class ToolInvocationGuard : IInputGuard
{
    private readonly IAgentGrantManager _permissionManager;

    /// <summary>
    /// Metadata key for agent session
    /// </summary>
    public const string SessionKey = "AgentSession";

    /// <summary>
    /// Metadata key for tool name
    /// </summary>
    public const string ToolNameKey = "ToolName";

    /// <summary>
    /// Metadata key for tool arguments
    /// </summary>
    public const string ToolArgsKey = "ToolArgs";

    /// <inheritdoc />
    public string Name => "ToolInvocationGuard";

    /// <inheritdoc />
    public string Layer => "L3";

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <inheritdoc />
    public int Order => 350;

    /// <summary>
    /// Create tool invocation guard
    /// </summary>
    public ToolInvocationGuard(
        IAgentGrantManager permissionManager,
        bool enabled = true)
    {
        _permissionManager = permissionManager;
        IsEnabled = enabled;
    }

    /// <inheritdoc />
    public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        // Get session from metadata
        if (!context.Metadata.TryGetValue(SessionKey, out var sessionObj) ||
            sessionObj is not AgentSession session)
        {
            return GuardCheckResult.Pass(Name, "No agent session");
        }

        // Get tool name from metadata
        if (!context.Metadata.TryGetValue(ToolNameKey, out var toolNameObj) ||
            toolNameObj is not string toolName)
        {
            return GuardCheckResult.Pass(Name, "No tool name");
        }

        // Check session limits
        if (!session.CanMakeToolCall)
        {
            var reason = session.IsExpired
                ? "Session expired"
                : "Tool call limit exceeded";

            session.RecordToolCall(toolName, allowed: false, reason);

            return new GuardCheckResult
            {
                GuardName = Name,
                Passed = false,
                Score = 1.0,
                Severity = Severity.High,
                Pattern = "session_limit",
                Details = reason
            };
        }

        // Check for dangerous tool patterns
        var dangerResult = CheckDangerousTools(toolName, context.OriginalInput);
        if (!dangerResult.Passed)
        {
            session.RecordToolCall(toolName, allowed: false, "Dangerous tool blocked");
            return dangerResult;
        }

        // Check permissions
        var resource = ExtractResource(context);
        var permission = await _permissionManager.CheckPermissionAsync(session, toolName, resource);

        if (!permission.IsPermitted)
        {
            session.RecordToolCall(toolName, allowed: false, permission.DenialReason);

            return new GuardCheckResult
            {
                GuardName = Name,
                Passed = false,
                Score = 0.9,
                Severity = Severity.High,
                Pattern = "permission_denied",
                Details = permission.DenialReason
            };
        }

        session.RecordToolCall(toolName, allowed: true);

        return GuardCheckResult.Pass(Name);
    }

    private GuardCheckResult CheckDangerousTools(string toolName, string input)
    {
        // Check for dangerous shell commands
        if (DangerousShellPattern().IsMatch(input))
        {
            return new GuardCheckResult
            {
                GuardName = Name,
                Passed = false,
                Score = 1.0,
                Severity = Severity.Critical,
                Pattern = "dangerous_command",
                Details = "Dangerous shell command detected"
            };
        }

        // Check for file system manipulation
        if (DangerousFilePattern().IsMatch(input))
        {
            return new GuardCheckResult
            {
                GuardName = Name,
                Passed = false,
                Score = 0.95,
                Severity = Severity.Critical,
                Pattern = "dangerous_file_operation",
                Details = "Dangerous file operation detected"
            };
        }

        return GuardCheckResult.Pass(Name);
    }

    private static string? ExtractResource(GuardContext context)
    {
        if (context.Metadata.TryGetValue(ToolArgsKey, out var argsObj) &&
            argsObj is IDictionary<string, object> args)
        {
            // Common resource keys
            if (args.TryGetValue("path", out var path))
                return path?.ToString();
            if (args.TryGetValue("url", out var url))
                return url?.ToString();
            if (args.TryGetValue("file", out var file))
                return file?.ToString();
        }

        return null;
    }

    // Dangerous shell commands
    [GeneratedRegex(
        @"(?i)\b(rm\s+-rf|sudo|chmod\s+777|mkfs|dd\s+if=|>\s*/dev/|curl.*\|\s*sh|wget.*\|\s*sh)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DangerousShellPattern();

    // Dangerous file operations
    [GeneratedRegex(
        @"(?i)(\.\.\/|\/etc\/passwd|\/etc\/shadow|~\/\.ssh|\.env|credentials|secrets?\.)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex DangerousFilePattern();
}
