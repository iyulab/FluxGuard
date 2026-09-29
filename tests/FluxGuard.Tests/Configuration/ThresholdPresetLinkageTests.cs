using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.Presets;
using Xunit;

namespace FluxGuard.Tests.Configuration;

/// <summary>
/// The Strict preset's thresholds (block 0.8, flag 0.5, escalation 0.3) are the same whichever way Strict is chosen —
/// <c>ApplyStrictPreset()</c>, <c>WithPreset(GuardPreset.Strict)</c>, or options bound from configuration. Before, only
/// <c>ApplyStrictPreset()</c> lowered them; the other two kept the standard 0.9 / 0.7 / 0.5.
/// </summary>
public class ThresholdPresetLinkageTests
{
    public static TheoryData<string> StrictEntryPoints => ["ApplyStrictPreset", "WithPreset", "Options"];

    private static FluxGuardOptions OptionsFor(string entryPoint) => entryPoint switch
    {
        "ApplyStrictPreset" => FluxGuardBuilder.Create().ApplyStrictPreset().GetOptions(),
        "WithPreset" => FluxGuardBuilder.Create().WithPreset(GuardPreset.Strict).GetOptions(),
        "Options" => new FluxGuardOptions { Preset = GuardPreset.Strict },
        _ => throw new ArgumentOutOfRangeException(nameof(entryPoint)),
    };

    [Theory]
    [MemberData(nameof(StrictEntryPoints))]
    public void Strict_HasTheStrictThresholds_WhicheverWayItIsChosen(string entryPoint)
    {
        var options = OptionsFor(entryPoint);

        Assert.Equal((0.8, 0.5, 0.3), (options.BlockThreshold, options.FlagThreshold, options.EscalationThreshold));
    }

    [Fact]
    public void Standard_HasTheStandardThresholds()
    {
        var options = new FluxGuardOptions();

        Assert.Equal((0.9, 0.7, 0.5), (options.BlockThreshold, options.FlagThreshold, options.EscalationThreshold));
    }

    [Fact]
    public void AnExplicitThreshold_WinsOverThePreset_InEitherOrder()
    {
        var before = new FluxGuardOptions { BlockThreshold = 0.95, Preset = GuardPreset.Strict };
        var after = new FluxGuardOptions { Preset = GuardPreset.Strict };
        after.FlagThreshold = 0.6;

        Assert.Equal(0.95, before.BlockThreshold);
        Assert.Equal(0.6, after.FlagThreshold);
        Assert.Equal(0.3, after.EscalationThreshold);
    }

    [Fact]
    public void CopyTo_KeepsAnUnsetThresholdFollowingThePreset()
    {
        var source = new FluxGuardOptions { EscalationThreshold = 0.4 };
        var target = new FluxGuardOptions();

        source.CopyTo(target);
        target.Preset = GuardPreset.Strict;

        Assert.Equal((0.8, 0.5, 0.4), (target.BlockThreshold, target.FlagThreshold, target.EscalationThreshold));
    }
}
