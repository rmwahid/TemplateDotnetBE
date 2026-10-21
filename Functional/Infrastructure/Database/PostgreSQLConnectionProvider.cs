using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;

namespace Infrastructure.Database;

public class PostgreSQLConnectionProvider : IConnectionProvider
{
    private readonly DatabaseConfig _config;

    public PostgreSQLConnectionProvider(IOptions<DatabaseConfig> config)
    {
        _config = config.Value;
    }

    public DbConnection CreateConnection()
    {
        return new NpgsqlConnection(_config.ConnectionString);
    }
}
