using System.Globalization;
using System.Text.Json;
using FluxGuard.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FluxGuard.SDK.AspNetCore.Middleware;

/// <summary>
/// FluxGuard middleware for ASP.NET Core
/// Protects LLM API endpoints by validating incoming requests
/// </summary>
public sealed partial class FluxGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IFluxGuard _guard;
    private readonly FluxGuardMiddlewareOptions _options;
    private readonly ILogger<FluxGuardMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Create FluxGuard middleware
    /// </summary>
    public FluxGuardMiddleware(
        RequestDelegate next,
        IFluxGuard guard,
        IOptions<FluxGuardMiddlewareOptions> options,
        ILogger<FluxGuardMiddleware> logger)
    {
        _next = next;
        _guard = guard;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Process request
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Check if path should be protected
        if (!ShouldProtect(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Only check POST/PUT/PATCH requests with body
        if (!HasBody(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // A body over the limit is refused, not passed on unchecked: skipping the check would let padding carry
        // anything past the guard.
        var maxBodySize = _options.MaxBodySize;
        if (maxBodySize > 0 && context.Request.ContentLength > maxBodySize)
        {
            await WritePayloadTooLargeAsync(context, maxBodySize);
            return;
        }

        // Enable buffering and read body
        context.Request.EnableBuffering();
        var body = await ReadBodyAsync(context.Request, maxBodySize, context.RequestAborted);
        if (body is null)
        {
            await WritePayloadTooLargeAsync(context, maxBodySize);
            return;
        }

        if (string.IsNullOrEmpty(body))
        {
            await _next(context);
            return;
        }

        // Extract text content from body
        var textContent = ExtractTextContent(body);
        if (string.IsNullOrEmpty(textContent))
        {
            await _next(context);
            return;
        }

        // Check input
        var result = await _guard.CheckInputAsync(textContent, context.RequestAborted);

        if (result.IsBlocked)
        {
            LogRequestBlocked(_logger, result.BlockReason, context.Request.Path, result.Score);

            await WriteBlockedResponseAsync(context, result);
            return;
        }

        if (result.IsFlagged)
        {
            LogRequestFlagged(_logger, result.Score, context.Request.Path);

            // Add header to indicate flagged request
            context.Response.Headers["X-FluxGuard-Flagged"] = "true";
            context.Response.Headers["X-FluxGuard-Score"] =
                result.Score.ToString("F2", CultureInfo.InvariantCulture);
        }

        // Reset request body stream position
        context.Request.Body.Position = 0;

        await _next(context);
    }

    private bool ShouldProtect(PathString path)
    {
        if (_options.ProtectedPaths.Count == 0)
        {
            return true; // Protect all paths if none specified
        }

        foreach (var protectedPath in _options.ProtectedPaths)
        {
            if (path.StartsWithSegments(protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBody(string method)
    {
        return method is "POST" or "PUT" or "PATCH";
    }

    /// <summary>Reads the body as UTF-8 text; null when it is longer than <paramref name="maxBodySize"/> bytes (0 = no limit).</summary>
    private static async Task<string?> ReadBodyAsync(HttpRequest request, int maxBodySize, CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (maxBodySize > 0 && bytes.Length + read > maxBodySize)
            {
                return null;
            }

            bytes.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
    }

    private async Task WritePayloadTooLargeAsync(HttpContext context, int maxBodySize)
    {
        LogBodyTooLarge(_logger, context.Request.Path, maxBodySize);
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsync("Request body exceeds the size this endpoint checks.");
    }

    private string? ExtractTextContent(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Try common LLM API field names
            if (root.TryGetProperty(_options.InputFieldName, out var inputField))
            {
                return inputField.GetString();
            }

            if (root.TryGetProperty("prompt", out var promptField))
            {
                return promptField.GetString();
            }

            if (root.TryGetProperty("messages", out var messagesField) &&
                messagesField.ValueKind == JsonValueKind.Array)
            {
                // Extract last user message
                foreach (var message in messagesField.EnumerateArray().Reverse())
                {
                    if (message.TryGetProperty("role", out var role) &&
                        role.GetString() == "user" &&
                        message.TryGetProperty("content", out var content))
                    {
                        return content.GetString();
                    }
                }
            }

            return null;
        }
        catch (JsonException)
        {
            // Not JSON - treat body as raw text
            return body;
        }
    }

    private async Task WriteBlockedResponseAsync(HttpContext context, GuardResult result)
    {
        context.Response.StatusCode = _options.BlockedStatusCode;
        context.Response.ContentType = "application/json";

        var response = new BlockedResponse
        {
            Error = "Request blocked by security guard",
            Code = "GUARD_BLOCKED",
            RequestId = result.RequestId
        };

        if (_options.IncludeDetailsInResponse)
        {
            response.Details = result.BlockReason;
            response.Score = result.Score;
        }

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions));
    }

    private sealed class BlockedResponse
    {
        public required string Error { get; init; }
        public required string Code { get; init; }
        public required string RequestId { get; init; }
        public string? Details { get; set; }
        public double? Score { get; set; }
    }

    [LoggerMessage(LogLevel.Warning, "Request blocked by FluxGuard: {Reason}, Path: {Path}, Score: {Score}")]
    private static partial void LogRequestBlocked(ILogger logger, string? reason, PathString path, double score);

    [LoggerMessage(LogLevel.Information, "Request flagged by FluxGuard: Score: {Score}, Path: {Path}")]
    private static partial void LogRequestFlagged(ILogger logger, double score, PathString path);

    [LoggerMessage(LogLevel.Warning, "Request body over the {MaxBodySize}-byte limit refused, Path: {Path}")]
    private static partial void LogBodyTooLarge(ILogger logger, PathString path, int maxBodySize);
}
