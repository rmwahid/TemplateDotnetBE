namespace Infrastructure.Security;

public sealed class IdempotencyOptions
{
    /// <summary>
    /// HMAC key used to fingerprint request bodies. Never store this value in source
    /// control; configure it through an environment variable or a secret manager.
    /// </summary>
    public byte[] Key { get; set; } = Array.Empty<byte>();

    /// <summary>How long a completed idempotency record can be replayed.</summary>
    public TimeSpan RecordLifetime { get; set; } = TimeSpan.FromHours(24);
}
