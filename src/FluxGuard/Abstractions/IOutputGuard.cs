using FluxGuard.Core;

namespace FluxGuard.Abstractions;

/// <summary>
/// Output guard interface
/// </summary>
public interface IOutputGuard
{
    /// <summary>
    /// Guard name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Guard layer (L1, L2, L3)
    /// </summary>
    string Layer { get; }

    /// <summary>
    /// Whether guard is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Execution order (lower executes first)
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Perform output check
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="output">LLM output text</param>
    /// <returns>Check result</returns>
    ValueTask<GuardCheckResult> CheckAsync(GuardContext context, string output);
}
