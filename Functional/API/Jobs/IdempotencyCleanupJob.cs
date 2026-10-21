using Infrastructure.Database;
using Microsoft.Extensions.Logging;
using Repository.Idempotency;

namespace API.Jobs;

/// <summary>
/// Deletes idempotency records whose replay window has passed. Run on a schedule so
/// the table does not grow without bound.
/// </summary>
public sealed class IdempotencyCleanupJob
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IIdempotencyRepo _idempotencyRepo;
    private readonly ILogger<IdempotencyCleanupJob> _logger;

    public IdempotencyCleanupJob(
        IUnitOfWorkFactory factory,
        IIdempotencyRepo idempotencyRepo,
        ILogger<IdempotencyCleanupJob> logger)
    {
        _factory = factory;
        _idempotencyRepo = idempotencyRepo;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = await _factory.BeginAsync(cancellationToken);
        var deleted = await _idempotencyRepo.DeleteExpiredAsync(
            uow, DateTime.UtcNow, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Deleted {Count} expired idempotency records",
                deleted);
        }
    }
}
