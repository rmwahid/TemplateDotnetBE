using Microsoft.Extensions.Configuration;

namespace Migrations.Core;

public static class ConnectionStringResolver
{
    /// <summary>
    /// Builds a small configuration provider for design-time EF Core tooling.
    /// Reads from optional appsettings.json files and environment variables.
    /// </summary>
    private const string UserSecretsId = "TemplateDotnetBE.Functional.API";

    private static string GetUserSecretsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            return Path.Combine(
                appData, "Microsoft", "UserSecrets", UserSecretsId, "secrets.json");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(
            home, ".microsoft", "usersecrets", UserSecretsId, "secrets.json");
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true, reloadOnChange: false)
            .AddJsonFile(GetUserSecretsPath(), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Resolves the connection string for design-time migrations using the same
    /// precedence as the API: ConnectionStrings:DefaultConnection, then
    /// Database:ConnectionString.
    /// </summary>
    public static string GetConnectionString()
    {
        var configuration = BuildConfiguration();

        return configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException(
                """
                Design-time connection string not found. Configure ConnectionStrings:DefaultConnection
                or Database:ConnectionString via appsettings.json, user secrets, or the
                ConnectionStrings__DefaultConnection / Database__ConnectionString environment variables.
                """);
    }
}
