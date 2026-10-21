using System.Diagnostics;
using System.Text.Json;
using Infrastructure.Database;
using Infrastructure.Exceptions;
using Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Idempotency;
using Model.Response;
using Repository.Idempotency;

namespace Service.Common;

public sealed class UnitOfWorkExecutor : IUnitOfWorkExecutor
{
    private readonly IUnitOfWorkFactory _factory;
    private readonly IUnitOfWorkExecutionStrategy _executionStrategy;
    private readonly IIdempotencyRepo _idempotencyRepo;
    private readonly IIdempotencyContext _idempotencyContext;
    private readonly TimeSpan _idempotencyLifetime;
    private readonly ILogger<UnitOfWorkExecutor> _logger;

    public UnitOfWorkExecutor(
        IUnitOfWorkFactory factory,
        IUnitOfWorkExecutionStrategy executionStrategy,
        IIdempotencyRepo idempotencyRepo,
        IIdempotencyContext idempotencyContext,
        IOptions<IdempotencyOptions> idempotencyOptions,
        ILogger<UnitOfWorkExecutor> logger)
    {
        _factory = factory;
        _executionStrategy = executionStrategy;
        _idempotencyRepo = idempotencyRepo;
        _idempotencyContext = idempotencyContext;
        _idempotencyLifetime = idempotencyOptions.Value.RecordLifetime;
        _logger = logger;
    }

    public Task<T> ExecuteAsync<T>(
        string operationName,
        Func<IUnitOfWork, Task<T>> operation,
        CancellationToken cancellationToken = default,
        bool useIdempotency = true)
        where T : BaseResponseModel
    {
        var hasIdempotencyKey = useIdempotency && _idempotencyContext.IsPresent;

        if (!hasIdempotencyKey)
        {
            // An unkeyed write cannot be replayed safely, and auth endpoints must
            // not retry at all, so neither uses the retrying execution strategy.
            return _executionStrategy.ExecuteNoRetryAsync(
                () => ExecuteCoreAsync(operationName, operation, cancellationToken),
                cancellationToken);
        }

        return _executionStrategy.ExecuteAsync(
            () => ExecuteWithIdempotencyAsync(operationName, operation, cancellationToken),
            cancellationToken);
    }

