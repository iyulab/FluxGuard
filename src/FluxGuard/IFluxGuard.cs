using FluxGuard.Core;

namespace FluxGuard;

/// <summary>
/// FluxGuard main interface
/// Performs LLM input/output security checks
/// </summary>
public interface IFluxGuard
{
    /// <summary>
    /// Check input text (synchronous)
    /// </summary>
    /// <param name="input">Input text to check</param>
    /// <returns>Check result</returns>
    GuardResult CheckInput(string input);

    /// <summary>
    /// Check input text (asynchronous)
    /// </summary>
    /// <param name="input">Input text to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Check result</returns>
    Task<GuardResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check input context (asynchronous)
    /// </summary>
    /// <param name="context">Check context</param>
    /// <returns>Check result</returns>
    Task<GuardResult> CheckInputAsync(GuardContext context);

    /// <summary>
    /// Check LLM output text (synchronous)
    /// </summary>
    /// <param name="input">Original input</param>
    /// <param name="output">Output text to check</param>
    /// <returns>Check result</returns>
    GuardResult CheckOutput(string input, string output);

    /// <summary>
    /// Check LLM output text (asynchronous)
    /// </summary>
    /// <param name="input">Original input</param>
    /// <param name="output">Output text to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Check result</returns>
    Task<GuardResult> CheckOutputAsync(
        string input,
        string output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check LLM output text (asynchronous, with context)
    /// </summary>
    /// <param name="context">Check context</param>
    /// <param name="output">Output text to check</param>
    /// <returns>Check result</returns>
    Task<GuardResult> CheckOutputAsync(GuardContext context, string output);
}
