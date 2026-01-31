using FluentAssertions;
using FluxGuard.Remote.Abstractions;
using FluxGuard.Remote.Configuration;
using FluxGuard.Remote.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FluxGuard.Remote.Tests.Providers;

public sealed class OpenAICompletionServiceTests : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public void Name_OpenAI_ReturnsOpenAI()
    {
        // Arrange
        var options = new OpenAIProviderOptions { ApiKey = "test-key" };
        using var service = new OpenAICompletionService(
            options,
            _httpClient,
            NullLogger<OpenAICompletionService>.Instance);

        // Assert
        service.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void Name_AzureOpenAI_ReturnsAzureOpenAI()
    {
        // Arrange
        var options = new OpenAIProviderOptions
        {
            ApiKey = "test-key",
            UseAzure = true,
            BaseUrl = new Uri("https://test.openai.azure.com")
        };
        using var service = new OpenAICompletionService(
            options,
            _httpClient,
            NullLogger<OpenAICompletionService>.Instance);

        // Assert
        service.Name.Should().Be("Azure OpenAI");
    }

    [Fact]
    public void IsAvailable_WithApiKey_ReturnsTrue()
    {
        // Arrange
        var options = new OpenAIProviderOptions { ApiKey = "test-key" };
        using var service = new OpenAICompletionService(
            options,
            _httpClient,
            NullLogger<OpenAICompletionService>.Instance);

        // Assert
        service.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WithoutApiKey_ReturnsFalse()
    {
        // Arrange
        var options = new OpenAIProviderOptions { ApiKey = null };
        using var service = new OpenAICompletionService(
            options,
            _httpClient,
            NullLogger<OpenAICompletionService>.Instance);

        // Assert
        service.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_WithoutApiKey_ReturnsError()
    {
        // Arrange
        var options = new OpenAIProviderOptions { ApiKey = null };
        using var service = new OpenAICompletionService(
            options,
            _httpClient,
            NullLogger<OpenAICompletionService>.Instance);

        var request = new CompletionRequest { UserPrompt = "Hello" };

        // Act
        var result = await service.CompleteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not configured");
    }
}
