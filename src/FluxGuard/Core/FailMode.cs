namespace FluxGuard.Core;

/// <summary>
/// Behavior mode when guard errors occur
/// </summary>
public enum FailMode
{
    /// <summary>
    /// Pass through on guard error (availability first, default)
    /// </summary>
    Open,

    /// <summary>
    /// Block on guard error (security first)
    /// </summary>
    Closed
}
