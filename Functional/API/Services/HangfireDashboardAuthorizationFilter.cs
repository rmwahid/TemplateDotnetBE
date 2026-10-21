using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace API.Services;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In development, allow all. In production, implement proper authentication.
        var httpContext = context.GetHttpContext();
        return httpContext.Request.IsLocal() || httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
    }
}

public static class HangfireRequestExtensions
{
    public static bool IsLocal(this HttpRequest request)
    {
        var connection = request.HttpContext.Connection;
        if (connection.RemoteIpAddress is not null && connection.LocalIpAddress is not null)
        {
            return connection.RemoteIpAddress.Equals(connection.LocalIpAddress);
        }

        return connection.RemoteIpAddress is null;
    }
}
