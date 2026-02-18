using FluentAssertions;
using FluxGuard.Remote.RAG;
using Xunit;

namespace FluxGuard.Remote.Tests.RAG;

public class RAGDocumentTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var doc = new RAGDocument { Content = "Test content" };

        doc.Id.Should().BeNull();
        doc.Source.Should().BeNull();
        doc.Metadata.Should().BeNull();
        doc.RelevanceScore.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var metadata = new Dictionary<string, object> { ["author"] = "test" };
        var doc = new RAGDocument
        {
            Id = "doc-1",
            Content = "Full content",
            Source = "https://example.com",
            Metadata = metadata,
            RelevanceScore = 0.92
        };

        doc.Id.Should().Be("doc-1");
        doc.RelevanceScore.Should().Be(0.92);
        doc.Metadata.Should().ContainKey("author");
    }
}

public class RAGDocumentValidationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var doc = new RAGDocument { Content = "test" };
        var validation = new RAGDocumentValidation { Document = doc };

        validation.IsSafe.Should().BeFalse();
        validation.RiskScore.Should().Be(0);
        validation.Threats.Should().BeEmpty();
        validation.SuggestedAction.Should().Be(RAGAction.Include);
        validation.SanitizedContent.Should().BeNull();
    }

    [Fact]
    public void Safe_ShouldSetCorrectValues()
    {
        var doc = new RAGDocument { Content = "Safe content" };
        var result = RAGDocumentValidation.Safe(doc);

        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0.0);
        result.SuggestedAction.Should().Be(RAGAction.Include);
        result.Document.Should().BeSameAs(doc);
    }

    [Fact]
    public void Block_ShouldSetCorrectValues()
    {
        var doc = new RAGDocument { Content = "Malicious content" };
        var threats = new[]
        {
            new RAGThreat
            {
                Type = RAGThreatType.IndirectInjection,
                Confidence = 0.95,
                Pattern = "ignore previous instructions"
            }
        };

        var result = RAGDocumentValidation.Block(doc, 0.95, threats);

        result.IsSafe.Should().BeFalse();
        result.RiskScore.Should().Be(0.95);
        result.SuggestedAction.Should().Be(RAGAction.Block);
        result.Threats.Should().ContainSingle();
    }
}

public class RAGThreatTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var threat = new RAGThreat();

        threat.Type.Should().Be(RAGThreatType.None);
        threat.Confidence.Should().Be(0);
        threat.Pattern.Should().BeNull();
        threat.StartIndex.Should().BeNull();
        threat.Description.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var threat = new RAGThreat
        {
            Type = RAGThreatType.DataExfiltration,
            Confidence = 0.88,
            Pattern = "send data to external",
            StartIndex = 42,
            Description = "Attempt to exfiltrate user data"
        };

        threat.Type.Should().Be(RAGThreatType.DataExfiltration);
        threat.StartIndex.Should().Be(42);
    }
}

public class RAGThreatTypeEnumTests
{
    [Fact]
    public void ShouldHaveSevenValues()
    {
        Enum.GetValues<RAGThreatType>().Should().HaveCount(7);
    }

    [Theory]
    [InlineData(RAGThreatType.None, 0)]
    [InlineData(RAGThreatType.IndirectInjection, 1)]
    [InlineData(RAGThreatType.PromptOverride, 2)]
    [InlineData(RAGThreatType.EmbeddedInstruction, 3)]
    [InlineData(RAGThreatType.MaliciousLink, 4)]
    [InlineData(RAGThreatType.DataExfiltration, 5)]
    [InlineData(RAGThreatType.EncodedContent, 6)]
    public void ShouldHaveExpectedIntValues(RAGThreatType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

public class RAGActionEnumTests
{
    [Fact]
    public void ShouldHaveFourValues()
    {
        Enum.GetValues<RAGAction>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(RAGAction.Include, 0)]
    [InlineData(RAGAction.Sanitize, 1)]
    [InlineData(RAGAction.Block, 2)]
    [InlineData(RAGAction.Review, 3)]
    public void ShouldHaveExpectedIntValues(RAGAction action, int expected)
    {
        ((int)action).Should().Be(expected);
    }
}
