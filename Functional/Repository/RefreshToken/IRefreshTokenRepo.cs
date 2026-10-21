using Infrastructure.Database;
using Model.Auth;

namespace Repository.RefreshToken;

/// <summary>
/// Both sides of the refresh token store on one unit of work: Dapper for reads,
/// EF Core for writes. Every operation for a token family is serialised by locking
/// the family row, so reuse revocation and a concurrent rotation cannot interleave.
/// </summary>
public interface IRefreshTokenRepo
{
    Task<RefreshTokenModel?> GetByTokenAsync(
        IUnitOfWork uow,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new token family for a login session and returns its id.</summary>
    Task<int> CreateFamilyAsync(
        IUnitOfWork uow,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the family row with <c>SELECT ... FOR UPDATE</c> for the rest of the
    /// transaction. Returns false when the family does not exist.
    /// </summary>
    Task<bool> LockFamilyAsync(
        IUnitOfWork uow,
        int familyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the presented token only if it is still active and records the token
    /// that replaced it. Returns false when another request already rotated it.
    /// </summary>
    Task<bool> TryRotateAsync(
        IUnitOfWork uow,
        string token,
        DateTime revokedAt,
        string replacementToken,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a replacement token inside the caller's transaction.</summary>
    Task CreateAsync(
        IUnitOfWork uow,
        RefreshTokenModel token,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes every active token in one family.</summary>
    Task<int> RevokeFamilyAsync(
        IUnitOfWork uow,
        int familyId,
        DateTime revokedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes one token by value. False when it is missing or already revoked.</summary>
    Task<bool> RevokeAsync(
        IUnitOfWork uow,
        string token,
        DateTime revokedAt,
        CancellationToken cancellationToken = default);
}
