namespace Infrastructure.Database;

/// <summary>
/// Runs a unit-of-work operation under the provider's execution strategy.
///
/// With retry disabled this simply invokes the operation. When the DbContext is
/// configured with <c>EnableRetryOnFailure</c>, the whole transaction can be retried
/// because this hook wraps the complete begin/execute/commit sequence rather than a
/// single command.
/// </summary>
public interface IUnitOfWorkExecutionStrategy
{
    Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the operation once, without retrying. Used for writes that are not
    /// protected by an idempotency key and for auth endpoints.
    /// </summary>
    Task<T> ExecuteNoRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default);
}
