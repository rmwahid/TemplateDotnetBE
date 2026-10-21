namespace Infrastructure.Database;

/// <summary>
/// Creates unit-of-work scopes.
///
/// <see cref="BeginAsync"/> starts a transaction and is what a use case that writes
/// anything must use. <see cref="BeginReadOnlyAsync"/> opens a connection without a
/// transaction, because wrapping pure reads in one adds a round trip and nothing else.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);

    Task<IUnitOfWork> BeginReadOnlyAsync(CancellationToken cancellationToken = default);
}
