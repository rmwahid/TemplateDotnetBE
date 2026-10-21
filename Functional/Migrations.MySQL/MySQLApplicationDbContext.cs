using Migrations.Core;
using Microsoft.EntityFrameworkCore;

namespace Migrations.MySQL;

public class MySQLApplicationDbContext : ApplicationDbContext
{
    public MySQLApplicationDbContext(DbContextOptions<MySQLApplicationDbContext> options) : base(options)
    {
    }
}
