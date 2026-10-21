using Migrations.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySqlConnector;

namespace Migrations.MySQL;

public class MySQLApplicationDbContextFactory : IDesignTimeDbContextFactory<MySQLApplicationDbContext>
{
    public MySQLApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = ConnectionStringResolver.GetConnectionString();

        // Keep design-time and runtime connection strings identical; Pomelo requires
        // these options when it shares an externally opened connection.
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true,
            UseAffectedRows = false
        };

        var optionsBuilder = new DbContextOptionsBuilder<MySQLApplicationDbContext>();
        optionsBuilder.UseMySql(builder.ConnectionString, ServerVersion.Parse("8.0.0-mysql"),
            b => b.MigrationsAssembly("Migrations.MySQL"));

        return new MySQLApplicationDbContext(optionsBuilder.Options);
    }
}