    public Task<T> ExecuteReadOnlyAsync<T>(
        string operationName,
        Func<IUnitOfWork, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return _executionStrategy.ExecuteAsync(async () =>
        {
            var started = Stopwatch.GetTimestamp();
            await using var uow = await _factory.BeginReadOnlyAsync(cancellationToken);

            try
            {
                var result = await operation(uow);
                UnitOfWorkMetrics.ReadCompleted(
                    operationName,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                _logger.LogDebug(
                    "Read-only unit of work completed for {Operation}",
                    operationName);
                return result;
            }
            catch
            {
                UnitOfWorkMetrics.Failed(
                    operationName,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                throw;
            }
        }, cancellationToken);
    }

    private async Task<T> ExecuteCoreAsync<T>(
        string operationName,
        Func<IUnitOfWork, Task<T>> operation,
        CancellationToken cancellationToken)
        where T : BaseResponseModel
    {
        var started = Stopwatch.GetTimestamp();
        await using var uow = await _factory.BeginAsync(cancellationToken);

        try
        {
            var result = await operation(uow);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (result.Status)
            {
                await uow.CommitAsync(cancellationToken);
                UnitOfWorkMetrics.Committed(operationName, elapsedMs);
                _logger.LogDebug(
                    "Unit of work committed for {Operation} in {ElapsedMs} ms",
                    operationName,
                    elapsedMs);
            }
            else
            {
                if (uow.IsCommitted)
                {
                    // The use case intentionally committed a failure side effect,
                    // such as revoking a refresh-token family. Do not roll it back.
                    UnitOfWorkMetrics.FailureSideEffectCommitted(operationName, elapsedMs);
                    _logger.LogInformation(
                        "Unit of work committed a failure side effect for {Operation}",
                        operationName);
                    return result;
                }

                await uow.RollbackAsync(CancellationToken.None);
                UnitOfWorkMetrics.BusinessRolledBack(operationName, elapsedMs);
                _logger.LogInformation(
                    "Unit of work rolled back for {Operation}: business result rejected",
                    operationName);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync(uow, operationName);
            UnitOfWorkMetrics.Cancelled(
                operationName,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            _logger.LogDebug(
                "Unit of work cancelled for {Operation}",
                operationName);
            throw;
        }
        catch
        {
            await SafeRollbackAsync(uow, operationName);
            UnitOfWorkMetrics.Failed(
                operationName,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    private async Task<T> ExecuteWithIdempotencyAsync<T>(
        string operationName,
        Func<IUnitOfWork, Task<T>> operation,
        CancellationToken cancellationToken)
        where T : BaseResponseModel
    {
        var started = Stopwatch.GetTimestamp();
        var operationKey = _idempotencyContext.Operation;
        var ownerScope = _idempotencyContext.OwnerScope;
        var key = _idempotencyContext.Key;
        var requestHash = _idempotencyContext.RequestHash;

        await using var uow = await _factory.BeginAsync(cancellationToken);

        try
        {
            var existing = await _idempotencyRepo.FindAsync(
                uow, operationKey, ownerScope, key, cancellationToken);

            if (existing is not null)
            {
                if (existing.ExpiresAt > DateTime.UtcNow)
                {
                    await uow.RollbackAsync(CancellationToken.None);
                    return Replay<T>(existing, requestHash, operationName);
                }

                await _idempotencyRepo.DeleteExpiredAsync(uow, DateTime.UtcNow, cancellationToken);
            }

            var record = new IdempotencyRecordModel
            {
                Operation = operationKey,
                OwnerScope = ownerScope,
                IdempotencyKey = key,
                RequestHash = requestHash,
                ResponseBody = string.Empty,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_idempotencyLifetime)
            };

            try
            {
                record.Id = await _idempotencyRepo.AddAsync(uow, record, cancellationToken);
            }
            catch (UniqueConstraintException)
            {
                // Another request with the same key won the race. Its transaction has
                // committed by the time the unique index reports the conflict.
                await uow.RollbackAsync(CancellationToken.None);
                return await ReplayExistingAsync<T>(
                    operationKey,
                    ownerScope,
                    key,
                    requestHash,
                    operationName,
                    cancellationToken);
            }

            var result = await operation(uow);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (result.Status)
            {
                var responseBody = JsonSerializer.Serialize(result);
                await _idempotencyRepo.CompleteAsync(
                    uow, record.Id, responseBody, cancellationToken);
                await uow.CommitAsync(cancellationToken);
                UnitOfWorkMetrics.Committed(operationName, elapsedMs);
                _logger.LogDebug(
                    "Unit of work committed for {Operation} with idempotency key in {ElapsedMs} ms",
                    operationName,
                    elapsedMs);
            }
            else
            {
                if (uow.IsCommitted)
                {
                    // A keyed operation must not commit while returning a failed
                    // result, because its stored response would be empty and a retry
                    // could not be replayed.
                    throw new InvalidOperationException(
                        "A keyed operation committed a failure side effect; this is not supported.");
                }

                // A business failure rolls back the record too, so the client can
                // retry the same key after correcting the request.
                await uow.RollbackAsync(CancellationToken.None);
                UnitOfWorkMetrics.BusinessRolledBack(operationName, elapsedMs);
                _logger.LogInformation(
                    "Unit of work rolled back for {Operation}: business result rejected",
                    operationName);
            }

            return result;
        }
        catch (IdempotencyConflictException)
        {
            await SafeRollbackAsync(uow, operationName);
            throw;
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync(uow, operationName);
            UnitOfWorkMetrics.Cancelled(
                operationName,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch
        {
            await SafeRollbackAsync(uow, operationName);
            UnitOfWorkMetrics.Failed(
                operationName,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    private async Task<T> ReplayExistingAsync<T>(
        string operationKey,
        string ownerScope,
        string key,
        string requestHash,
        string operationName,
        CancellationToken cancellationToken)
        where T : BaseResponseModel
    {
        await using var uow = await _factory.BeginReadOnlyAsync(cancellationToken);
        var existing = await _idempotencyRepo.FindAsync(
            uow, operationKey, ownerScope, key, cancellationToken);

        if (existing is null)
        {
            throw new IdempotencyConflictException();
        }

        return Replay<T>(existing, requestHash, operationName);
    }

    private T Replay<T>(
        IdempotencyRecordModel existing,
        string requestHash,
        string operationName)
        where T : BaseResponseModel
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }

        var result = JsonSerializer.Deserialize<T>(existing.ResponseBody);
        if (result is null)
        {
            throw new InvalidOperationException(
                $"The stored idempotency response for {operationName} could not be read.");
        }

        UnitOfWorkMetrics.Replayed(operationName);
        _logger.LogInformation(
            "Idempotent replay for {Operation}",
            operationName);
        return result;
    }

    private async Task SafeRollbackAsync(IUnitOfWork uow, string operationName)
    {
        try
        {
            // Never pass a cancelled token here: rollback is exactly the cleanup that
            // must still run when the caller's request was aborted.
            await uow.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackError)
        {
            _logger.LogError(
                rollbackError,
                "Rollback failed for {Operation}",
                operationName);
        }
    }
}
