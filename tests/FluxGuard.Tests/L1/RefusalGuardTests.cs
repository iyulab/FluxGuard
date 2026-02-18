using FluentAssertions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Output;
using Xunit;

namespace FluxGuard.Tests.L1;

public class RefusalGuardTests
{
    #region Properties

    [Fact]
    public void Name_ReturnsL1Refusal()
    {
        var guard = new L1RefusalGuard();
        guard.Name.Should().Be("L1Refusal");
    }

    [Fact]
    public void Layer_ReturnsL1()
    {
        var guard = new L1RefusalGuard();
        guard.Layer.Should().Be("L1");
    }

    [Fact]
    public void Order_Returns200()
    {
        var guard = new L1RefusalGuard();
        guard.Order.Should().Be(200);
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        var guard = new L1RefusalGuard();
        guard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_CanBeDisabled()
    {
        var guard = new L1RefusalGuard(isEnabled: false);
        guard.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region CheckAsync — Safe

    [Theory]
    [InlineData("The capital of France is Paris.")]
    [InlineData("Here is the code you requested.")]
    [InlineData("Sure, I'd be happy to help!")]
    [InlineData("")]
    public async Task CheckAsync_SafeOutput_ReturnsSafe(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0);
    }

    #endregion

    #region CheckAsync — Refusal Detection

    [Theory]
    [InlineData("I'm sorry, but I can't help with that request.")]
    [InlineData("I am sorry, I cannot assist with this.")]
    [InlineData("I'm unable to provide that information.")]
    [InlineData("I am not able to do that for you.")]
    [InlineData("I'm afraid I cannot do that.")]
    public async Task CheckAsync_ApologyRefusal_DetectsRefusal(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue(); // Refusal is flagged, not blocked
        result.Score.Should().Be(0.3);
        result.Severity.Should().Be(Severity.Info);
        result.Pattern.Should().Be("RefusalDetected");
        result.MatchedText.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("I cannot provide instructions for that.")]
    [InlineData("I can't assist with harmful requests.")]
    [InlineData("I won't help with that topic.")]
    [InlineData("I will not provide that information.")]
    public async Task CheckAsync_DirectRefusal_DetectsRefusal(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.3);
        result.Pattern.Should().Be("RefusalDetected");
    }

    [Theory]
    [InlineData("As an AI, I don't have personal opinions.")]
    [InlineData("As an AI language model, I cannot access the internet.")]
    public async Task CheckAsync_AsAnAI_DetectsRefusal(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.3);
    }

    [Theory]
    [InlineData("This is against my programming to provide.")]
    [InlineData("It goes against my policy to answer that.")]
    public async Task CheckAsync_PolicyRefusal_DetectsRefusal(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.3);
    }

    [Theory]
    [InlineData("For ethical reasons, I must decline this request.")]
    [InlineData("For safety reasons, I cannot provide that.")]
    [InlineData("For legal reasons, I cannot assist.")]
    public async Task CheckAsync_EthicalRefusal_DetectsRefusal(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.3);
    }

    [Theory]
    [InlineData("I must decline your request.")]
    [InlineData("I must refuse to help with that.")]
    public async Task CheckAsync_MustDecline_DetectsRefusal(string output)
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.3);
    }

    [Fact]
    public async Task CheckAsync_InappropriateRequest_DetectsRefusal()
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };
        var output = "This request is inappropriate and I cannot assist.";

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.Score.Should().Be(0.3);
    }

    #endregion

    #region CheckAsync — Truncation

    [Fact]
    public async Task CheckAsync_LongRefusal_TruncatesMatchedText()
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };
        // Create a long output where the refusal match could be long
        var output = "I'm not allowed to provide instructions for creating weapons or explosives under any circumstances whatsoever.";

        var result = await guard.CheckAsync(context, output);

        result.Passed.Should().BeTrue();
        result.MatchedText.Should().NotBeNull();
        // TruncateForDisplay limits to 50 chars + "..."
        result.MatchedText!.Length.Should().BeLessThanOrEqualTo(53);
    }

    #endregion

    #region CheckAsync — Details

    [Fact]
    public async Task CheckAsync_Refusal_HasCorrectDetails()
    {
        var guard = new L1RefusalGuard();
        var context = new GuardContext { OriginalInput = "test" };

        var result = await guard.CheckAsync(context, "I'm sorry, but I cannot help.");

        result.Details.Should().Be("LLM refusal detected in output");
    }

    #endregion
}
