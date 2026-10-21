using Model.Auth;
using UserEntity = Entities.User;

namespace Repository.User;

/// <summary>
/// Translates the user Model into the entity EF writes.
///
/// Internal on purpose: a tracked entity must never be returned from this project, or
/// it could reach a response object. The alias exists because <c>Repository.User</c>
/// is a namespace, so a bare <c>User</c> resolves to it rather than to the entity.
/// There is no reverse mapping — reads go through Dapper straight into Models.
/// </summary>
internal static class UserMapper
{
    public static UserEntity ToEntity(UserAuthModel model) => new()
    {
        Username = model.Username,
        Email = model.Email,
        PasswordHash = model.PasswordHash,
        Role = model.Role,
        CreatedAt = model.CreatedAt,
        IsActive = model.IsActive,
        // Id is deliberately not copied: on insert the database generates it and EF
        // reads the value back. Setting it would ask EF to insert an explicit key.
    };
}
