using System.Reflection;
using Iyu.Conventions.Testing;
using Xunit;

namespace FluxGuard.Tests;

/// <summary>
/// Every public option in this library is read by the library. An option nothing reads is a promise it does not keep:
/// a caller sets it, and nothing changes and nothing is reported. The roster fails both ways - a new unread option,
/// and a listed one that has since been wired - so each change is recorded on purpose.
/// </summary>
public class OptionsReachabilityRosterTests
{
    // Every assembly this repository ships: an option declared in one and read in another only counts as read
    // when both are scanned.
    private static readonly Assembly[] Libraries =
    [
        Assembly.Load("FluxGuard"),
        Assembly.Load("FluxGuard.Remote"),
        Assembly.Load("FluxGuard.SDK"),
    ];

    /// <summary>
    /// Options accepted as unread today. Shrink this list; never grow it silently.
    /// <para>
    /// Opening baseline (2026-09-20): recorded as found, not as judged. A sample was checked by hand
    /// (<c>EnablePIIMasking</c>, <c>EnableToxicity</c>, <c>EnableContentPolicy</c>,
    /// <c>EnableRateLimit</c>, <c>GuardTimeoutMs</c>): each has its declaration as its only reference in src/
    /// (<c>GuardTimeoutMs</c> is also copied by the options clone, which is not a read). Several of them are
    /// switches on what this library guards, so "set and nothing happens" here means a protection the caller
    /// believes in is absent. Each entry leaves this list by being wired or by being removed.
    /// The core assembly's entries are gone (0.16.0): three were wired or implemented, the rest - switches for guards
    /// that do not exist - were removed.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownUnread = new()
    {
        ["FluxGuard.SDK.AI.ChatClient.FluxGuardChatClientOptions"] = ["ValidateStreamingOutput"],
        ["FluxGuard.SDK.AspNetCore.Middleware.FluxGuardMiddlewareOptions"] = ["MaxBodySize"],
    };

    [Fact]
    public void EveryPublicOption_IsRead() =>
        OptionsReachability.Scan(Libraries, OptionsTypes.NamedWith("Options", "Config"))
            .ShouldMatchRoster(KnownUnread);
}
