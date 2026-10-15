using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Presets;
using AwesomeAssertions;
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// <see cref="InputGuardOptions.PIIExposureActions"/> / <see cref="OutputGuardOptions.PIILeakageActions"/>: a consumer
/// names a PII pattern and what to do with it; every pattern not named keeps its severity default.
/// </summary>
public class PIIActionTests
{
    private static IFluxGuard Build(Action<FluxGuardBuilder>? configure = null)
    {
        var builder = FluxGuardBuilder.Create(b => b.ApplyStandardPreset());
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task InputIpAddress_Ignored_Passes()
    {
        var guard = Build(b => b.ConfigureInputGuards(o => o.PIIExposureActions["IPAddress"] = PIIAction.Ignore));

        var result = await guard.CheckInputAsync("서버 192.168.0.10 에 접속이 안 돼", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    [Fact]
    public async Task InputIpAddress_NotNamed_KeepsFlagDefault()
    {
        var guard = Build();

        var result = await guard.CheckInputAsync("서버 192.168.0.10 에 접속이 안 돼", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Flagged);
    }

    [Fact]
    public async Task InputEmail_Block_Blocks()
    {
        var guard = Build(b => b.ConfigureInputGuards(o => o.PIIExposureActions["EmailAddress"] = PIIAction.Block));

        var result = await guard.CheckInputAsync(
            "Please reply to jane.doe@example.com with the summary", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
        result.TriggeredGuards.Should().Contain(t => t.Pattern == "EmailAddress");
    }

    [Fact]
    public async Task InputCreditCard_Flag_OverridesBlockDefault()
    {
        // Each matching pattern acts on its own: text that a second Critical pattern also matches still blocks unless
        // that pattern is named too.
        var cardOnly = Build(b => b.ConfigureInputGuards(o => o.PIIExposureActions["CreditCard"] = PIIAction.Flag));
        var both = Build(b => b.ConfigureInputGuards(o =>
        {
            o.PIIExposureActions["CreditCard"] = PIIAction.Flag;
            o.PIIExposureActions["Password"] = PIIAction.Flag;
        }));
        const string input = "My card is 4111-1111-1111-1111, password: hunter2!";

        var cardOnlyResult = await cardOnly.CheckInputAsync(input, TestContext.Current.CancellationToken);
        var bothResult = await both.CheckInputAsync(input, TestContext.Current.CancellationToken);
        var cardAlone = await cardOnly.CheckInputAsync("My card is 4111-1111-1111-1111", TestContext.Current.CancellationToken);

        cardAlone.IsBlocked.Should().BeFalse();
        cardAlone.Decision.Should().Be(GuardDecision.Flagged);
        cardOnlyResult.IsBlocked.Should().BeTrue();
        cardOnlyResult.BlockReason.Should().Contain("Password");
        bothResult.IsBlocked.Should().BeFalse();
        bothResult.Decision.Should().Be(GuardDecision.Flagged);
    }

    [Fact]
    public async Task OutputEmail_Block_Blocks()
    {
        var guard = Build(b => b.ConfigureOutputGuards(o => o.PIILeakageActions["EmailAddress"] = PIIAction.Block));

        var result = await guard.CheckOutputAsync(
            "who wrote it?", "It was written by jane.doe@example.com", TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task OutputEmail_NotNamed_IsFlagged()
    {
        var guard = Build();

        var result = await guard.CheckOutputAsync(
            "who wrote it?", "It was written by jane.doe@example.com", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Flagged);
    }

    [Fact]
    public async Task PatternName_IsCaseInsensitive()
    {
        var guard = Build(b => b.ConfigureInputGuards(o => o.PIIExposureActions["ipaddress"] = PIIAction.Ignore));

        var result = await guard.CheckInputAsync("I can't reach 10.0.0.12", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Pass);
    }

    [Fact]
    public void UnknownPatternName_ThrowsAtBuild_NamingTheKnownOnes()
    {
        var act = () => Build(b => b.ConfigureInputGuards(o => o.PIIExposureActions["IpAdress"] = PIIAction.Ignore));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*IpAdress*")
            .And.Message.Should().Contain("IPAddress").And.Contain("ResidentRegistrationNumber");
    }

    [Fact]
    public void UndefinedAction_ThrowsAtBuild()
    {
        var act = () => Build(b => b.ConfigureOutputGuards(o => o.PIILeakageActions["EmailAddress"] = (PIIAction)0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
