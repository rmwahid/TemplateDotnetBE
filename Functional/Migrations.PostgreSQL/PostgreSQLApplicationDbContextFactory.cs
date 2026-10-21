using Migrations.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Migrations.PostgreSQL;

public class PostgreSQLApplicationDbContextFactory : IDesignTimeDbContextFactory<PostgreSQLApplicationDbContext>
{
    public PostgreSQLApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = ConnectionStringResolver.GetConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<PostgreSQLApplicationDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            b => b.MigrationsAssembly("Migrations.PostgreSQL"));

        return new PostgreSQLApplicationDbContext(optionsBuilder.Options);
    }
}
