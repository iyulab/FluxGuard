using FluentAssertions;
using FluxGuard.L2.Models;
using Xunit;

namespace FluxGuard.Tests.L2.Models;

public class InferenceResultTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var result = new InferenceResult { Label = "safe", Score = 0.95 };

        result.Probabilities.Should().BeEmpty();
        result.LatencyMs.Should().Be(0);
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ModelId.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var probs = new Dictionary<string, double>
        {
            ["safe"] = 0.95,
            ["injection"] = 0.05
        };
        var result = new InferenceResult
        {
            Label = "safe",
            Score = 0.95,
            Probabilities = probs,
            LatencyMs = 12.5,
            ModelId = "prompt-injection-v2"
        };

        result.Label.Should().Be("safe");
        result.Score.Should().Be(0.95);
        result.Probabilities.Should().HaveCount(2);
        result.LatencyMs.Should().Be(12.5);
        result.ModelId.Should().Be("prompt-injection-v2");
    }

    [Fact]
    public void Failed_ShouldSetCorrectValues()
    {
        var result = InferenceResult.Failed("Model not loaded", "prompt-injection-v2");

        result.Label.Should().Be("unknown");
        result.Score.Should().Be(0);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Model not loaded");
        result.ModelId.Should().Be("prompt-injection-v2");
    }

    [Fact]
    public void Failed_WithoutModelId_ShouldBeNull()
    {
        var result = InferenceResult.Failed("ONNX runtime error");

        result.ModelId.Should().BeNull();
        result.Success.Should().BeFalse();
    }
}

public class ModelInfoTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var info = new ModelInfo
        {
            Id = "test-model",
            Name = "Test",
            Version = "1.0",
            ModelPath = "/models/test.onnx"
        };

        info.TokenizerPath.Should().BeNull();
        info.MaxSequenceLength.Should().Be(512);
        info.InputNames.Should().BeEquivalentTo(["input_ids", "attention_mask"]);
        info.OutputName.Should().Be("logits");
        info.Labels.Should().BeEmpty();
        info.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ShouldInitialize_WithCustomLabels()
    {
        var labels = new Dictionary<int, string>
        {
            { 0, "safe" },
            { 1, "injection" }
        };
        var info = new ModelInfo
        {
            Id = "prompt-injection-v2",
            Name = "DeBERTa-v3 Prompt Injection",
            Version = "2.0",
            ModelPath = "/models/prompt-injection/model.onnx",
            TokenizerPath = "/models/prompt-injection/vocab.txt",
            MaxSequenceLength = 256,
            Labels = labels,
            Enabled = false
        };

        info.Labels.Should().HaveCount(2);
        info.Labels[0].Should().Be("safe");
        info.MaxSequenceLength.Should().Be(256);
        info.Enabled.Should().BeFalse();
    }
}
