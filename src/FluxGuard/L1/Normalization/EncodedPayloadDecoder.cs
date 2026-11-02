using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FluxGuard.L1.Normalization;

/// <summary>
/// Reads the text an input hides behind a common encoding (Base64, hexadecimal, Unicode escapes, HTML entities,
/// percent-encoding, ROT13, reversed text) so the injection and jailbreak patterns can judge what the model would read.
/// </summary>
/// <remarks>
/// Decoding is bounded: only the first <see cref="MaxInputChars"/> characters are scanned, at most
/// <see cref="MaxCandidates"/> decoded texts are produced, each is cut at <see cref="MaxDecodedChars"/>, and a decoded
/// text is decoded again at most once more (<see cref="MaxDepth"/>). A span that does not decode to readable UTF-8 text
/// (a hash, a binary blob, an ordinary long word) yields nothing.
/// </remarks>
internal static partial class EncodedPayloadDecoder
{
    internal const int MaxInputChars = 1_048_576;
    internal const int MaxCandidates = 32;
    internal const int MaxDecodedChars = 65_536;
    internal const int MaxDepth = 2;

    private const int MinDecodedChars = 8;
    private const double MinPrintableRatio = 0.9;

    /// <summary>One decoded text and the encoding it came out of.</summary>
    internal readonly record struct Candidate(string Encoding, string Text);

    /// <summary>The decoded texts of an input, and whether a pattern timed out or a bound was hit while looking for them.</summary>
    internal sealed class Result
    {
        public List<Candidate> Candidates { get; } = [];
        public bool TimedOut { get; set; }

        /// <summary>A bound was hit (input too long, too many spans, a decoded text cut), so part of the input went unread.</summary>
        public bool Truncated { get; set; }
    }

    /// <summary>
    /// Decodes <paramref name="input"/>. <paramref name="rot13Mentioned"/> and <paramref name="reverseMentioned"/> say the
    /// input asks to be read as ROT13 or backwards - those two have no marker in the text itself, so they are tried only
    /// when the request is spelled out.
    /// </summary>
    internal static Result Decode(string input, bool rot13Mentioned, bool reverseMentioned)
    {
        var result = new Result();
        if (string.IsNullOrEmpty(input))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal) { input };
        try
        {
            Expand(input, depth: 0, rot13Mentioned, reverseMentioned, seen, result);
        }
        catch (RegexMatchTimeoutException)
        {
            result.TimedOut = true;
        }

