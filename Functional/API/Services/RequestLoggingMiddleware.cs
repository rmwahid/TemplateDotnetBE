using Logger;
using System.Diagnostics;
using System.Text;

namespace API.Services;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRestLogService _restLogService;

    public RequestLoggingMiddleware(RequestDelegate next, IRestLogService restLogService)
    {
        _next = next;
        _restLogService = restLogService;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var requestTime = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        var request = await ReadRequestBodyAsync(httpContext.Request);

        var originalResponseBody = httpContext.Response.Body;
        using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        try
        {
            await _next(httpContext);

            responseBody.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalResponseBody);

            stopwatch.Stop();

            if (ShouldLog(httpContext.Request.Path))
            {
                await _restLogService.InfoLog(
                    $"{httpContext.Request.Host}{httpContext.Request.Path}",
                    httpContext.Request.Method,
                    request,
                    new { StatusCode = httpContext.Response.StatusCode, Body = responseText, DurationMs = stopwatch.ElapsedMilliseconds },
                    requestTime);
            }
        }
        catch (Exception)
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalResponseBody);
            throw;
        }
        finally
        {
            httpContext.Response.Body = originalResponseBody;
        }
    }

    private static async Task<object> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return new
        {
            request.Method,
            request.Path,
            request.QueryString,
            Body = string.IsNullOrWhiteSpace(body) ? null : body
        };
    }

    private static bool ShouldLog(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;
        return !pathValue.Contains("/health", StringComparison.OrdinalIgnoreCase)
            && !pathValue.Contains("/hangfire", StringComparison.OrdinalIgnoreCase);
    }
}
