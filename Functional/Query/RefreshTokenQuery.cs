using Constants;

namespace Query;

/// <summary>
/// Read queries only. Writes to this table go through Entity Framework in
/// Repository, so there is no INSERT, UPDATE or DELETE here.
/// </summary>
public class RefreshTokenQuery
{
    public string GetByToken() => $"""
        SELECT Id, UserId, FamilyId, Token, Expires, CreatedAt, RevokedAt, ReplacedByToken
        FROM {Tables.RefreshTokens}
        WHERE Token = @Token;
        """;

    /// <summary>
    /// Takes a row lock on the family for the rest of the transaction. Every refresh
    /// and family revocation for the same family goes through this lock, which closes
    /// the gap between a revocation UPDATE and a concurrent token insert.
    /// </summary>
    public string LockFamily() => $"""
        SELECT Id
        FROM {Tables.RefreshTokenFamilies}
        WHERE Id = @FamilyId
        FOR UPDATE;
        """;
}
