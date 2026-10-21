namespace Infrastructure.Exceptions;

/// <summary>
/// Raised when an Idempotency-Key is reused with a different request payload.
/// Mapped to HTTP 409 by the API layer.
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("The idempotency key was reused with a different request.")
    {
    }
}
