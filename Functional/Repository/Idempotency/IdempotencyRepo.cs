using Dapper;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Migrations.Core;
using Model.Idempotency;
using Query;
using Repository.Common;

namespace Repository.Idempotency;

public class IdempotencyRepo : IIdempotencyRepo
{
    private readonly IdempotencyQuery _query = new();
    private readonly ApplicationDbContext _db;

    public IdempotencyRepo(ApplicationDbContext db) => _db = db;

    public async Task<IdempotencyRecordModel?> FindAsync(
        IUnitOfWork uow,
        string operation,
        string ownerScope,
        string key,
        CancellationToken cancellationToken = default)
    {
        return await uow.Connection.QueryFirstOrDefaultAsync<IdempotencyRecordModel>(
            new CommandDefinition(
                _query.GetByKey(),
                new { Operation = operation, OwnerScope = ownerScope, IdempotencyKey = key },
                uow.Transaction,
                cancellationToken: cancellationToken));
    }

    public async Task<int> AddAsync(
        IUnitOfWork uow,
        IdempotencyRecordModel record,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var entity = IdempotencyMapper.ToEntity(record);
            _db.IdempotencyRecords.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity.Id;
        });
    }

    public async Task CompleteAsync(
        IUnitOfWork uow,
        int id,
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            await _db.IdempotencyRecords
                .Where(r => r.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.ResponseBody, responseBody),
                    cancellationToken);
        });
    }

    public async Task<int> DeleteExpiredAsync(
        IUnitOfWork uow,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            return await _db.IdempotencyRecords
                .Where(r => r.ExpiresAt <= utcNow)
                .ExecuteDeleteAsync(cancellationToken);
        });
    }
}
