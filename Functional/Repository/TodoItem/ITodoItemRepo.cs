using Infrastructure.Database;
using Model.Requests;
using Model.Shared;
using Model.TodoItem;

namespace Repository.TodoItem;

/// <summary>
/// Both sides of the todo store on one unit of work: Dapper for reads, EF Core for
/// writes.
///
/// Keeping them in one interface is deliberate. An operation that reads something,
/// writes it, and must not be half-applied needs one transaction around all of it —
/// splitting read and write repositories would push that atomicity back onto every
/// caller to reconstruct. The owner travels in the write's WHERE clause instead of a
/// separate read, so there is no gap between checking and writing.
/// </summary>
public interface ITodoItemRepo
{
    Task<TodoItemModel?> GetByIdAsync(
        IUnitOfWork uow,
        int id,
        CancellationToken cancellationToken = default);

    Task<PagedResultModel<TodoItemModel>> GetListAsync(
        IUnitOfWork uow,
        BaseListRequestModel request,
        int createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts the item and returns the identifier the database generated.</summary>
    Task<int> CreateAsync(
        IUnitOfWork uow,
        TodoItemModel item,
        CancellationToken cancellationToken = default);

    /// <summary>False means no row matched — gone, or not theirs.</summary>
    Task<bool> UpdateAsync(
        IUnitOfWork uow,
        TodoItemModel item,
        int createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>False means no row matched — gone, or not theirs.</summary>
    Task<bool> DeleteAsync(
        IUnitOfWork uow,
        int id,
        int createdByUserId,
        CancellationToken cancellationToken = default);
}
