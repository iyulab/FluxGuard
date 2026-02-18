using FluentAssertions;
using FluxGuard.Core;
using FluxGuard.Streaming;
using Xunit;

namespace FluxGuard.Tests.Streaming;

public class TokenValidationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var validation = new TokenValidation();

        validation.Passed.Should().BeFalse();
        validation.ShouldTerminate.Should().BeFalse();
        validation.ShouldSuppress.Should().BeFalse();
        validation.ReplacementText.Should().BeNull();
        validation.Score.Should().Be(0);
        validation.Severity.Should().Be(Severity.None);
        validation.Pattern.Should().BeNull();
        validation.MatchedText.Should().BeNull();
        validation.GuardName.Should().BeNull();
    }

    [Fact]
    public void Safe_ShouldReturnPassedResult()
    {
        var safe = TokenValidation.Safe;

        safe.Passed.Should().BeTrue();
        safe.ShouldTerminate.Should().BeFalse();
        safe.ShouldSuppress.Should().BeFalse();
    }

    [Fact]
    public void Pass_ShouldSetCorrectValues()
    {
        var pass = TokenValidation.Pass("PIIGuard");

        pass.Passed.Should().BeTrue();
        pass.GuardName.Should().Be("PIIGuard");
        pass.ShouldTerminate.Should().BeFalse();
    }

    [Fact]
    public void Pass_WithoutGuardName_ShouldBeNull()
    {
        var pass = TokenValidation.Pass();
        pass.GuardName.Should().BeNull();
    }

    [Fact]
    public void Suppress_ShouldSetCorrectValues()
    {
        var suppress = TokenValidation.Suppress(
            "PIIGuard",
            replacement: "[REDACTED]",
            pattern: "SSN pattern");

        suppress.Passed.Should().BeFalse();
        suppress.ShouldSuppress.Should().BeTrue();
        suppress.ReplacementText.Should().Be("[REDACTED]");
        suppress.Pattern.Should().Be("SSN pattern");
        suppress.GuardName.Should().Be("PIIGuard");
        suppress.ShouldTerminate.Should().BeFalse();
    }

    [Fact]
    public void Suppress_WithoutOptionalParams_ShouldDefaultToNull()
    {
        var suppress = TokenValidation.Suppress("Guard");

        suppress.ReplacementText.Should().BeNull();
        suppress.Pattern.Should().BeNull();
    }

    [Fact]
    public void Terminate_ShouldSetCorrectValues()
    {
        var terminate = TokenValidation.Terminate(
            "ToxicityGuard",
            score: 0.95,
            severity: Severity.Critical,
            pattern: "Hate speech",
            matchedText: "offensive text");

        terminate.Passed.Should().BeFalse();
        terminate.ShouldTerminate.Should().BeTrue();
        terminate.Score.Should().Be(0.95);
        terminate.Severity.Should().Be(Severity.Critical);
        terminate.Pattern.Should().Be("Hate speech");
        terminate.MatchedText.Should().Be("offensive text");
        terminate.GuardName.Should().Be("ToxicityGuard");
    }

    [Fact]
    public void Terminate_WithoutOptionalParams_ShouldDefaultToNull()
    {
        var terminate = TokenValidation.Terminate(
            "Guard",
            score: 0.8,
            severity: Severity.High);

        terminate.Pattern.Should().BeNull();
        terminate.MatchedText.Should().BeNull();
    }
}
