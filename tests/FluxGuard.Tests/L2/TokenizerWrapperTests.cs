using AwesomeAssertions;
using FluxGuard.L2.ML;
using Xunit;

namespace FluxGuard.Tests.L2;

/// <summary>
/// A tokenizer that cannot read its vocabulary must say so, not invent tokens.
/// </summary>
/// <remarks>
/// <para>
/// The wrapper used to fall back to hashing each whitespace-separated word into an id
/// (<c>Math.Abs(token.GetHashCode()) % 30000 + 1000</c>) whenever the vocabulary file was missing,
/// empty or unreadable. The classification model then scored arbitrary ids and the guard returned
/// that score as a verdict. Two things make this worse than "inaccurate":
/// </para>
/// <list type="number">
/// <item>.NET randomises string hash codes <b>per process</b>, so the same text produced different
/// ids on every run — the scores were not even reproducible.</item>
/// <item>The guard reported them as an ordinary safe/unsafe answer, so a consumer who had opted
/// into L2 got a protection-shaped result from a tokenizer that was not tokenizing.</item>
/// </list>
/// <para>
/// 0.16.0 made "L2 is explicitly on but cannot run" an error at every other point in the path
/// (<c>AddL2Guards</c> checks the model files exist; a guard that throws is a guard error the fail
/// mode decides). This is the remaining hole: file present but unreadable, and a guard built by
/// hand rather than through the builder.
/// </para>
/// </remarks>
public sealed class TokenizerWrapperTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fluxguard-vocab-{Guid.NewGuid():N}");

    public TokenizerWrapperTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string WriteVocab(string name, params string[] lines)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void AMissingVocabularyFile_ThrowsAndNamesIt()
    {
        var path = Path.Combine(_dir, "not-here.txt");

        var act = () => new TokenizerWrapper(path);

        act.Should().Throw<FileNotFoundException>().Which.Message.Should().Contain("not-here.txt");
    }

    [Fact]
    public void AnEmptyPath_Throws()
    {
        // How a hand-built guard arrives here: the session manager has no model registered, so the
        // guard passes `modelInfo?.TokenizerPath ?? string.Empty`.
        var act = () => new TokenizerWrapper(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AVocabularyFileWithNoUsableEntries_ThrowsAndNamesIt()
    {
        // Readable, so the old code "loaded" it - into an empty dictionary, after which every word
        // is unknown and every score is noise.
        var path = WriteVocab("empty-vocab.txt", "", "   ", "");

        var act = () => new TokenizerWrapper(path);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("empty-vocab.txt");
    }

    [Fact]
    public void AUsableVocabulary_TokenizesWithIt()
    {
        var path = WriteVocab("vocab.txt", "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello", "world");

        using var tokenizer = new TokenizerWrapper(path, maxLength: 8);
        var result = tokenizer.Tokenize("hello world");

        // [CLS] hello world [SEP] then padding.
        result.InputIds.Take(4).Should().Equal(2L, 4L, 5L, 3L);
        result.AttentionMask.Take(4).Should().Equal(1L, 1L, 1L, 1L);
        result.AttentionMask.Skip(4).Should().AllBeEquivalentTo(0L);
    }

    [Fact]
    public void AnUnknownWord_BecomesTheUnknownToken_NotAHash()
    {
        var path = WriteVocab("vocab.txt", "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello");

        using var tokenizer = new TokenizerWrapper(path, maxLength: 8);
        var result = tokenizer.Tokenize("hello elephant");

        result.InputIds.Take(4).Should().Equal(2L, 4L, 1L, 3L);
    }

    [Fact]
    public void TheSameTextTokenizesToTheSameIdsEveryTime()
    {
        // The property the hash fallback could not hold across processes. Within one process this
        // is weak evidence, so it is paired with the facts above: there is no longer a path that
        // derives an id from a hash at all.
        var path = WriteVocab("vocab.txt", "[PAD]", "[UNK]", "[CLS]", "[SEP]", "alpha", "beta");

        using var a = new TokenizerWrapper(path, maxLength: 8);
        using var b = new TokenizerWrapper(path, maxLength: 8);

        a.Tokenize("alpha beta").InputIds.Should().Equal(b.Tokenize("alpha beta").InputIds);
    }

    [Fact]
    public void APreLoadedVocabularyMustNotBeEmptyEither()
    {
        var act = () => new TokenizerWrapper(new Dictionary<string, int>());

        act.Should().Throw<ArgumentException>();
    }
}
