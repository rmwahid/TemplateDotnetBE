using API.Common.Exceptions;
using API.Common.Response;
using Infrastructure.Exceptions;
using Logger;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace API.Services;

/// <summary>
/// Turns an escaping exception into a response, and records it exactly once.
///
/// This is the only place an unhandled exception is logged, which is deliberate: a
/// service that catches Exception to build a result object loses the stack trace and
/// makes a database outage indistinguishable from a validation failure.
/// </summary>
public class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRestLogService _restLogService;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;

    public ErrorHandlerMiddleware(
        RequestDelegate next,
        IRestLogService restLogService,
        ILogger<ErrorHandlerMiddleware> logger)
    {
        _next = next;
        _restLogService = restLogService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected. This is not an application error, so it is not
            // logged at Error and there is no response body to write.
            httpContext.Response.StatusCode = 499; // client closed request
        }
        catch (Exception e)
        {
            var requestTime = DateTime.Now;
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            var (status, message, details) = Classify(e);
            httpContext.Response.StatusCode = status;

            // The stack trace lives here and nowhere else. Client errors are logged at
            // Warning; only an unexpected failure is an Error.
            if (status >= 500)
            {
                _logger.LogError(e,
                    "Unhandled exception on {Method} {Path} -> {Status} (trace {TraceId})",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    status,
                    traceId);
            }
            else
            {
                _logger.LogWarning(e,
                    "Request failed on {Method} {Path} -> {Status} (trace {TraceId})",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    status,
                    traceId);
            }

            var error = new ErrorResponse
            {
                Code = status,
                Message = message,
                Details = details
            };

            httpContext.Response.ContentType = "application/json";

            if (status >= 500)
            {
                await _restLogService.ErrorLog(
                    $"{httpContext.Request.Host}{httpContext.Request.Path}",
                    httpContext.Request.Method,
                    httpContext.Response.StatusCode,
                    error,
                    requestTime);
            }
            else
            {
                await _restLogService.InfoLog(
                    $"{httpContext.Request.Host}{httpContext.Request.Path}",
                    httpContext.Request.Method,
                    null,
                    error,
                    requestTime);
            }

            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
    }

    /// <summary>
    /// Maps an exception to a status, a client-safe message, and the detail the
    /// caller is allowed to see. Unexpected failures never echo their message back:
    /// it can name tables, queries, or file paths.
    /// </summary>
    private static (int Status, string Message, string? Details) Classify(Exception e) => e switch
    {
        ValidationException or ArgumentException or ArgumentNullException =>
            ((int)HttpStatusCode.BadRequest, "Invalid request parameters", e.Message),

        NotFoundException =>
            ((int)HttpStatusCode.NotFound, "Resource not found", e.Message),

        ResponseException =>
            ((int)HttpStatusCode.BadRequest, "No valid response found", e.Message),

        ConcurrencyConflictException =>
            ((int)HttpStatusCode.Conflict,
                "The resource was modified by another operation.",
                null),

        UniqueConstraintException =>
            ((int)HttpStatusCode.Conflict,
                "The resource conflicts with an existing record.",
                null),

        IdempotencyConflictException =>
            ((int)HttpStatusCode.Conflict,
                "The idempotency key was reused with a different request.",
                null),

        _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred", null)
    };
}
