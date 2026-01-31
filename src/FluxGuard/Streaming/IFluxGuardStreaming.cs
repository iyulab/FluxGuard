using FluxGuard.Core;

namespace FluxGuard.Streaming;

/// <summary>
/// FluxGuard streaming extension interface
/// </summary>
public interface IFluxGuardStreaming
{
    /// <summary>
    /// Check LLM output stream asynchronously
    /// </summary>
    /// <param name="context">Guard context with original input</param>
    /// <param name="outputStream">Stream of output chunks from LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of validated chunks</returns>
    IAsyncEnumerable<StreamingChunkResult> CheckOutputStreamAsync(
        GuardContext context,
        IAsyncEnumerable<string> outputStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check LLM output stream with simple input
    /// </summary>
    /// <param name="input">Original input text</param>
    /// <param name="outputStream">Stream of output chunks from LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of validated chunks</returns>
    IAsyncEnumerable<StreamingChunkResult> CheckOutputStreamAsync(
        string input,
        IAsyncEnumerable<string> outputStream,
        CancellationToken cancellationToken = default);
}
