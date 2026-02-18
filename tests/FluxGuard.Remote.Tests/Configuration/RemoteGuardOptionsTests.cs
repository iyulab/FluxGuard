using FluentAssertions;
using FluxGuard.Remote.Configuration;
using Xunit;

namespace FluxGuard.Remote.Tests.Configuration;

public class RemoteGuardOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var options = new RemoteGuardOptions();

        options.Enabled.Should().BeTrue();
        options.TimeoutMs.Should().Be(5000);
        options.MaxRetries.Should().Be(1);
        options.EnableCache.Should().BeTrue();
        options.CacheTtlSeconds.Should().Be(3600);
        options.MaxCacheEntries.Should().Be(10000);
        options.Judge.Should().NotBeNull();
        options.OpenAI.Should().NotBeNull();
    }

    [Fact]
    public void SectionName_ShouldBeCorrect()
    {
        RemoteGuardOptions.SectionName.Should().Be("FluxGuard:Remote");
    }

    [Fact]
    public void ShouldOverride_AllDefaults()
    {
        var options = new RemoteGuardOptions
        {
            Enabled = false,
            TimeoutMs = 10000,
            MaxRetries = 3,
            EnableCache = false,
            CacheTtlSeconds = 7200,
            MaxCacheEntries = 5000
        };

        options.Enabled.Should().BeFalse();
        options.TimeoutMs.Should().Be(10000);
        options.MaxRetries.Should().Be(3);
        options.EnableCache.Should().BeFalse();
        options.CacheTtlSeconds.Should().Be(7200);
        options.MaxCacheEntries.Should().Be(5000);
    }
}

public class LLMJudgeOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var options = new LLMJudgeOptions();

        options.Model.Should().Be("gpt-4o-mini");
        options.Temperature.Should().Be(0.0);
        options.MaxTokens.Should().Be(256);
        options.BlockThreshold.Should().Be(0.8);
        options.FlagThreshold.Should().Be(0.5);
    }

    [Fact]
    public void ShouldOverride_Defaults()
    {
        var options = new LLMJudgeOptions
        {
            Model = "gpt-4-turbo",
            Temperature = 0.3,
            MaxTokens = 512,
            BlockThreshold = 0.9,
            FlagThreshold = 0.6
        };

        options.Model.Should().Be("gpt-4-turbo");
        options.Temperature.Should().Be(0.3);
        options.MaxTokens.Should().Be(512);
        options.BlockThreshold.Should().Be(0.9);
        options.FlagThreshold.Should().Be(0.6);
    }
}

public class OpenAIProviderOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var options = new OpenAIProviderOptions();

        options.ApiKey.Should().BeNull();
        options.OrganizationId.Should().BeNull();
        options.BaseUrl.Should().BeNull();
        options.DeploymentName.Should().BeNull();
        options.ApiVersion.Should().BeNull();
        options.UseAzure.Should().BeFalse();
    }

    [Fact]
    public void ShouldInitialize_AzureConfig()
    {
        var options = new OpenAIProviderOptions
        {
            ApiKey = "sk-test-key",
            BaseUrl = new Uri("https://myinstance.openai.azure.com"),
            DeploymentName = "gpt-4o",
            ApiVersion = "2024-02-01",
            UseAzure = true
        };

        options.UseAzure.Should().BeTrue();
        options.DeploymentName.Should().Be("gpt-4o");
        options.BaseUrl!.Host.Should().Contain("azure.com");
    }
}
