using System.Runtime.CompilerServices;
using FluxGuard.Core;

namespace FluxGuard.Streaming;

/// <summary>
/// Orchestrates streaming guards for real-time output validation
/// </summary>
public sealed class StreamingGuardOrchestrator
{
    private readonly IReadOnlyList<IStreamingGuard> _guards;
    private readonly StreamingGuardOptions _options;

    /// <summary>
    /// Create streaming guard orchestrator
    /// </summary>
    /// <param name="guards">Streaming guards to apply</param>
    /// <param name="options">Streaming options</param>
    public StreamingGuardOrchestrator(
        IEnumerable<IStreamingGuard> guards,
        StreamingGuardOptions? options = null)
    {
        _guards = [.. guards.Where(g => g.IsEnabled)];
        _options = options ?? new StreamingGuardOptions();
    }

    /// <summary>
    /// Validate a streaming output asynchronously
    /// </summary>
    /// <param name="context">Guard context</param>
    /// <param name="chunks">Stream of output chunks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// One result per validated chunk - forward its <see cref="StreamingChunkResult.OutputChunk"/> - and, when the
    /// stream ran to its end, a last result with <see cref="StreamingChunkResult.IsFinal"/> set that carries the verdict
    /// on the whole output. The final result never carries text: every chunk was already forwarded by then.
    /// Chunks shorter than <see cref="StreamingGuardOptions.MinChunkSize"/> are held and validated together.
    /// </returns>
    public async IAsyncEnumerable<StreamingChunkResult> ValidateStreamAsync(
        GuardContext context,
        IAsyncEnumerable<string> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new ChunkBuffer(_options.MaxBufferSize);
        var pending = new System.Text.StringBuilder();
        var sawAnyChunk = false;

        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            sawAnyChunk = true;
            buffer.Append(chunk);
            pending.Append(chunk);

            if (pending.Length < _options.MinChunkSize)
            {
                continue;
            }

            var text = pending.ToString();
            pending.Clear();

            var result = await ValidateOneAsync(context, text, buffer.Content, cancellationToken);
            yield return result;
            if (result.IsTerminated)
            {
                yield break;
            }

            // Periodically validate accumulated buffer for sentence-level checks
            if (_options.EnableSentenceLevelValidation && buffer.MayContainIncompleteSensitiveData())
            {
                foreach (var sentence in buffer.ExtractAllSentences())
                {
                    var sentenceValidation = await ValidateSentenceWithGuardsAsync(
                        context, sentence, cancellationToken);

                    if (sentenceValidation.ShouldTerminate)
                    {
                        yield return new StreamingChunkResult
                        {
                            OriginalChunk = sentence,
                            Validation = sentenceValidation,
                            IsTerminated = true
                        };
                        yield break;
                    }
                }
            }
        }

        // Chunks still held below MinChunkSize when the stream ended
        if (pending.Length > 0)
        {
            var result = await ValidateOneAsync(context, pending.ToString(), buffer.Content, cancellationToken);
            yield return result;
            if (result.IsTerminated)
            {
                yield break;
            }
        }

        if (!sawAnyChunk)
        {
            yield break;
        }

        // Verdict on the whole output. It carries no text: everything was forwarded chunk by chunk above.
        buffer.Flush();
        var finalValidation = await ValidateFinalWithGuardsAsync(context, buffer.Content, cancellationToken);

