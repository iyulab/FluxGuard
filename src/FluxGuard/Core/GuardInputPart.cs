namespace FluxGuard.Core;

/// <summary>
/// A non-text part of the user turn a guard check covers — what the model will see beside the text, such as an attached
/// image (<see cref="GuardContext.Parts"/>).
/// </summary>
/// <param name="MediaType">The part's media type, e.g. <c>image/png</c>.</param>
/// <param name="Data">The part's bytes.</param>
public sealed record GuardInputPart(string MediaType, ReadOnlyMemory<byte> Data);
