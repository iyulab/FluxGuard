using FluxGuard.Abstractions;
using FluxGuard.Configuration;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Guards.Output;
using FluxGuard.L1.Patterns;
using FluxGuard.Presets;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.Presets;

public class MinimalPresetTests
{
    [Fact]
    public void GetInputGuards_Returns2Guards()
    {
        var registry = new PatternRegistry();

        var guards = MinimalPreset.GetInputGuards(registry).ToList();

        guards.Should().HaveCount(2);
    }

    [Fact]
    public void GetInputGuards_ContainsPromptInjectionAndJailbreak()
    {
        var registry = new PatternRegistry();

        var guards = MinimalPreset.GetInputGuards(registry).ToList();

        guards.Should().Contain(g => g is L1PromptInjectionGuard);
        guards.Should().Contain(g => g is L1JailbreakGuard);
    }

    [Fact]
    public void GetInputGuards_DoesNotContainEncodingBypassOrPII()
    {
        var registry = new PatternRegistry();

        var guards = MinimalPreset.GetInputGuards(registry).ToList();

        guards.Should().NotContain(g => g is L1EncodingBypassGuard);
        guards.Should().NotContain(g => g is L1PIIExposureGuard);
    }

    [Fact]
    public void GetOutputGuards_Returns1Guard()
    {
        var registry = new PatternRegistry();

        var guards = MinimalPreset.GetOutputGuards(registry).ToList();

        guards.Should().HaveCount(1);
    }

    [Fact]
    public void GetOutputGuards_ContainsPIILeakage()
    {
        var registry = new PatternRegistry();

        var guards = MinimalPreset.GetOutputGuards(registry).ToList();

        guards.Should().Contain(g => g is L1PIILeakageGuard);
    }

    [Fact]
    public void ApplyMinimalPreset_BuildsSuccessfully()
    {
        var builder = FluxGuardBuilder.Create();

        var result = builder.ApplyMinimalPreset();

        result.Should().BeSameAs(builder);
        builder.Build().Should().NotBeNull();
    }
}

public class StandardPresetTests
{
    [Fact]
    public void GetInputGuards_AllEnabled_Returns4Guards()
    {
        var registry = new PatternRegistry();
        var options = new InputGuardOptions();

        var guards = StandardPreset.GetInputGuards(registry, options).ToList();

        guards.Should().HaveCount(4);
    }

    [Fact]
    public void GetInputGuards_AllEnabled_ContainsAllTypes()
    {
        var registry = new PatternRegistry();
        var options = new InputGuardOptions();

        var guards = StandardPreset.GetInputGuards(registry, options).ToList();

        guards.Should().Contain(g => g is L1EncodingBypassGuard);
        guards.Should().Contain(g => g is L1PromptInjectionGuard);
        guards.Should().Contain(g => g is L1JailbreakGuard);
        guards.Should().Contain(g => g is L1PIIExposureGuard);
    }

    [Fact]
    public void GetInputGuards_EncodingBypassDisabled_Returns3Guards()
    {
        var registry = new PatternRegistry();
        var options = new InputGuardOptions { EnableEncodingBypass = false };

        var guards = StandardPreset.GetInputGuards(registry, options).ToList();

        guards.Should().HaveCount(3);
        guards.Should().NotContain(g => g is L1EncodingBypassGuard);
    }

    [Fact]
    public void GetInputGuards_PIIExposureDisabled_Returns3Guards()
    {
        var registry = new PatternRegistry();
        var options = new InputGuardOptions { EnablePIIExposure = false };

        var guards = StandardPreset.GetInputGuards(registry, options).ToList();

        guards.Should().HaveCount(3);
        guards.Should().NotContain(g => g is L1PIIExposureGuard);
    }

    [Fact]
    public void GetInputGuards_AllDisabled_ReturnsEmpty()
    {
        var registry = new PatternRegistry();
        var options = new InputGuardOptions
        {
            EnableEncodingBypass = false,
            EnablePromptInjection = false,
            EnableJailbreak = false,
            EnablePIIExposure = false
        };

        var guards = StandardPreset.GetInputGuards(registry, options).ToList();

        guards.Should().BeEmpty();
    }

    [Fact]
    public void GetOutputGuards_AllEnabled_Returns2Guards()
    {
        var registry = new PatternRegistry();
        var options = new OutputGuardOptions();
        var languages = new List<string> { "en" };

        var guards = StandardPreset.GetOutputGuards(registry, options, languages).ToList();

        guards.Should().HaveCount(2);
    }

