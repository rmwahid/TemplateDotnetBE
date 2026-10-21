using Migrations.Core;
using Microsoft.EntityFrameworkCore;

namespace Migrations.PostgreSQL;

public class PostgreSQLApplicationDbContext : ApplicationDbContext
{
    public PostgreSQLApplicationDbContext(DbContextOptions<PostgreSQLApplicationDbContext> options) : base(options)
    {
    }
}
