using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Migrations.Core;

namespace UnitOfWork;

/// <summary>
/// Runs the whole begin/execute/commit sequence through the DbContext execution
/// strategy. With retry disabled this is a plain invocation; when the provider is
/// configured with <c>EnableRetryOnFailure</c>, the complete transaction is retried
/// instead of a single command, which is what EF Core requires for user transactions.
/// </summary>
public sealed class DbContextUnitOfWorkExecutionStrategy : IUnitOfWorkExecutionStrategy
{
    private readonly ApplicationDbContext _db;

    public DbContextUnitOfWorkExecutionStrategy(ApplicationDbContext db) => _db = db;

    public Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(_ => operation(), cancellationToken);
    }

    public Task<T> ExecuteNoRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return operation();
    }
}
