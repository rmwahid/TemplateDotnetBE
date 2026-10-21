using Infrastructure.Database;
using Model.Response;

namespace Service.Common;

/// <summary>
/// Owns the transaction boundary for one use case.
///
/// A service returns a result; this executor commits when the result is successful
/// and rolls back when it is a business failure. Exceptions roll back and propagate
/// so the API error handler is the only place that logs them as errors.
///
/// When the request carries an Idempotency-Key, the key, request fingerprint, and
/// successful response are stored in the same transaction as the business write.
/// A retry with the same key and payload replays the stored response.
/// </summary>
public interface IUnitOfWorkExecutor
{
    Task<T> ExecuteAsync<T>(
        string operationName,
        Func<IUnitOfWork, Task<T>> operation,
        CancellationToken cancellationToken = default,
        bool useIdempotency = true)
        where T : BaseResponseModel;

    Task<T> ExecuteReadOnlyAsync<T>(
        string operationName,
        Func<IUnitOfWork, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
