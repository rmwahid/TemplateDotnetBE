using System.Data.Common;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Migrations.Core;

namespace UnitOfWork;

/// <summary>
/// One connection, optionally one transaction, owned by a single use case.
///
/// Lives in its own project rather than in Infrastructure because it needs the
/// <see cref="ApplicationDbContext"/>, and through it Entities. Keeping it out of
/// Infrastructure is what stops Entities reaching Service by way of a project
/// reference. The interfaces it implements stay in Infrastructure for the same reason:
/// Service depends on those, and must not depend on this.
/// </summary>
internal sealed class UnitOfWorkScope : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly IDbContextTransaction? _efTransaction;
    private readonly ILogger _logger;
    private readonly bool _transactional;
    private bool _completed;
    private bool _committed;

    internal UnitOfWorkScope(
        ApplicationDbContext db,
        DbConnection connection,
        IDbContextTransaction? efTransaction,
        ILogger logger,
        bool transactional)
    {
        _db = db;
        Connection = connection;
        _efTransaction = efTransaction;
        _logger = logger;
        _transactional = transactional;
    }

    public DbConnection Connection { get; }

    public DbTransaction? Transaction => _efTransaction?.GetDbTransaction();

    public bool IsCommitted => _committed;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            throw new InvalidOperationException(
                "The unit of work has already been committed or rolled back.");
        }

        if (_efTransaction is not null)
        {
            await _efTransaction.CommitAsync(cancellationToken);
        }

        _completed = true;
        _committed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        try
        {
            if (_efTransaction is not null)
            {
                await _efTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            // Marked even when the rollback itself fails: the transaction is no
            // longer usable and DisposeAsync must not attempt a second rollback.
            _completed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transactional && !_completed)
        {
            // The executor always commits or rolls back. Reaching this line means a
            // caller bypassed the executor, so this is a real lifecycle bug.
            _logger.LogError(
                "UnitOfWork disposed without CommitAsync or RollbackAsync; the transaction was rolled back and any write discarded.");

            try
            {
                if (_efTransaction is not null)
                {
                    await _efTransaction.RollbackAsync();
                }
            }
            catch (Exception rollbackError)
            {
                _logger.LogError(
                    rollbackError,
                    "Rollback failed while disposing an incomplete unit of work.");
            }
        }

        try
        {
            if (_efTransaction is not null)
            {
                await _efTransaction.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                await Connection.DisposeAsync();
            }
            finally
            {
                // The context outlives this scope. Anything it still tracks belongs
                // to a finished operation and must not leak into the next one.
                _db.ChangeTracker.Clear();
            }
        }
    }
}
