namespace Infrastructure.Exceptions;

/// <summary>
/// Raised when an update loses an optimistic-concurrency check. Mapped to HTTP 409
/// by the API layer.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception innerException)
        : base("The resource was modified by another operation.", innerException)
    {
    }
}
