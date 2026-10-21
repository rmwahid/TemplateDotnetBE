namespace Logger;

public interface IRestLogService
{
    Task ErrorLog(string path, string method, int statusCode, object error, DateTime requestTime);
    Task InfoLog(string path, string method, object? request, object? response, DateTime requestTime);
}
