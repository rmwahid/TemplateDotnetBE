using System.Data.Common;

namespace Infrastructure.Database;

/// <summary>
/// One connection and, for a transactional scope, one transaction, shared by every
/// repository call in a single use case.
///
/// Deliberately EF-agnostic: it exposes only ADO.NET types, so it can live in a
/// project that never sees Entities. Whoever owns the DbContext hands it this
/// connection and transaction; Dapper borrows the same two.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    DbConnection Connection { get; }

    /// <summary>Null for a read-only scope, which opens no transaction.</summary>
    DbTransaction? Transaction { get; }

    /// <summary>
    /// True after a successful <see cref="CommitAsync"/>. A failed use case that must
    /// persist a security side effect (for example revoking a token family) commits
    /// explicitly and returns a failed result; the executor then leaves it committed.
    /// </summary>
    bool IsCommitted { get; }

    /// <summary>
    /// Commits the transaction. A no-op for a read-only scope. Must be called on
    /// every successful transactional operation.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls the transaction back and marks the scope as completed. A no-op for a
    /// read-only scope. The executor calls this for business failures and exceptions.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