        yield return new StreamingChunkResult
        {
            OriginalChunk = string.Empty,
            Validation = finalValidation,
            IsTerminated = finalValidation.ShouldTerminate,
            IsFinal = true
        };
    }

    private async ValueTask<StreamingChunkResult> ValidateOneAsync(
        GuardContext context,
        string chunk,
        string bufferContent,
        CancellationToken cancellationToken)
    {
        var (validation, shouldContinue) = await ValidateChunkWithGuardsAsync(
            context, chunk, bufferContent, cancellationToken);

        if (!shouldContinue)
        {
            return new StreamingChunkResult { OriginalChunk = chunk, Validation = validation, IsTerminated = true };
        }

        if (validation.ShouldSuppress)
        {
            return new StreamingChunkResult
            {
                OriginalChunk = chunk,
                OutputChunk = string.IsNullOrEmpty(validation.ReplacementText) ? null : validation.ReplacementText,
                Validation = validation,
                IsSuppressed = true
            };
        }

        return new StreamingChunkResult { OriginalChunk = chunk, OutputChunk = chunk, Validation = validation };
    }

    private async ValueTask<(TokenValidation validation, bool shouldContinue)> ValidateChunkWithGuardsAsync(
        GuardContext context,
        string chunk,
        string buffer,
        CancellationToken cancellationToken)
    {
        foreach (var guard in _guards)
        {
            try
            {
                var result = await guard.ValidateChunkAsync(context, chunk, buffer, cancellationToken);

                if (result.ShouldTerminate)
                {
                    return (result, false);
                }

                if (!result.Passed || result.ShouldSuppress)
                {
                    return (result, true);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (_options.FailMode == FailMode.Closed)
            {
                return (GuardErrorVerdict(guard), false);
            }
            catch (Exception)
            {
                // FailMode.Open: the guard is skipped
            }
        }

        return (TokenValidation.Safe, true);
    }

    private async ValueTask<TokenValidation> ValidateSentenceWithGuardsAsync(
        GuardContext context,
        string sentence,
        CancellationToken cancellationToken)
    {
        foreach (var guard in _guards)
        {
            try
            {
                var result = await guard.ValidateFinalAsync(context, sentence, cancellationToken);
                if (!result.Passed)
                {
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (_options.FailMode == FailMode.Closed)
            {
                return GuardErrorVerdict(guard);
            }
            catch (Exception)
            {
                // FailMode.Open: the guard is skipped
            }
        }

        return TokenValidation.Safe;
    }

    private async ValueTask<TokenValidation> ValidateFinalWithGuardsAsync(
        GuardContext context,
        string fullOutput,
        CancellationToken cancellationToken)
    {
        foreach (var guard in _guards)
        {
            try
            {
                var result = await guard.ValidateFinalAsync(context, fullOutput, cancellationToken);
                if (!result.Passed)
                {
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (_options.FailMode == FailMode.Closed)
            {
                return GuardErrorVerdict(guard);
            }
            catch (Exception)
            {
                // FailMode.Open: the guard is skipped
            }
        }

        return TokenValidation.Safe;
    }

    private static TokenValidation GuardErrorVerdict(IStreamingGuard guard) =>
        TokenValidation.Terminate(guard.Name, 1.0, Severity.High, pattern: "GuardError");
}

/// <summary>
/// Streaming guard options
/// </summary>
public sealed class StreamingGuardOptions
{
    /// <summary>
    /// Maximum buffer size before forced validation (default: 4096)
    /// </summary>
    public int MaxBufferSize { get; set; } = 4096;

    /// <summary>
    /// Enable sentence-level validation (default: true)
    /// </summary>
    public bool EnableSentenceLevelValidation { get; set; } = true;

    /// <summary>
    /// Minimum chunk size for validation (default: 1)
    /// </summary>
    public int MinChunkSize { get; set; } = 1;

    /// <summary>
    /// What a guard that throws means (default: <see cref="Core.FailMode.Open"/> - the guard is skipped).
    /// Under <see cref="Core.FailMode.Closed"/> the stream is terminated instead.
    /// </summary>
    public FailMode FailMode { get; set; } = FailMode.Open;
}

/// <summary>
/// Result of streaming chunk validation
/// </summary>
public sealed record StreamingChunkResult
{
    /// <summary>
    /// Original chunk from LLM
    /// </summary>
    public required string OriginalChunk { get; init; }

    /// <summary>
    /// Output chunk (may differ from original if modified)
    /// </summary>
    public string? OutputChunk { get; init; }

    /// <summary>
    /// Validation result
    /// </summary>
    public required TokenValidation Validation { get; init; }

    /// <summary>
    /// Whether stream was terminated
    /// </summary>
    public bool IsTerminated { get; init; }

    /// <summary>
    /// Whether chunk was suppressed
    /// </summary>
    public bool IsSuppressed { get; init; }

    /// <summary>
    /// Whether this is the final chunk
    /// </summary>
    public bool IsFinal { get; init; }
}
