using FluxGuard.Core;

namespace FluxGuard.Abstractions;

/// <summary>
/// Turns a non-text part of a user turn (<see cref="GuardContext.Parts"/> — an image, a scanned page) into the text the input
/// guards judge: OCR, or a call to the consumer's own vision model with a «transcribe the visible text only» prompt.
/// FluxGuard ships none; the consumer registers one (<c>FluxGuardBuilder.WithPartTextExtractor</c>, or an
/// <see cref="IInputPartTextExtractor"/> service for <c>AddFluxGuard</c>).
/// </summary>
/// <remarks>
/// The extracted text is untrusted like any input: it is only ever judged by the guards, never followed. A part no
/// extractor reads is counted in <see cref="GuardResult.UncheckedParts"/> rather than passed silently.
/// </remarks>
public interface IInputPartTextExtractor
{
    /// <summary>Whether this extractor reads parts of <paramref name="mediaType"/> (e.g. <c>image/png</c>).</summary>
    /// <param name="mediaType">The part's media type.</param>
    bool CanExtract(string mediaType);

    /// <summary>The text <paramref name="part"/> shows, or an empty string when it shows none.</summary>
    /// <param name="part">The part to read.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    Task<string> ExtractTextAsync(GuardInputPart part, CancellationToken cancellationToken = default);
}
