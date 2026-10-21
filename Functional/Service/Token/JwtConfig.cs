namespace Service.Token;

public class JwtConfig
{
    public string Secret { get; set; } = string.Empty;
    public int ExpireHours { get; set; } = 24;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
