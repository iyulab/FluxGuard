using FluentAssertions;
using FluxGuard.Streaming;
using Xunit;

namespace FluxGuard.Tests.Streaming;

public class ChunkBufferTests
{
    [Fact]
    public void Append_AccumulatesChunks()
    {
        // Arrange
        var buffer = new ChunkBuffer();

        // Act
        buffer.Append("Hello ");
        buffer.Append("world");

        // Assert
        buffer.Content.Should().Be("Hello world");
        buffer.Length.Should().Be(11);
    }

    [Fact]
    public void TryExtractSentence_WithCompleteSentence_ExtractsSentence()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Hello world. How are you?");

        // Act
        var found = buffer.TryExtractSentence(out var sentence);

        // Assert
        found.Should().BeTrue();
        sentence.Should().Be("Hello world. ");
    }

    [Fact]
    public void TryExtractSentence_WithoutSentenceEnd_ReturnsFalse()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Hello world");

        // Act
        var found = buffer.TryExtractSentence(out var sentence);

        // Assert
        found.Should().BeFalse();
        sentence.Should().BeNull();
    }

    [Fact]
    public void ExtractAllSentences_MultipleSentences_ExtractsAll()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("First sentence. Second sentence! Third question? More text");

        // Act
        var sentences = buffer.ExtractAllSentences();

        // Assert
        sentences.Should().HaveCount(3);
        sentences[0].Should().Be("First sentence. ");
        sentences[1].Should().Be("Second sentence! ");
        sentences[2].Should().Be("Third question? ");
    }

    [Fact]
    public void Flush_ReturnsRemainingContent()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Complete. Incomplete");

        // Extract complete sentence
        buffer.TryExtractSentence(out _);

        // Act
        var remaining = buffer.Flush();

        // Assert
        remaining.Should().Be("Incomplete");
    }

    [Fact]
    public void Clear_ResetsBuffer()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Some content");

        // Act
        buffer.Clear();

        // Assert
        buffer.Length.Should().Be(0);
        buffer.Content.Should().BeEmpty();
    }

    [Fact]
    public void TryExtractSentence_ExceedsMaxBufferSize_ForcesExtraction()
    {
        // Arrange
        var buffer = new ChunkBuffer(maxBufferSize: 10);
        buffer.Append("This is a very long text without any sentence ending");

        // Act
        var found = buffer.TryExtractSentence(out var sentence);

        // Assert - Should force extraction when exceeding max buffer
        found.Should().BeTrue();
        sentence.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MayContainIncompleteSensitiveData_PartialEmail_ReturnsTrue()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Contact me at user@");

        // Act
        var mayContain = buffer.MayContainIncompleteSensitiveData();

        // Assert
        mayContain.Should().BeTrue();
    }

    [Fact]
    public void MayContainIncompleteSensitiveData_NormalText_ReturnsFalse()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Hello world");

        // Act
        var mayContain = buffer.MayContainIncompleteSensitiveData();

        // Assert
        mayContain.Should().BeFalse();
    }

    [Fact]
    public void Append_EmptyChunk_DoesNothing()
    {
        // Arrange
        var buffer = new ChunkBuffer();
        buffer.Append("Hello");

        // Act
        buffer.Append("");
        buffer.Append(null!);

        // Assert
        buffer.Length.Should().Be(5);
    }
}
