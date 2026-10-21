namespace Service.RefreshToken;

public class RefreshTokenConfig
{
    public int LifetimeDays { get; set; } = 7;

    /// <summary>
    /// Window in which a rotated token may be presented again so a lost response can
    /// be retried. Outside this window the reuse is treated as token theft.
    /// </summary>
    public int ReuseGracePeriodSeconds { get; set; } = 30;
}
