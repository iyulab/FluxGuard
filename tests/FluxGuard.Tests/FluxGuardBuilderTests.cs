using FluxGuard.Core;
using AwesomeAssertions;
using Xunit;

namespace FluxGuard.Tests;

public class FluxGuardBuilderTests
{
    [Fact]
    public void Create_WithDefaults_ReturnsFluxGuard()
    {
        // Act
        var guard = FluxGuardBuilder.Create().Build();

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithBuilder_AppliesConfiguration()
    {
        // Act
        var guard = FluxGuardBuilder.Create(builder => builder
            .WithPreset(GuardPreset.Strict)
            .WithFailMode(FailMode.Closed)
            .WithBlockThreshold(0.8)).Build();

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithMinimalPreset_ReturnsFluxGuard()
    {
        // Act
        var guard = FluxGuardBuilder.Create(builder => builder
            .WithPreset(GuardPreset.Minimal)).Build();

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckInputAsync_WithSafeInput_ReturnsPass()
    {
        // Arrange
        var guard = FluxGuardBuilder.Create().Build();

        // Act
        var result = await guard.CheckInputAsync("Hello, how are you today?", TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsBlocked.Should().BeFalse();
    }
}
