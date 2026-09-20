using System.Globalization;

namespace FluxGuard.L2.ML;

/// <summary>
/// Word-piece tokenization against a BERT-style vocabulary file.
/// </summary>
/// <remarks>
/// A tokenizer without its vocabulary is not a degraded tokenizer, it is a different function, so
/// construction fails rather than substituting one. The wrapper used to hash each word into an id
/// when the vocabulary could not be read; the model then scored arbitrary ids and the guard
/// returned that as a verdict. Worse, .NET randomises string hash codes per process, so those ids
/// - and the verdicts built on them - changed from run to run.
/// </remarks>
public sealed class TokenizerWrapper : IDisposable
{
    private readonly int _maxLength;
    private readonly Dictionary<string, int> _vocabulary;
    private bool _disposed;

    /// <summary>
    /// Creates a tokenizer wrapper from a vocabulary file.
    /// </summary>
    /// <param name="vocabPath">Path to a BERT-style vocabulary file, one token per line.</param>
    /// <param name="maxLength">Maximum sequence length, including the special tokens.</param>
    /// <exception cref="ArgumentException"><paramref name="vocabPath"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="vocabPath"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file cannot be read, or holds no usable token.
    /// </exception>
    public TokenizerWrapper(string vocabPath, int maxLength = 512)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vocabPath);

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException(
                $"The L2 tokenizer vocabulary was not found at '{vocabPath}'. L2 guards need their model files; " +
                "place them under the models directory or set L2GuardOptions.ModelsBasePath.",
                vocabPath);
        }

        Dictionary<string, int> vocabulary;
        try
        {
            vocabulary = LoadVocabulary(vocabPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"The L2 tokenizer vocabulary at '{vocabPath}' could not be read.", ex);
        }

        if (vocabulary.Count == 0)
        {
            throw new InvalidOperationException(
                $"The L2 tokenizer vocabulary at '{vocabPath}' holds no tokens. Every word would be unknown, " +
                "so the model's scores would carry no information.");
        }

        _vocabulary = vocabulary;
        _maxLength = maxLength;
    }

    /// <summary>
    /// Creates a tokenizer wrapper with a pre-loaded vocabulary.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="vocabulary"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="vocabulary"/> is empty.</exception>
    public TokenizerWrapper(Dictionary<string, int> vocabulary, int maxLength = 512)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        if (vocabulary.Count == 0)
        {
            throw new ArgumentException(
                "The tokenizer vocabulary is empty; every word would be unknown.", nameof(vocabulary));
        }

        _vocabulary = vocabulary;
        _maxLength = maxLength;
    }

    /// <summary>
    /// Tokenizes text and returns input tensors
    /// </summary>
    public TokenizedInput Tokenize(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(text))
        {
            return CreateEmptyInput();
        }

        return TokenizeWithVocabulary(text);
    }

    /// <summary>
    /// Batch tokenizes multiple texts
    /// </summary>
    public IReadOnlyList<TokenizedInput> TokenizeBatch(IEnumerable<string> texts)
    {
        return texts.Select(Tokenize).ToList();
    }

    private TokenizedInput TokenizeWithVocabulary(string text)
    {
        // Simple word-piece tokenization
        var tokens = new List<int>();

        // Add [CLS] token
        if (_vocabulary.TryGetValue("[CLS]", out var clsId))
        {
            tokens.Add(clsId);
        }

        // Tokenize words
        var words = text.ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':'],
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (tokens.Count >= _maxLength - 1)
            {
                break;
            }

            if (_vocabulary.TryGetValue(word, out var wordId))
            {
                tokens.Add(wordId);
            }
            else if (_vocabulary.TryGetValue("[UNK]", out var unkId))
            {
                tokens.Add(unkId);
            }
            else
            {
                // Fallback hash-based ID
                tokens.Add(Math.Abs(word.GetHashCode(StringComparison.Ordinal)) % 30000 + 1000);
            }
        }

        // Add [SEP] token
        if (_vocabulary.TryGetValue("[SEP]", out var sepId) && tokens.Count < _maxLength)
        {
            tokens.Add(sepId);
        }

        return CreatePaddedInput(tokens);
    }

    private TokenizedInput CreateEmptyInput()
    {
        return new TokenizedInput
        {
            InputIds = new long[_maxLength],
            AttentionMask = new long[_maxLength],
            SequenceLength = 0
        };
    }

    private TokenizedInput CreatePaddedInput(List<int> tokenIds)
    {
        var inputIds = new long[_maxLength];
        var attentionMask = new long[_maxLength];
        var sequenceLength = Math.Min(tokenIds.Count, _maxLength);

        for (var i = 0; i < sequenceLength; i++)
        {
            inputIds[i] = tokenIds[i];
            attentionMask[i] = 1;
        }

        return new TokenizedInput
        {
            InputIds = inputIds,
            AttentionMask = attentionMask,
            SequenceLength = sequenceLength
        };
    }

    private static Dictionary<string, int> LoadVocabulary(string vocabPath)
    {
        var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(vocabPath);

        for (var i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                vocabulary[token] = i;
            }
        }

        return vocabulary;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // No unmanaged resources to dispose
    }
}

/// <summary>
/// Tokenized input for model inference
/// </summary>
public sealed class TokenizedInput
{
    /// <summary>
    /// Token IDs
    /// </summary>
    public required long[] InputIds { get; init; }

    /// <summary>
    /// Attention mask (1 for real tokens, 0 for padding)
    /// </summary>
    public required long[] AttentionMask { get; init; }

    /// <summary>
    /// Actual sequence length before padding
    /// </summary>
    public int SequenceLength { get; init; }
}
