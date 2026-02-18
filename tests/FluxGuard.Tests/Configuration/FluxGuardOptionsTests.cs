using FluxGuard.Configuration;
using FluxGuard.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FluxGuard.Tests.Configuration;

public class FluxGuardOptionsTests
{
    [Fact]
    public void SectionName_IsFluxGuard()
    {
        FluxGuardOptions.SectionName.Should().Be("FluxGuard");
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new FluxGuardOptions();

        opts.Preset.Should().Be(GuardPreset.Standard);
        opts.FailMode.Should().Be(FailMode.Open);
        opts.LogLevel.Should().Be(LogLevel.Warning);
        opts.EnableL2Guards.Should().BeTrue();
        opts.EnableL3Escalation.Should().BeFalse();
        opts.BlockThreshold.Should().Be(0.9);
        opts.FlagThreshold.Should().Be(0.7);
        opts.EscalationThreshold.Should().Be(0.5);
        opts.GuardTimeoutMs.Should().Be(5000);
        opts.EscalationTimeoutMs.Should().Be(5000);
    }

    [Fact]
    public void Defaults_InputGuards_AreNotNull()
    {
        var opts = new FluxGuardOptions();

        opts.InputGuards.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_OutputGuards_AreNotNull()
    {
        var opts = new FluxGuardOptions();

        opts.OutputGuards.Should().NotBeNull();
    }

    [Fact]
    public void AllProperties_AreSettable()
    {
        var opts = new FluxGuardOptions
        {
            Preset = GuardPreset.Strict,
            FailMode = FailMode.Closed,
            LogLevel = LogLevel.Debug,
            EnableL2Guards = false,
            EnableL3Escalation = true,
            BlockThreshold = 0.8,
            FlagThreshold = 0.5,
            EscalationThreshold = 0.3,
            GuardTimeoutMs = 10000,
            EscalationTimeoutMs = 3000
        };

        opts.Preset.Should().Be(GuardPreset.Strict);
        opts.FailMode.Should().Be(FailMode.Closed);
        opts.LogLevel.Should().Be(LogLevel.Debug);
        opts.EnableL2Guards.Should().BeFalse();
        opts.EnableL3Escalation.Should().BeTrue();
        opts.BlockThreshold.Should().Be(0.8);
        opts.FlagThreshold.Should().Be(0.5);
        opts.EscalationThreshold.Should().Be(0.3);
        opts.GuardTimeoutMs.Should().Be(10000);
        opts.EscalationTimeoutMs.Should().Be(3000);
    }
}

public class InputGuardOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new InputGuardOptions();

        opts.EnablePromptInjection.Should().BeTrue();
        opts.EnableJailbreak.Should().BeTrue();
        opts.EnableEncodingBypass.Should().BeTrue();
        opts.EnablePIIExposure.Should().BeTrue();
        opts.EnableRateLimit.Should().BeFalse();
        opts.EnableContentPolicy.Should().BeTrue();
        opts.MaxInputLength.Should().Be(128000);
        opts.EnableUnicodeNormalization.Should().BeTrue();
        opts.EnableHomoglyphDetection.Should().BeTrue();
        opts.EnableZeroWidthFiltering.Should().BeTrue();
    }

    [Fact]
    public void SupportedLanguages_Default_Has10Languages()
    {
        var opts = new InputGuardOptions();

        opts.SupportedLanguages.Should().HaveCount(10);
        opts.SupportedLanguages.Should().Contain("en");
        opts.SupportedLanguages.Should().Contain("ko");
        opts.SupportedLanguages.Should().Contain("ja");
        opts.SupportedLanguages.Should().Contain("zh");
        opts.SupportedLanguages.Should().Contain("es");
        opts.SupportedLanguages.Should().Contain("fr");
        opts.SupportedLanguages.Should().Contain("de");
        opts.SupportedLanguages.Should().Contain("pt");
        opts.SupportedLanguages.Should().Contain("ru");
        opts.SupportedLanguages.Should().Contain("ar");
    }

    [Fact]
    public void SupportedLanguages_IsSettable()
    {
        var opts = new InputGuardOptions
        {
            SupportedLanguages = ["en", "ko"]
        };

        opts.SupportedLanguages.Should().HaveCount(2);
    }
}

public class OutputGuardOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new OutputGuardOptions();

        opts.EnablePIILeakage.Should().BeTrue();
        opts.EnableFormatCompliance.Should().BeFalse();
        opts.EnableRefusal.Should().BeTrue();
        opts.EnableToxicity.Should().BeTrue();
        opts.MaxOutputLength.Should().Be(128000);
        opts.EnablePIIMasking.Should().BeFalse();
        opts.PIIMaskChar.Should().Be('*');
    }

    [Fact]
    public void PIIMaskChar_IsSettable()
    {
        var opts = new OutputGuardOptions { PIIMaskChar = '#' };

        opts.PIIMaskChar.Should().Be('#');
    }
}
