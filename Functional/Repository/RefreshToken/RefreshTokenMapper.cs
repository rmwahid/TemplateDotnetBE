using Model.Auth;
using RefreshTokenEntity = Entities.RefreshToken;

namespace Repository.RefreshToken;

/// <summary>
/// Translates the refresh token Model into the entity EF writes. See <c>UserMapper</c>
/// for why this is internal, why it maps in one direction only, and why the alias is
/// needed.
/// </summary>
internal static class RefreshTokenMapper
{
    public static RefreshTokenEntity ToEntity(RefreshTokenModel model) => new()
    {
        UserId = model.UserId,
        FamilyId = model.FamilyId,
        Token = model.Token,
        Expires = model.Expires,
        CreatedAt = model.CreatedAt,
        RevokedAt = model.RevokedAt,
        ReplacedByToken = model.ReplacedByToken,
        // Id is not copied: the database generates it on insert.
    };
}
