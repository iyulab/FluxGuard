using AwesomeAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using Xunit;

namespace FluxGuard.Tests.Configuration;

/// <summary>
/// FailMode ↔ GuardPreset linkage (D20).
/// Choosing <see cref="GuardPreset.Strict"/> states "security over availability"; the fail mode
/// must follow that intent unless the consumer explicitly says otherwise.
/// </summary>
public class FailModePresetLinkageTests
{
    private sealed class ThrowingInputGuard : IInputGuard
    {
        public string Name => "throwing-guard";
        public string Layer => "L1";
        public bool IsEnabled => true;
        public int Order => 0;

        public ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
            => throw new InvalidOperationException("guard blew up");
    }

    // --- Acceptance #1 — user-visible behavior -------------------------------

    [Fact]
    public async Task StrictPreset_WhenGuardThrows_BlocksTheRequest()
    {
        var guard = FluxGuard.Create(b => b
            .WithPreset(GuardPreset.Strict)
            .AddInputGuard(new ThrowingInputGuard()));

        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        result.Decision.Should().Be(GuardDecision.Blocked,
            "Strict without an explicit FailMode must fail closed, not silently bypass the guard");
    }

    // --- Acceptance #2 — explicit override always wins -----------------------

    [Fact]
    public async Task StrictPreset_WithExplicitOpen_PassesWhenGuardThrows()
    {
        var guard = FluxGuard.Create(b => b
            .WithPreset(GuardPreset.Strict)
            .WithFailMode(FailMode.Open)
            .AddInputGuard(new ThrowingInputGuard()));

        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        result.Decision.Should().NotBe(GuardDecision.Blocked,
            "an explicit WithFailMode(Open) must override the preset default");
    }

    [Fact]
    public void ExplicitFailMode_SurvivesLaterPresetChange()
    {
        var options = new FluxGuardOptions { FailMode = FailMode.Open };

        options.Preset = GuardPreset.Strict;

        options.FailMode.Should().Be(FailMode.Open,
            "explicit configuration must not be undone by setting a preset afterwards");
    }

    // --- Acceptance #3 — Minimal/Standard unchanged --------------------------

    [Theory]
    [InlineData(GuardPreset.Minimal)]
    [InlineData(GuardPreset.Standard)]
    public async Task NonStrictPresets_WhenGuardThrows_StillPass(GuardPreset preset)
    {
        var guard = FluxGuard.Create(b => b
            .WithPreset(preset)
            .AddInputGuard(new ThrowingInputGuard()));

        var result = await guard.CheckInputAsync("hello", TestContext.Current.CancellationToken);

        result.Decision.Should().NotBe(GuardDecision.Blocked,
            "availability-first presets keep the existing fail-open behavior");
    }

    // --- Resolution table ----------------------------------------------------

    [Theory]
    [InlineData(GuardPreset.Minimal, FailMode.Open)]
    [InlineData(GuardPreset.Standard, FailMode.Open)]
    [InlineData(GuardPreset.Strict, FailMode.Closed)]
    public void UnsetFailMode_ResolvesFromPreset(GuardPreset preset, FailMode expected)
    {
        var options = new FluxGuardOptions { Preset = preset };

        options.FailMode.Should().Be(expected);
    }

    [Theory]
    [InlineData(GuardPreset.Strict, FailMode.Open)]
    [InlineData(GuardPreset.Standard, FailMode.Closed)]
    public void ExplicitFailMode_AlwaysWinsOverPreset(GuardPreset preset, FailMode explicitMode)
    {
        var options = new FluxGuardOptions { Preset = preset, FailMode = explicitMode };

        options.FailMode.Should().Be(explicitMode);
    }

    // --- DI hop: options resolved from the container are copied onto a builder --------

    [Fact]
    public void CopyTo_LeavesAnUnsetFailModeUnset()
    {
        var source = new FluxGuardOptions { Preset = GuardPreset.Standard };
        var target = new FluxGuardOptions();

        source.CopyTo(target);
        target.Preset = GuardPreset.Strict;

        target.FailMode.Should().Be(FailMode.Closed,
            "an unset fail mode must survive the copy as unset, so it still resolves from the preset");
    }

    [Fact]
    public void CopyTo_CarriesAnExplicitFailMode()
    {
        var source = new FluxGuardOptions { Preset = GuardPreset.Strict, FailMode = FailMode.Open };
        var target = new FluxGuardOptions();

        source.CopyTo(target);

        target.FailMode.Should().Be(FailMode.Open);
    }
}
