namespace FluxGuard.L1.Patterns.PII;

/// <summary>
/// Boundaries for numeric PII patterns (phone numbers): a match is a whole number, not a group of a longer
/// hyphen- or colon-joined one. Without them, the seconds of a time of day with seven fractional digits
/// (<c>10:24:03.5968830</c>, the .NET round-trip format) read as a Korean landline.
/// </summary>
internal static class NumericBoundary
{
    /// <summary>
    /// A match does not start inside a run of ASCII letters or digits, or right after a digit and a hyphen or colon.
    /// </summary>
    public const string Start = @"(?<![0-9A-Za-z])(?<![0-9][-:])";

    /// <summary>
    /// A match does not end inside a run of ASCII letters or digits, or right before a hyphen or colon and a digit.
    /// </summary>
    public const string End = @"(?![-:][0-9])(?![0-9A-Za-z])";
}
