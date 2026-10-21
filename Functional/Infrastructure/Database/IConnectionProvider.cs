using System.Data.Common;

namespace Infrastructure.Database;

/// <summary>
/// Creates connections. The return type is <see cref="DbConnection"/> rather than
/// <see cref="System.Data.IDbConnection"/> because EF Core's transaction enlistment
/// works in <see cref="DbTransaction"/> terms; Dapper accepts DbConnection unchanged.
/// </summary>
public interface IConnectionProvider
{
    DbConnection CreateConnection();
}
