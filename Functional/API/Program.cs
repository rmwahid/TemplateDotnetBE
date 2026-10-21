using API.Jobs;
using API.Services;
using Constants;
using Hangfire;
using Hangfire.MemoryStorage;
using Migrations.Core;
using Migrations.MySQL;
using Migrations.PostgreSQL;
using Logger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Repository.RefreshToken;
using Repository.TodoItem;
using Repository.User;
using Scalar.AspNetCore;
using Service.Auth;
using Service.Common;
using Service.RefreshToken;
using Service.Token;
using Service.TodoItem;
using Service.User;
using System.Text;
using System.Threading.RateLimiting;
using Infrastructure.Database;
using Infrastructure.Hashers;
using Infrastructure.Security;
using Repository.Idempotency;
using System.Security.Cryptography;
using UnitOfWork;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

// Configure strongly typed settings
services.Configure<DatabaseConfig>(configuration.GetSection("Database"));
services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
services.Configure<AdminConfig>(configuration.GetSection("Admin"));
services.Configure<RefreshTokenConfig>(configuration.GetSection("RefreshToken"));

// Idempotency fingerprints use HMAC, so the key never comes from source control.
// In production it must be supplied by the environment or a secret manager.
var idempotencyKeyValue = configuration["Security:IdempotencyHmacKey"];
var generatedIdempotencyKey = false;
byte[] idempotencyKey;
if (string.IsNullOrWhiteSpace(idempotencyKeyValue))
{
    if (builder.Environment.IsDevelopment())
    {
        idempotencyKey = RandomNumberGenerator.GetBytes(32);
        generatedIdempotencyKey = true;
    }
    else
    {
        throw new InvalidOperationException(
            "Security:IdempotencyHmacKey must be configured through an environment variable or a secret manager.");
    }
}
else
{
    try
    {
        idempotencyKey = Convert.FromBase64String(idempotencyKeyValue);
    }
    catch (FormatException)
    {
        throw new InvalidOperationException(
            "Security:IdempotencyHmacKey must be Base64-encoded.");
    }

    if (idempotencyKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Security:IdempotencyHmacKey must contain at least 32 bytes.");
    }
}

services.Configure<IdempotencyOptions>(options =>
{
    options.Key = idempotencyKey;
    options.RecordLifetime = TimeSpan.FromHours(24);
});

// Register connection factory based on provider
var databaseProvider = configuration.GetValue<string>("Database:Provider") ?? "PostgreSQL";
if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    services.AddSingleton<IConnectionProvider, PostgreSQLConnectionProvider>();
}
else if (databaseProvider.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
{
    services.AddSingleton<IConnectionProvider, MySQLConnectionProvider>();
}
else
{
    throw new InvalidOperationException($"Unsupported database provider: {databaseProvider}");
}

// Register Dapper repositories. Each one serves both reads (Dapper) and writes (EF)
// on the unit of work the caller opens, so an operation that spans several of them
// still runs in one transaction.
services.AddScoped<IUserRepo, UserRepo>();
services.AddScoped<ITodoItemRepo, TodoItemRepo>();
services.AddScoped<IRefreshTokenRepo, RefreshTokenRepo>();
services.AddScoped<IIdempotencyRepo, IdempotencyRepo>();

// Unit of work: one connection and, when transactional, one transaction per use case.
services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
services.AddScoped<IUnitOfWorkExecutionStrategy, DbContextUnitOfWorkExecutionStrategy>();
services.AddScoped<IUnitOfWorkExecutor, UnitOfWorkExecutor>();

// Idempotency request context, set by IdempotencyMiddleware.
services.AddScoped<IIdempotencyContext, IdempotencyContext>();
services.AddSingleton<IIdempotencyRequestHasher, HmacIdempotencyRequestHasher>();

// Register utilities
services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

// Register services
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<ITokenService, TokenService>();
services.AddScoped<IRefreshTokenService, RefreshTokenService>();
services.AddScoped<ITodoItemService, TodoItemService>();
services.AddScoped<IUserService, UserService>();

// Register logger
services.AddSingleton<IRestLogService, RestLogService>();

// Register background job service
services.AddSingleton<ISampleJobService, SampleJobService>();
services.AddScoped<IdempotencyCleanupJob>();

// Add rate limiting
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };
});

// Add health checks
services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// Add Hangfire
services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
    config.UseMemoryStorage();
});
services.AddHangfireServer();

// Register EF Core DbContext (based on provider).
//
// The write path goes through EF, so the context is needed at runtime whether or
// not migrations are applied by the application.
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? configuration.GetValue<string>("Database:ConnectionString");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. Set ConnectionStrings:DefaultConnection or Database:ConnectionString.");
}

var enableRetryOnFailure = configuration.GetValue<bool>("Database:EnableRetryOnFailure");

if (databaseProvider.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
{
    connectionString = MySQLConnectionString.Normalize(connectionString);
}

// IConnectionProvider reads DatabaseConfig.ConnectionString. Override it with the
// same resolved value used by the DbContext so both never diverge.
services.Configure<DatabaseConfig>(options => options.ConnectionString = connectionString);

if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    services.AddDbContext<ApplicationDbContext, PostgreSQLApplicationDbContext>(options =>
        options.UseNpgsql(connectionString, b =>
        {
            b.MigrationsAssembly("Migrations.PostgreSQL");
            if (enableRetryOnFailure)
            {
                b.EnableRetryOnFailure();
            }
        }));
}
else if (databaseProvider.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
{
    services.AddDbContext<ApplicationDbContext, MySQLApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), b =>
        {
            b.MigrationsAssembly("Migrations.MySQL");
            if (enableRetryOnFailure)
            {
                b.EnableRetryOnFailure();
            }
        }));
}

// Add JWT Authentication
var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig();
if (string.IsNullOrWhiteSpace(jwtConfig.Secret))
{
    jwtConfig.Secret = "your-super-secret-key-min-32-chars-long!!";
}

var key = Encoding.ASCII.GetBytes(jwtConfig.Secret);
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
});

services.AddControllers();
services.AddEndpointsApiExplorer();

// Add OpenAPI + Scalar
services.AddOpenApi();

var app = builder.Build();

if (generatedIdempotencyKey)
{
    app.Logger.LogWarning(
        "Security:IdempotencyHmacKey is not configured. Using a random development key; idempotent replays will not survive a restart.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "TemplateDotnetBE Functional API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "TemplateDotnetBE Functional Jobs",
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});

// Schedule recurring job
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<ISampleJobService>(
        "recurring-sample-job",
        service => service.RunRecurringJobAsync(),
        Cron.Minutely);

    recurringJobManager.AddOrUpdate<IdempotencyCleanupJob>(
        "idempotency-cleanup",
        job => job.RunAsync(CancellationToken.None),
        Cron.Daily);
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlerMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers()
   .RequireRateLimiting("fixed");

app.Run();
