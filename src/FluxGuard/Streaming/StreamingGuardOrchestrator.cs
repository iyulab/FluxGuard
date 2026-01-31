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
    /// <returns>Stream of validated chunks with validation results</returns>
    public async IAsyncEnumerable<StreamingChunkResult> ValidateStreamAsync(
        GuardContext context,
        IAsyncEnumerable<string> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new ChunkBuffer(_options.MaxBufferSize);

        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            buffer.Append(chunk);

            // Validate chunk with all guards
            var (validation, shouldContinue) = await ValidateChunkWithGuardsAsync(
                context, chunk, buffer.Content, cancellationToken);

            if (!shouldContinue)
            {
                // Stream terminated by guard
                yield return new StreamingChunkResult
                {
                    OriginalChunk = chunk,
                    Validation = validation,
                    IsTerminated = true
                };
                yield break;
            }

            if (validation.ShouldSuppress)
            {
                // Chunk suppressed - yield replacement if any
                if (!string.IsNullOrEmpty(validation.ReplacementText))
                {
                    yield return new StreamingChunkResult
                    {
                        OriginalChunk = chunk,
                        OutputChunk = validation.ReplacementText,
                        Validation = validation,
                        IsSuppressed = true
                    };
                }
                else
                {
                    yield return new StreamingChunkResult
                    {
                        OriginalChunk = chunk,
                        Validation = validation,
                        IsSuppressed = true
                    };
                }
            }
            else
            {
                // Chunk passed - yield as-is
                yield return new StreamingChunkResult
                {
                    OriginalChunk = chunk,
                    OutputChunk = chunk,
                    Validation = validation
                };
            }

            // Periodically validate accumulated buffer for sentence-level checks
            if (_options.EnableSentenceLevelValidation && buffer.MayContainIncompleteSensitiveData())
            {
                var sentences = buffer.ExtractAllSentences();
                foreach (var sentence in sentences)
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

        // Final validation of remaining buffer
        var remaining = buffer.Flush();
        if (!string.IsNullOrEmpty(remaining))
        {
            var finalValidation = await ValidateFinalWithGuardsAsync(
                context, buffer.Content, cancellationToken);

            yield return new StreamingChunkResult
            {
                OriginalChunk = remaining,
                OutputChunk = finalValidation.ShouldSuppress ? finalValidation.ReplacementText : remaining,
                Validation = finalValidation,
                IsFinal = true
            };
        }
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
            catch
            {
                // Guard error - continue with next guard (FailMode.Open behavior)
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
            catch
            {
                // Continue with next guard
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
            catch
            {
                // Continue with next guard
            }
        }

        return TokenValidation.Safe;
    }
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
