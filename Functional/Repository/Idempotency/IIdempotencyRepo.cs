using Infrastructure.Database;
using Model.Idempotency;

namespace Repository.Idempotency;

public interface IIdempotencyRepo
{
    Task<IdempotencyRecordModel?> FindAsync(
        IUnitOfWork uow,
        string operation,
        string ownerScope,
        string key,
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(
        IUnitOfWork uow,
        IdempotencyRecordModel record,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        IUnitOfWork uow,
        int id,
        string responseBody,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(
        IUnitOfWork uow,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
