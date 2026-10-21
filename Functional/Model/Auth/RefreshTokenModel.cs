namespace Model.Auth;

public class RefreshTokenModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? FamilyId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public bool IsActive => RevokedAt == null && Expires > DateTime.UtcNow;
    public bool IsExpired => Expires <= DateTime.UtcNow;
}
