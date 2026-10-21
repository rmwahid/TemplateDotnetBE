using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Data.Common;

namespace Infrastructure.Database;

public class MySQLConnectionProvider : IConnectionProvider
{
    private readonly DatabaseConfig _config;

    public MySQLConnectionProvider(IOptions<DatabaseConfig> config)
    {
        _config = config.Value;
    }

    public DbConnection CreateConnection()
    {
        return new MySqlConnection(
            MySQLConnectionString.Normalize(_config.ConnectionString));
    }
}
