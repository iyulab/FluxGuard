using System.Text;
using System.Text.RegularExpressions;

namespace FluxGuard.Streaming;

/// <summary>
/// Buffer for accumulating streaming chunks with sentence boundary detection
/// </summary>
public sealed partial class ChunkBuffer
{
    private readonly StringBuilder _buffer = new();
    private readonly int _maxBufferSize;
    private int _processedUpTo;

    /// <summary>
    /// Current buffer content
    /// </summary>
    public string Content => _buffer.ToString();

    /// <summary>
    /// Unprocessed content since last flush
    /// </summary>
    public string UnprocessedContent => _buffer.ToString(_processedUpTo, _buffer.Length - _processedUpTo);

    /// <summary>
    /// Total length of buffer
    /// </summary>
    public int Length => _buffer.Length;

    /// <summary>
    /// Create a new chunk buffer
    /// </summary>
    /// <param name="maxBufferSize">Maximum buffer size before forced flush (default: 4096)</param>
    public ChunkBuffer(int maxBufferSize = 4096)
    {
        _maxBufferSize = maxBufferSize;
    }

    /// <summary>
    /// Append a chunk to the buffer
    /// </summary>
    /// <param name="chunk">Chunk to append</param>
    public void Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
            return;

        _buffer.Append(chunk);
    }

    /// <summary>
    /// Try to extract a complete sentence from the buffer
    /// </summary>
    /// <param name="sentence">Extracted sentence</param>
    /// <returns>True if a complete sentence was found</returns>
    public bool TryExtractSentence(out string? sentence)
    {
        sentence = null;

        var content = UnprocessedContent;
        if (string.IsNullOrEmpty(content))
            return false;

        var match = SentenceEndPattern().Match(content);
        if (!match.Success)
        {
            // Force extraction if buffer is too large
            if (_buffer.Length - _processedUpTo > _maxBufferSize)
            {
                sentence = content;
                _processedUpTo = _buffer.Length;
                return true;
            }
            return false;
        }

        // Extract up to and including the sentence boundary
        var endIndex = match.Index + match.Length;
        sentence = content[..endIndex];
        _processedUpTo += endIndex;
        return true;
    }

    /// <summary>
    /// Extract all complete sentences from the buffer
    /// </summary>
    /// <returns>List of complete sentences</returns>
    public IReadOnlyList<string> ExtractAllSentences()
    {
        var sentences = new List<string>();

        while (TryExtractSentence(out var sentence))
        {
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    /// <summary>
    /// Flush remaining content
    /// </summary>
    /// <returns>Remaining unprocessed content</returns>
    public string Flush()
    {
        var remaining = UnprocessedContent;
        _processedUpTo = _buffer.Length;
        return remaining;
    }

    /// <summary>
    /// Clear the buffer
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
        _processedUpTo = 0;
    }

    /// <summary>
    /// Check if the buffer contains potentially incomplete sensitive data
    /// </summary>
    /// <returns>True if there might be incomplete PII</returns>
    public bool MayContainIncompleteSensitiveData()
    {
        var content = UnprocessedContent;
        if (string.IsNullOrEmpty(content))
            return false;

        // Check for partial patterns that could become PII
        return PartialSensitivePattern().IsMatch(content);
    }

    // Sentence boundary pattern: . ! ? followed by space or end
    [GeneratedRegex(@"[.!?]+(?:\s|$)", RegexOptions.Compiled)]
    private static partial Regex SentenceEndPattern();

    // Partial sensitive data patterns (could become complete PII)
    [GeneratedRegex(@"(?:\d{3,}[-\s]?|\d{2,}[-/]|\w+@|\b[A-Z][a-z]+\s[A-Z])$",
        RegexOptions.Compiled)]
    private static partial Regex PartialSensitivePattern();
}
