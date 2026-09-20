using System.Text;
using AwesomeAssertions;
using FluxGuard.Core;
using FluxGuard.SDK.AspNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace FluxGuard.SDK.Tests;

/// <summary>
/// <c>MaxBodySize</c> was read by nothing: the middleware buffered and read a body of any size. A body over the limit
/// is refused (413) rather than passed on unchecked - skipping the check would let padding carry anything past the guard.
/// </summary>
public class FluxGuardMiddlewareBodySizeTests
{
    private static (FluxGuardMiddleware middleware, IFluxGuard guard, Func<bool> nextCalled) Create(int maxBodySize)
    {
        var guard = Substitute.For<IFluxGuard>();
        guard.CheckInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(GuardResult.Pass("r", 0));
        var called = false;
        var middleware = new FluxGuardMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            guard,
            Options.Create(new FluxGuardMiddlewareOptions { MaxBodySize = maxBodySize }),
            NullLogger<FluxGuardMiddleware>.Instance);
        return (middleware, guard, () => called);
    }

    private static DefaultHttpContext Post(string json, bool withContentLength)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/chat";
        context.Request.Body = new MemoryStream(bytes);
        context.Response.Body = new MemoryStream();
        if (withContentLength)
        {
            context.Request.ContentLength = bytes.Length;
        }

        return context;
    }

    private static string Body(int inputLength) => "{\"input\":\"" + new string('a', inputLength) + "\"}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // chunked: no Content-Length to trust, the limit has to hold while reading
    public async Task ABodyOverTheLimit_IsRefusedWith413_AndNeitherCheckedNorPassedOn(bool withContentLength)
    {
        var (middleware, guard, nextCalled) = Create(maxBodySize: 64);
        var context = Post(Body(200), withContentLength);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        nextCalled().Should().BeFalse();
        await guard.DidNotReceive().CheckInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABodyWithinTheLimit_IsCheckedAndPassedOn()
    {
        var (middleware, guard, nextCalled) = Create(maxBodySize: 64);
        var context = Post(Body(10), withContentLength: true);

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
        await guard.Received(1).CheckInputAsync(new string('a', 10), Arg.Any<CancellationToken>());
        context.Request.Body.Position.Should().Be(0);
    }

    [Fact]
    public async Task ZeroMeansNoLimit()
    {
        var (middleware, _, nextCalled) = Create(maxBodySize: 0);
        var context = Post(Body(5000), withContentLength: true);

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
    }
}
