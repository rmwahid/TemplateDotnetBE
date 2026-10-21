namespace Infrastructure.Database;

public class DatabaseConfig
{
    public string Provider { get; set; } = "PostgreSQL";
    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableRetryOnFailure { get; set; }
}
