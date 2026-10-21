using Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Service.Common;
using System.Security.Claims;
using System.Text;

namespace API.Services;

/// <summary>
/// Reads the Idempotency-Key header for POST requests, fingerprints the request, and
/// makes the values available to the unit-of-work executor. The executor owns the
/// storage and replay; this middleware only translates HTTP into that context.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 255;

    private readonly RequestDelegate _next;
    private readonly IIdempotencyRequestHasher _hasher;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(
        RequestDelegate next,
        IIdempotencyRequestHasher hasher,
        ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, IIdempotencyContext context)
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method) ||
            !httpContext.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            await _next(httpContext);
            return;
        }

        var key = values.ToString().Trim();
        if (string.IsNullOrEmpty(key))
        {
            await _next(httpContext);
            return;
        }

        if (key.Length > MaxKeyLength)
        {
            throw new ArgumentException(
                $"Idempotency-Key must be at most {MaxKeyLength} characters.");
        }

        // The body is read for its fingerprint only. It is never logged and the
        // stream is rewound so model binding sees the same content.
        httpContext.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(
                   httpContext.Request.Body,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(httpContext.RequestAborted);
        }
        httpContext.Request.Body.Position = 0;

        var ownerScope = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var path = httpContext.Request.Path.Value ?? string.Empty;

        context.Set(
            key,
            httpContext.Request.Method,
            path,
            ownerScope,
            _hasher.Hash(httpContext.Request.Method, path, body));
        _logger.LogDebug(
            "Idempotency key present for {Method} {Path}",
            httpContext.Request.Method,
            path);

        await _next(httpContext);
    }
}
