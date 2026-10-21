using Infrastructure.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Services;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IConnectionProvider _connectionProvider;

    public DatabaseHealthCheck(IConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionProvider.CreateConnection();
            connection.Open();
            return HealthCheckResult.Healthy("Database connection is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
