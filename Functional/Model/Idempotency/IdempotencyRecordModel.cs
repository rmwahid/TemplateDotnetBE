namespace Model.Idempotency;

public class IdempotencyRecordModel
{
    public int Id { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string OwnerScope { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
