using Infrastructure.Exceptions;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Repository.Common;

/// <summary>
/// Converts provider/EF write failures into exceptions the application layer can
/// understand. Keeping this in Repository is what lets Service catch a
/// <see cref="UniqueConstraintException"/> without referencing EF Core.
/// </summary>
internal static class RepositoryExceptionTranslator
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (DbError.TryGetUniqueViolation(ex, out var constraint))
        {
            throw new UniqueConstraintException(constraint, ex);
        }
        catch (DbException ex) when (DbError.TryGetUniqueViolation(ex, out var constraint))
        {
            throw new UniqueConstraintException(constraint, ex);
        }
    }

    public static async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (DbError.TryGetUniqueViolation(ex, out var constraint))
        {
            throw new UniqueConstraintException(constraint, ex);
        }
        catch (DbException ex) when (DbError.TryGetUniqueViolation(ex, out var constraint))
        {
            throw new UniqueConstraintException(constraint, ex);
        }
    }
}
