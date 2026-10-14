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
    /// Layer label reported in results, logs and statistics. Built-in guards report <c>"L1"</c> (in-process
    /// pattern and string work) or <c>"L3"</c> (a call to a model provider); a custom guard may report any label.
    /// There is no L2 since 0.19.0, when the local ML guards were removed.
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
