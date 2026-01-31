using System.Globalization;

namespace FluxGuard.L2.ML;

/// <summary>
/// Wrapper for tokenizer operations with fallback support
/// Uses simple word-piece tokenization as fallback when BERT tokenizer is not available
/// </summary>
public sealed class TokenizerWrapper : IDisposable
{
    private readonly int _maxLength;
    private readonly Dictionary<string, int>? _vocabulary;
    private bool _disposed;

    /// <summary>
    /// Gets whether the tokenizer vocabulary is loaded
    /// </summary>
    public bool IsVocabularyLoaded => _vocabulary is not null;

    /// <summary>
    /// Creates a tokenizer wrapper from a vocabulary file
    /// </summary>
    public TokenizerWrapper(string vocabPath, int maxLength = 512)
    {
        _maxLength = maxLength;

        if (!string.IsNullOrEmpty(vocabPath) && File.Exists(vocabPath))
        {
            try
            {
                _vocabulary = LoadVocabulary(vocabPath);
            }
            catch (Exception)
            {
                // Vocabulary loading failed, will use fallback
                _vocabulary = null;
            }
        }
    }

    /// <summary>
    /// Creates a tokenizer wrapper with a pre-loaded vocabulary
    /// </summary>
    public TokenizerWrapper(Dictionary<string, int> vocabulary, int maxLength = 512)
    {
        _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
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

        if (_vocabulary is not null)
        {
            return TokenizeWithVocabulary(text);
        }

        return CreateFallbackInput(text);
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
        if (_vocabulary!.TryGetValue("[CLS]", out var clsId))
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

    private TokenizedInput CreateFallbackInput(string text)
    {
        // Simple character-based fallback tokenization
        var tokens = text.ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Take(_maxLength)
            .ToArray();

        var tokenIds = new List<int>();

        foreach (var token in tokens)
        {
            // Simple hash-based token ID
            var hashCode = token.GetHashCode(StringComparison.Ordinal);
            tokenIds.Add(Math.Abs(hashCode) % 30000 + 1000);
        }

        return CreatePaddedInput(tokenIds);
    }

    private TokenizedInput CreatePaddedInput(IReadOnlyList<int> tokenIds)
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
