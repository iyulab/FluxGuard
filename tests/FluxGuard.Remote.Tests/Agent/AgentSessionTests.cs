using FluentAssertions;
using FluxGuard.Remote.Agent;
using Xunit;

namespace FluxGuard.Remote.Tests.Agent;

public class AgentSessionTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var session = new AgentSession { AgentName = "test-agent" };

        session.SessionId.Should().NotBeNullOrEmpty();
        session.AgentName.Should().Be("test-agent");
        session.UserId.Should().BeNull();
        session.Timeout.Should().Be(TimeSpan.FromHours(1));
        session.ExecutionDepth.Should().Be(0);
        session.MaxExecutionDepth.Should().Be(10);
        session.TotalToolCalls.Should().Be(0);
        session.MaxToolCalls.Should().Be(100);
        session.Metadata.Should().BeEmpty();
        session.ToolCallHistory.Should().BeEmpty();
    }

    [Fact]
    public void IsExpired_ShouldBeFalse_WhenNew()
    {
        var session = new AgentSession { AgentName = "agent" };
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldBeTrue_WhenZeroTimeout()
    {
        var session = new AgentSession
        {
            AgentName = "agent",
            Timeout = TimeSpan.Zero
        };
        session.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void RecordToolCall_ShouldIncrementCount()
    {
        var session = new AgentSession { AgentName = "agent" };

        session.RecordToolCall("read_file", true);
        session.RecordToolCall("write_file", false, "Permission denied");

        session.TotalToolCalls.Should().Be(2);
        session.ToolCallHistory.Should().HaveCount(2);
        session.ToolCallHistory[0].ToolName.Should().Be("read_file");
        session.ToolCallHistory[0].Allowed.Should().BeTrue();
        session.ToolCallHistory[1].Allowed.Should().BeFalse();
        session.ToolCallHistory[1].Details.Should().Be("Permission denied");
    }

    [Fact]
    public void EnterNestedExecution_ShouldIncrementDepth()
    {
        var session = new AgentSession { AgentName = "agent", MaxExecutionDepth = 3 };

        session.EnterNestedExecution().Should().BeTrue();
        session.ExecutionDepth.Should().Be(1);

        session.EnterNestedExecution().Should().BeTrue();
        session.ExecutionDepth.Should().Be(2);

        session.EnterNestedExecution().Should().BeTrue();
        session.ExecutionDepth.Should().Be(3);

        // At max depth
        session.EnterNestedExecution().Should().BeFalse();
        session.ExecutionDepth.Should().Be(3);
    }

    [Fact]
    public void ExitNestedExecution_ShouldDecrementDepth()
    {
        var session = new AgentSession { AgentName = "agent" };
        session.EnterNestedExecution();
        session.EnterNestedExecution();

        session.ExitNestedExecution();
        session.ExecutionDepth.Should().Be(1);

        session.ExitNestedExecution();
        session.ExecutionDepth.Should().Be(0);

        // Should not go below 0
        session.ExitNestedExecution();
        session.ExecutionDepth.Should().Be(0);
    }

    [Fact]
    public void CanMakeToolCall_ShouldBeTrue_WhenUnderLimit()
    {
        var session = new AgentSession { AgentName = "agent", MaxToolCalls = 2 };

        session.CanMakeToolCall.Should().BeTrue();

        session.RecordToolCall("tool1", true);
        session.CanMakeToolCall.Should().BeTrue();

        session.RecordToolCall("tool2", true);
        session.CanMakeToolCall.Should().BeFalse();
    }

    [Fact]
    public void CanMakeToolCall_ShouldBeFalse_WhenExpired()
    {
        var session = new AgentSession
        {
            AgentName = "agent",
            Timeout = TimeSpan.Zero
        };

        session.CanMakeToolCall.Should().BeFalse();
    }

    [Fact]
    public void SessionId_ShouldBeUnique()
    {
        var session1 = new AgentSession { AgentName = "agent" };
        var session2 = new AgentSession { AgentName = "agent" };

        session1.SessionId.Should().NotBe(session2.SessionId);
    }
}

public class ToolCallRecordTests
{
    [Fact]
    public void ShouldInitialize_WithRequiredFields()
    {
        var record = new ToolCallRecord
        {
            ToolName = "read_file",
            Allowed = true,
            Timestamp = DateTimeOffset.UtcNow
        };

        record.ToolName.Should().Be("read_file");
        record.Allowed.Should().BeTrue();
        record.Details.Should().BeNull();
    }

    [Fact]
    public void ShouldInitialize_WithAllFields()
    {
        var now = DateTimeOffset.UtcNow;
        var record = new ToolCallRecord
        {
            ToolName = "write_file",
            Allowed = false,
            Details = "Blocked by policy",
            Timestamp = now
        };

        record.Details.Should().Be("Blocked by policy");
        record.Timestamp.Should().Be(now);
    }
}
