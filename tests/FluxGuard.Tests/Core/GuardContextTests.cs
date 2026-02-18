using FluxGuard.Core;
using FluentAssertions;
using Xunit;

namespace FluxGuard.Tests.Core;

public class GuardContextTests
{
    [Fact]
    public void Constructor_RequestId_IsUniquePerInstance()
    {
        var ctx1 = new GuardContext { OriginalInput = "test1" };
        var ctx2 = new GuardContext { OriginalInput = "test2" };

        ctx1.RequestId.Should().NotBe(ctx2.RequestId);
    }

    [Fact]
    public void Constructor_RequestId_Is32CharHexString()
    {
        var ctx = new GuardContext { OriginalInput = "test" };

        // Guid.ToString("N") produces 32 hex chars
        ctx.RequestId.Should().HaveLength(32);
        ctx.RequestId.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void Constructor_Defaults_AreCorrect()
    {
        var ctx = new GuardContext { OriginalInput = "hello" };

        ctx.OriginalInput.Should().Be("hello");
        ctx.NormalizedInput.Should().BeEmpty();
        ctx.UserId.Should().BeNull();
        ctx.SessionId.Should().BeNull();
        ctx.ConversationHistory.Should().BeEmpty();
        ctx.Metadata.Should().BeEmpty();
        ctx.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void Timestamp_IsCloseToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var ctx = new GuardContext { OriginalInput = "test" };
        var after = DateTimeOffset.UtcNow;

        ctx.Timestamp.Should().BeOnOrAfter(before);
        ctx.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Init_OptionalProperties_CanBeSet()
    {
        using var cts = new CancellationTokenSource();
        var history = new List<string> { "msg1", "msg2" };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        var ctx = new GuardContext
        {
            OriginalInput = "test",
            UserId = "user-1",
            SessionId = "session-1",
            ConversationHistory = history,
            Metadata = metadata,
            CancellationToken = cts.Token
        };

        ctx.UserId.Should().Be("user-1");
        ctx.SessionId.Should().Be("session-1");
        ctx.ConversationHistory.Should().HaveCount(2);
        ctx.Metadata.Should().ContainKey("key");
        ctx.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void RequestId_CanBeOverriddenViaInit()
    {
        var ctx = new GuardContext
        {
            OriginalInput = "test",
            RequestId = "custom-id"
        };

        ctx.RequestId.Should().Be("custom-id");
    }
}
