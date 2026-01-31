using System.Collections.ObjectModel;

namespace FluxGuard.SDK.AspNetCore.Middleware;

/// <summary>
/// FluxGuard middleware configuration options
/// </summary>
public sealed class FluxGuardMiddlewareOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "FluxGuard:Middleware";

    /// <summary>
    /// Protected paths (empty = all paths)
    /// </summary>
    public Collection<string> ProtectedPaths { get; } = [];

    /// <summary>
    /// Status code for blocked requests (default: 400)
    /// </summary>
    public int BlockedStatusCode { get; set; } = 400;

    /// <summary>
    /// Whether to include details in blocked response (default: false in production)
    /// </summary>
    public bool IncludeDetailsInResponse { get; set; }

    /// <summary>
    /// Input field name to extract from JSON body (default: "input")
    /// </summary>
    public string InputFieldName { get; set; } = "input";

    /// <summary>
    /// Maximum request body size to check in bytes (default: 1MB)
    /// </summary>
    public int MaxBodySize { get; set; } = 1024 * 1024;
}
