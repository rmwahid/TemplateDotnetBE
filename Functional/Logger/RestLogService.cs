namespace Logger;

public class RestLogService : IRestLogService
{
    public Task ErrorLog(string path, string method, int statusCode, object error, DateTime requestTime)
    {
        // Default implementation: log to console. Replace with your real logging provider.
        Console.WriteLine($"[{requestTime:O}] ERROR {method} {path} -> {statusCode}: {System.Text.Json.JsonSerializer.Serialize(error)}");
        return Task.CompletedTask;
    }

    public Task InfoLog(string path, string method, object? request, object? response, DateTime requestTime)
    {
        // Default implementation: log to console.
        Console.WriteLine($"[{requestTime:O}] INFO {method} {path}");
        return Task.CompletedTask;
    }
}