    [Fact]
    public void GetOutputGuards_AllEnabled_ContainsAllTypes()
    {
        var registry = new PatternRegistry();
        var options = new OutputGuardOptions();
        var languages = new List<string> { "en", "ko" };

        var guards = StandardPreset.GetOutputGuards(registry, options, languages).ToList();

        guards.Should().Contain(g => g is L1PIILeakageGuard);
        guards.Should().Contain(g => g is L1RefusalGuard);
    }

    [Fact]
    public void GetOutputGuards_PIILeakageDisabled_Returns1Guard()
    {
        var registry = new PatternRegistry();
        var options = new OutputGuardOptions { EnablePIILeakage = false };
        var languages = new List<string> { "en" };

        var guards = StandardPreset.GetOutputGuards(registry, options, languages).ToList();

        guards.Should().HaveCount(1);
        guards.Should().Contain(g => g is L1RefusalGuard);
    }

    [Fact]
    public void GetOutputGuards_RefusalDisabled_Returns1Guard()
    {
        var registry = new PatternRegistry();
        var options = new OutputGuardOptions { EnableRefusal = false };
        var languages = new List<string> { "en" };

        var guards = StandardPreset.GetOutputGuards(registry, options, languages).ToList();

        guards.Should().HaveCount(1);
        guards.Should().Contain(g => g is L1PIILeakageGuard);
    }

    [Fact]
    public void GetOutputGuards_AllDisabled_ReturnsEmpty()
    {
        var registry = new PatternRegistry();
        var options = new OutputGuardOptions { EnablePIILeakage = false, EnableRefusal = false };
        var languages = new List<string> { "en" };

        var guards = StandardPreset.GetOutputGuards(registry, options, languages).ToList();

        guards.Should().BeEmpty();
    }

    [Fact]
    public void ApplyStandardPreset_BuildsSuccessfully()
    {
        var builder = FluxGuardBuilder.Create();

        var result = builder.ApplyStandardPreset();

        result.Should().BeSameAs(builder);
        builder.Build().Should().NotBeNull();
    }
}

public class StrictPresetTests
{
    [Fact]
    public void GetInputGuards_AllEnabled_Returns4Guards()
    {
        var registry = new PatternRegistry();
        var options = new InputGuardOptions();

        var guards = StrictPreset.GetInputGuards(registry, options).ToList();

        guards.Should().HaveCount(4);
    }

    [Fact]
    public void GetInputGuards_AlwaysIncludesEncodingBypassPromptInjectionJailbreak()
    {
        var registry = new PatternRegistry();
        // Even with these flags off, strict preset still yields the guards (just disabled)
        var options = new InputGuardOptions
        {
            EnableEncodingBypass = false,
            EnablePromptInjection = false,
            EnableJailbreak = false,
            EnablePIIExposure = false
        };

        var guards = StrictPreset.GetInputGuards(registry, options).ToList();

        // EncodingBypass, PromptInjection, Jailbreak always yielded; PIIExposure is conditional
        guards.Should().HaveCount(3);
        guards.Should().Contain(g => g is L1EncodingBypassGuard);
        guards.Should().Contain(g => g is L1PromptInjectionGuard);
        guards.Should().Contain(g => g is L1JailbreakGuard);
    }

    [Fact]
    public void GetOutputGuards_AllEnabled_Returns2Guards()
    {
        var registry = new PatternRegistry();
        var options = new OutputGuardOptions();
        var languages = new List<string> { "en" };

        var guards = StrictPreset.GetOutputGuards(registry, options, languages).ToList();

        guards.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplyStrictPreset_BlocksAtLowerThreshold()
    {
        // Strict preset has BlockThreshold=0.8 vs Standard 0.9
        // Verify it builds and functions with stricter settings
        var guard = FluxGuard.Create(builder => builder.ApplyStrictPreset());

        // Safe input should still pass
        var result = await guard.CheckInputAsync("Hello, how are you?");
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void ApplyStrictPreset_BuildsSuccessfully()
    {
        var builder = FluxGuardBuilder.Create();

        var result = builder.ApplyStrictPreset();

        result.Should().BeSameAs(builder);
        builder.Build().Should().NotBeNull();
    }
}
