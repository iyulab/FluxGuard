using FluentAssertions;
using FluxGuard.Remote.MCP;
using Xunit;

namespace FluxGuard.Remote.Tests.MCP;

public class MCPToolRequestTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var request = new MCPToolRequest
        {
            ServerName = "filesystem",
            ToolName = "read_file"
        };

        request.Arguments.Should().BeNull();
        request.SessionId.Should().BeNull();
        request.Metadata.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var args = new Dictionary<string, object> { ["path"] = "/tmp/test.txt" };
        var metadata = new Dictionary<string, object> { ["user"] = "admin" };
        var request = new MCPToolRequest
        {
            ServerName = "filesystem",
            ToolName = "read_file",
            Arguments = args,
            SessionId = "session-123",
            Metadata = metadata
        };

        request.ServerName.Should().Be("filesystem");
        request.ToolName.Should().Be("read_file");
        request.Arguments.Should().ContainKey("path");
        request.SessionId.Should().Be("session-123");
    }
}

public class MCPValidationResultTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var result = new MCPValidationResult();

        result.IsValid.Should().BeFalse();
        result.ShouldBlock.Should().BeFalse();
        result.Reason.Should().BeNull();
        result.RiskScore.Should().Be(0);
        result.Issues.Should().BeEmpty();
        result.SanitizedResult.Should().BeNull();
    }

    [Fact]
    public void Valid_ShouldSetCorrectValues()
    {
        var result = MCPValidationResult.Valid();

        result.IsValid.Should().BeTrue();
        result.ShouldBlock.Should().BeFalse();
        result.RiskScore.Should().Be(0.0);
    }

    [Fact]
    public void Block_ShouldSetCorrectValues()
    {
        var result = MCPValidationResult.Block("Unknown server detected", 0.9);

        result.IsValid.Should().BeFalse();
        result.ShouldBlock.Should().BeTrue();
        result.Reason.Should().Be("Unknown server detected");
        result.RiskScore.Should().Be(0.9);
    }

    [Fact]
    public void Block_DefaultRiskScore_ShouldBeOne()
    {
        var result = MCPValidationResult.Block("Blocked");

        result.RiskScore.Should().Be(1.0);
    }
}

public class MCPIssueTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var issue = new MCPIssue();

        issue.Type.Should().Be(MCPIssueType.UnknownServer);
        issue.Description.Should().BeNull();
        issue.Severity.Should().Be(MCPIssueSeverity.Low);
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var issue = new MCPIssue
        {
            Type = MCPIssueType.PromptInjection,
            Description = "Injection detected in tool result",
            Severity = MCPIssueSeverity.Critical
        };

        issue.Type.Should().Be(MCPIssueType.PromptInjection);
        issue.Severity.Should().Be(MCPIssueSeverity.Critical);
    }
}

public class MCPIssueTypeEnumTests
{
    [Fact]
    public void ShouldHaveSevenValues()
    {
        Enum.GetValues<MCPIssueType>().Should().HaveCount(7);
    }

    [Theory]
    [InlineData(MCPIssueType.UnknownServer, 0)]
    [InlineData(MCPIssueType.UnknownTool, 1)]
    [InlineData(MCPIssueType.InvalidArguments, 2)]
    [InlineData(MCPIssueType.PermissionDenied, 3)]
    [InlineData(MCPIssueType.RateLimitExceeded, 4)]
    [InlineData(MCPIssueType.SensitiveData, 5)]
    [InlineData(MCPIssueType.PromptInjection, 6)]
    public void ShouldHaveExpectedIntValues(MCPIssueType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

public class MCPIssueSeverityEnumTests
{
    [Fact]
    public void ShouldHaveFourValues()
    {
        Enum.GetValues<MCPIssueSeverity>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(MCPIssueSeverity.Low, 0)]
    [InlineData(MCPIssueSeverity.Medium, 1)]
    [InlineData(MCPIssueSeverity.High, 2)]
    [InlineData(MCPIssueSeverity.Critical, 3)]
    public void ShouldHaveExpectedIntValues(MCPIssueSeverity severity, int expected)
    {
        ((int)severity).Should().Be(expected);
    }
}

public class MCPServerInfoTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var info = new MCPServerInfo { Name = "test-server" };

        info.Type.Should().Be("stdio");
        info.AllowedTools.Should().BeEmpty();
        info.IsTrusted.Should().BeFalse();
        info.MaxConcurrentCalls.Should().Be(10);
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var info = new MCPServerInfo
        {
            Name = "filesystem",
            Type = "http",
            AllowedTools = ["read_file", "list_directory"],
            IsTrusted = true,
            MaxConcurrentCalls = 5
        };

        info.Name.Should().Be("filesystem");
        info.Type.Should().Be("http");
        info.AllowedTools.Should().HaveCount(2);
        info.IsTrusted.Should().BeTrue();
        info.MaxConcurrentCalls.Should().Be(5);
    }
}
