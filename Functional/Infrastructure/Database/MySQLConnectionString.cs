using MySqlConnector;

namespace Infrastructure.Database;

public static class MySQLConnectionString
{
    /// <summary>
    /// Pomelo requires these options whenever EF shares an externally opened
    /// connection. Normalising here keeps the requirement out of each caller's
    /// configuration and prevents Pomelo from mutating an already-open connection.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true,
            UseAffectedRows = false
        };

        return builder.ConnectionString;
    }
}
