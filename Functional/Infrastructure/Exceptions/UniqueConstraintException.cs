namespace Infrastructure.Exceptions;

/// <summary>
/// Raised when a write violates a unique index. Repositories translate the
/// provider-specific exception so services can map it to a business result without
/// referencing EF Core or a database driver.
/// </summary>
public sealed class UniqueConstraintException : Exception
{
    public string? ConstraintName { get; }

    public UniqueConstraintException(string? constraintName, Exception innerException)
        : base(
            constraintName is null
                ? "A unique constraint was violated."
                : $"A unique constraint was violated: {constraintName}.",
            innerException)
    {
        ConstraintName = constraintName;
    }
}
