using Dapper;
using Entities;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Migrations.Core;
using Model.Auth;
using Query;
using Repository.Common;

namespace Repository.RefreshToken;

public class RefreshTokenRepo : IRefreshTokenRepo
{
    private readonly RefreshTokenQuery _query = new();
    private readonly ApplicationDbContext _db;

    public RefreshTokenRepo(ApplicationDbContext db) => _db = db;

    public async Task<RefreshTokenModel?> GetByTokenAsync(
        IUnitOfWork uow,
        string token,
        CancellationToken cancellationToken = default) =>
        await uow.Connection.QueryFirstOrDefaultAsync<RefreshTokenModel>(
            new CommandDefinition(
                _query.GetByToken(),
                new { Token = token },
                uow.Transaction,
                cancellationToken: cancellationToken));

    public async Task<int> CreateFamilyAsync(
        IUnitOfWork uow,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var family = new RefreshTokenFamily
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.RefreshTokenFamilies.Add(family);
            await _db.SaveChangesAsync(cancellationToken);
            return family.Id;
        });
    }

    public async Task<bool> LockFamilyAsync(
        IUnitOfWork uow,
        int familyId,
        CancellationToken cancellationToken = default)
    {
        var id = await uow.Connection.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(
                _query.LockFamily(),
                new { FamilyId = familyId },
                uow.Transaction,
                cancellationToken: cancellationToken));

        return id.HasValue;
    }

    public async Task<bool> TryRotateAsync(
        IUnitOfWork uow,
        string token,
        DateTime revokedAt,
        string replacementToken,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var affected = await _db.RefreshTokens
                .Where(t => t.Token == token && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, revokedAt)
                    .SetProperty(t => t.ReplacedByToken, replacementToken),
                    cancellationToken);

            return affected > 0;
        });
    }

    public async Task CreateAsync(
        IUnitOfWork uow,
        RefreshTokenModel token,
        CancellationToken cancellationToken = default)
    {
        await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var entity = RefreshTokenMapper.ToEntity(token);
            _db.RefreshTokens.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
        });
    }

    public async Task<int> RevokeFamilyAsync(
        IUnitOfWork uow,
        int familyId,
        DateTime revokedAt,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            return await _db.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, revokedAt),
                    cancellationToken);
        });
    }

    public async Task<bool> RevokeAsync(
        IUnitOfWork uow,
        string token,
        DateTime revokedAt,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var affected = await _db.RefreshTokens
                .Where(t => t.Token == token && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, revokedAt),
                    cancellationToken);

            return affected > 0;
        });
    }
}
