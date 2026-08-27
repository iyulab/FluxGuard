using FluentAssertions;
using FluxGuard.Remote.Extensions;
using FluxGuard.Remote.MCP;
using FluxGuard.Remote.RAG;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FluxGuard.Remote.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFluxGuardRagSecurity_RegistersIRAGSecurityPipeline()
    {
        var services = new ServiceCollection();

        services.AddFluxGuardRagSecurity();
        using var provider = services.BuildServiceProvider();

        var pipeline = provider.GetService<IRAGSecurityPipeline>();
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<IndirectInjectionDetector>();
    }

    [Fact]
    public void AddFluxGuardMcpGuardrail_RegistersIMCPGuardrail()
    {
        var services = new ServiceCollection();

        services.AddFluxGuardMcpGuardrail();
        using var provider = services.BuildServiceProvider();

        var guardrail = provider.GetService<IMCPGuardrail>();
        guardrail.Should().NotBeNull();
        guardrail.Should().BeOfType<MCPToolValidator>();
    }

    [Fact]
    public void WithoutRegistration_IRAGSecurityPipeline_ResolvesToNull()
    {
        // Confirms the opt-in shape: a consumer that never calls AddFluxGuardRagSecurity()
        // gets no pipeline, not an exception — matches how FluxIndex/FluxFeed/ironhive-agent
        // are expected to treat these as optional dependencies (inject as nullable, skip the
        // guard check when absent).
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        provider.GetService<IRAGSecurityPipeline>().Should().BeNull();
        provider.GetService<IMCPGuardrail>().Should().BeNull();
    }
}
