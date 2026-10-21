using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Migrations.Core;

namespace UnitOfWork;

/// <summary>
/// Builds unit-of-work scopes on top of the scoped <see cref="ApplicationDbContext"/>.
///
/// The connection comes from <see cref="IConnectionProvider"/> and is attached to the
/// context, so the transaction EF begins is the same one Dapper queries run inside.
/// </summary>
public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly ApplicationDbContext _db;
    private readonly IConnectionProvider _connections;
    private readonly ILogger<UnitOfWorkFactory> _logger;

    public UnitOfWorkFactory(
        ApplicationDbContext db,
        IConnectionProvider connections,
        ILogger<UnitOfWorkFactory> logger)
    {
        _db = db;
        _connections = connections;
        _logger = logger;
    }

    public Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default) =>
        BeginCoreAsync(transactional: true, cancellationToken);

    public Task<IUnitOfWork> BeginReadOnlyAsync(CancellationToken cancellationToken = default) =>
        BeginCoreAsync(transactional: false, cancellationToken);

    private async Task<IUnitOfWork> BeginCoreAsync(bool transactional, CancellationToken cancellationToken)
    {
        var connection = _connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            // Attach the caller-owned connection, then let EF begin the transaction on it.
            // From here on, SaveChanges and Dapper use the same connection and transaction.
            _db.Database.SetDbConnection(connection);
            var transaction = transactional
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            return new UnitOfWorkScope(_db, connection, transaction, _logger, transactional);
        }
        catch
        {
            // The scope never got the chance to own the connection, so it must be
            // disposed here rather than leaked.
            await connection.DisposeAsync();
            throw;
        }
    }
}
