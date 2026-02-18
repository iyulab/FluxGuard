using FluentAssertions;
using FluxGuard.Remote.Agent;
using Xunit;

namespace FluxGuard.Remote.Tests.Agent;

public class AgentGrantTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var grant = new AgentGrant { Name = "file:read" };

        grant.AllowedActions.Should().BeEmpty();
        grant.AllowedResources.Should().BeEmpty();
        grant.RateLimitPerMinute.Should().Be(0);
        grant.IsTemporary.Should().BeFalse();
        grant.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var grant = new AgentGrant
        {
            Name = "web:fetch",
            AllowedActions = ["GET", "POST"],
            AllowedResources = ["*.example.com", "api.test.com/*"],
            RateLimitPerMinute = 60,
            IsTemporary = true,
            ExpiresAt = expires
        };

        grant.Name.Should().Be("web:fetch");
        grant.AllowedActions.Should().HaveCount(2);
        grant.AllowedResources.Should().HaveCount(2);
        grant.RateLimitPerMinute.Should().Be(60);
        grant.IsTemporary.Should().BeTrue();
        grant.ExpiresAt.Should().Be(expires);
    }
}

public class PermissionResultTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var result = new PermissionResult();

        result.IsPermitted.Should().BeFalse();
        result.DenialReason.Should().BeNull();
        result.MatchingPermission.Should().BeNull();
        result.RateLimitRemaining.Should().BeNull();
    }

    [Fact]
    public void Permitted_ShouldSetCorrectValues()
    {
        var grant = new AgentGrant { Name = "file:read" };
        var result = PermissionResult.Permitted(grant);

        result.IsPermitted.Should().BeTrue();
        result.MatchingPermission.Should().BeSameAs(grant);
        result.DenialReason.Should().BeNull();
    }

    [Fact]
    public void Denied_ShouldSetCorrectValues()
    {
        var result = PermissionResult.Denied("Rate limit exceeded");

        result.IsPermitted.Should().BeFalse();
        result.DenialReason.Should().Be("Rate limit exceeded");
        result.MatchingPermission.Should().BeNull();
    }
}