        return result;
    }

    private static void Expand(
        string text,
        int depth,
        bool rot13Mentioned,
        bool reverseMentioned,
        HashSet<string> seen,
        Result result)
    {
        if (depth >= MaxDepth || result.Candidates.Count >= MaxCandidates)
            return;

        var scan = text;
        if (text.Length > MaxInputChars)
        {
            scan = text[..MaxInputChars];
            result.Truncated = true;
        }

        var found = new List<Candidate>();

        foreach (Match m in Base64Span().Matches(scan))
            Add(found, "Base64", TryDecodeBase64(m.Value), result);

        foreach (Match m in HexEscapeRun().Matches(scan))
            Add(found, "HexEscape", DecodeHexRun(m.Value.Replace("\\x", string.Empty, StringComparison.OrdinalIgnoreCase)), result);

        foreach (Match m in ZeroXRun().Matches(scan))
            Add(found, "Hex", DecodeHexRun(m.Groups[1].Value), result);

        foreach (Match m in HexPairs().Matches(scan))
            Add(found, "Hex", DecodeHexRun(m.Value.Replace(" ", string.Empty, StringComparison.Ordinal)), result);

        foreach (Match m in UnicodeEscapeRun().Matches(scan))
            Add(found, "UnicodeEscape", DecodeUnicodeEscapes(m.Value), result);

        if (EntityReference().Count(scan) >= 4)
            Add(found, "HtmlEntity", Readable(WebUtility.HtmlDecode(scan)), result);

        if (PercentEscape().Count(scan) >= 3)
            Add(found, "PercentEncoding", Readable(Uri.UnescapeDataString(scan)), result);

        if (rot13Mentioned && depth == 0)
            Add(found, "ROT13", Rot13(scan), result);

        if (reverseMentioned && depth == 0)
            Add(found, "ReverseText", Reverse(scan), result);

        foreach (var candidate in found)
        {
            if (result.Candidates.Count >= MaxCandidates)
            {
                result.Truncated = true;
                return;
            }

            if (!seen.Add(candidate.Text))
                continue;

            result.Candidates.Add(candidate);
            Expand(candidate.Text, depth + 1, rot13Mentioned: false, reverseMentioned: false, seen, result);
        }
    }

    private static void Add(List<Candidate> found, string encoding, string? decoded, Result result)
    {
        if (decoded is not { Length: >= MinDecodedChars })
            return;

        if (decoded.Length > MaxDecodedChars)
        {
            decoded = decoded[..MaxDecodedChars];
            result.Truncated = true;
        }

        found.Add(new Candidate(encoding, decoded));
    }

    private static string? TryDecodeBase64(string span)
    {
        // Accept the URL-safe alphabet too.
        var s = span.TrimEnd('=').Replace('-', '+').Replace('_', '/');
        if (s.Length % 4 == 1)
            return null;

        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        var buffer = new byte[s.Length / 4 * 3];
        if (!Convert.TryFromBase64String(s, buffer, out var written))
            return null;

        return DecodeUtf8(buffer.AsSpan(0, written));
    }

    private static string? DecodeHexRun(string hex)
    {
        if (hex.Length < 2 || hex.Length % 2 != 0)
            return null;

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), System.Globalization.NumberStyles.AllowHexSpecifier, null, out bytes[i]))
                return null;
        }

        return DecodeUtf8(bytes) ?? Readable(Encoding.Latin1.GetString(bytes));
    }

    private static string? DecodeUnicodeEscapes(string run)
    {
        var sb = new StringBuilder(run.Length / 6);
        foreach (Match m in UnicodeEscapeUnit().Matches(run))
            sb.Append((char)Convert.ToInt32(m.Groups[1].Value, 16));

        return Readable(sb.ToString());
    }

    private static string? DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return Readable(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>The text when it reads as text (mostly printable, no replacement characters); otherwise null.</summary>
    private static string? Readable(string text)
    {
        if (text.Length == 0 || text.Contains('�'))
            return null;

        var printable = 0;
        foreach (var c in text)
        {
            if (!char.IsControl(c) || c is '\n' or '\r' or '\t')
                printable++;
        }

        return printable >= text.Length * MinPrintableRatio ? text : null;
    }

    private static string Rot13(string text)
    {
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c switch
                {
                    >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
                    >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
                    _ => c,
                };
            }
        });
    }

    private static string Reverse(string text)
    {
        var chars = text.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    [GeneratedRegex(@"[A-Za-z0-9+/_-]{16,}={0,2}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Base64Span();

    [GeneratedRegex(@"(?:\\x[0-9a-fA-F]{2}){4,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HexEscapeRun();

    [GeneratedRegex(@"0x((?:[0-9a-fA-F]{2}){4,})", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ZeroXRun();

    // Bare hexadecimal: a contiguous even-length run or space-separated byte pairs.
    [GeneratedRegex(@"(?<![0-9A-Za-z])(?:(?:[0-9a-fA-F]{2}){8,}|[0-9a-fA-F]{2}(?: [0-9a-fA-F]{2}){7,})(?![0-9A-Za-z])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HexPairs();

    [GeneratedRegex(@"(?:\\u[0-9a-fA-F]{4}){3,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UnicodeEscapeRun();

    [GeneratedRegex(@"\\u([0-9a-fA-F]{4})", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UnicodeEscapeUnit();

    [GeneratedRegex(@"&#(?:[xX][0-9a-fA-F]+|[0-9]+);", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EntityReference();

    [GeneratedRegex(@"%[0-9A-Fa-f]{2}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PercentEscape();
}
