using FluentAssertions;
using FluxGuard.Remote.Hallucination;
using Xunit;

namespace FluxGuard.Remote.Tests.Hallucination;

public class HallucinationResultTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var result = new HallucinationResult();

        result.IsGrounded.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.HallucinationScore.Should().Be(0);
        result.Type.Should().Be(HallucinationType.None);
        result.HallucinatedClaims.Should().BeEmpty();
        result.Reasoning.Should().BeNull();
        result.LatencyMs.Should().Be(0);
    }

    [Fact]
    public void Grounded_ShouldSetCorrectValues()
    {
        var result = HallucinationResult.Grounded(0.95, 120.5);

        result.IsGrounded.Should().BeTrue();
        result.Confidence.Should().Be(0.95);
        result.HallucinationScore.Should().Be(0.0);
        result.Type.Should().Be(HallucinationType.None);
        result.HallucinatedClaims.Should().BeEmpty();
        result.LatencyMs.Should().Be(120.5);
    }

    [Fact]
    public void Hallucinated_ShouldSetCorrectValues()
    {
        var claims = new[]
        {
            new HallucinatedClaim
            {
                Claim = "Paris is the capital of Germany",
                Type = HallucinationType.FactualError,
                Confidence = 0.99,
                Correction = "Berlin is the capital of Germany"
            }
        };

        var result = HallucinationResult.Hallucinated(
            hallucinationScore: 0.85,
            confidence: 0.92,
            type: HallucinationType.FactualError,
            claims: claims,
            reasoning: "Geographic factual error detected",
            latencyMs: 200.0);

        result.IsGrounded.Should().BeFalse();
        result.HallucinationScore.Should().Be(0.85);
        result.Confidence.Should().Be(0.92);
        result.Type.Should().Be(HallucinationType.FactualError);
        result.HallucinatedClaims.Should().ContainSingle();
        result.Reasoning.Should().Contain("factual error");
        result.LatencyMs.Should().Be(200.0);
    }

    [Fact]
    public void Hallucinated_WithoutOptionalParams_ShouldDefaultCorrectly()
    {
        var result = HallucinationResult.Hallucinated(
            hallucinationScore: 0.7,
            confidence: 0.8,
            type: HallucinationType.Fabrication);

        result.HallucinatedClaims.Should().BeEmpty();
        result.Reasoning.Should().BeNull();
        result.LatencyMs.Should().Be(0);
    }
}

public class HallucinationTypeEnumTests
{
    [Fact]
    public void ShouldHaveSixValues()
    {
        Enum.GetValues<HallucinationType>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(HallucinationType.None, 0)]
    [InlineData(HallucinationType.FactualError, 1)]
    [InlineData(HallucinationType.Fabrication, 2)]
    [InlineData(HallucinationType.Contradiction, 3)]
    [InlineData(HallucinationType.UnsupportedClaim, 4)]
    [InlineData(HallucinationType.EntityConfusion, 5)]
    public void ShouldHaveExpectedIntValues(HallucinationType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

public class HallucinatedClaimTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var claim = new HallucinatedClaim { Claim = "Test claim" };

        claim.Type.Should().Be(HallucinationType.None);
        claim.Confidence.Should().Be(0);
        claim.Correction.Should().BeNull();
        claim.ContradictingSource.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var claim = new HallucinatedClaim
        {
            Claim = "The company was founded in 2020",
            Type = HallucinationType.FactualError,
            Confidence = 0.95,
            Correction = "The company was founded in 2018",
            ContradictingSource = "Annual report 2018"
        };

        claim.Claim.Should().Contain("2020");
        claim.Correction.Should().Contain("2018");
        claim.ContradictingSource.Should().Be("Annual report 2018");
    }
}
