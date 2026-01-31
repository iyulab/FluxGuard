using FluxGuard.Core;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests;

public class FluxGuardBuilderTests
{
    [Fact]
    public void Create_WithDefaults_ReturnsFluxGuard()
    {
        // Act
        var guard = FluxGuard.Create();

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithBuilder_AppliesConfiguration()
    {
        // Act
        var guard = FluxGuard.Create(builder => builder
            .WithPreset(GuardPreset.Strict)
            .WithFailMode(FailMode.Closed)
            .WithBlockThreshold(0.8));

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithMinimalPreset_ReturnsFluxGuard()
    {
        // Act
        var guard = FluxGuard.Create(builder => builder
            .WithPreset(GuardPreset.Minimal)
            .DisableL2Guards());

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckInputAsync_WithSafeInput_ReturnsPass()
    {
        // Arrange
        var guard = FluxGuard.Create();

        // Act
        var result = await guard.CheckInputAsync("Hello, how are you today?");

        // Assert
        result.Should().NotBeNull();
        result.IsBlocked.Should().BeFalse();
    }
}
