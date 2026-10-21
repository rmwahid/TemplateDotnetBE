using Dapper;
using Infrastructure.Database;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using Migrations.Core;
using Model.Requests;
using Model.Shared;
using Model.TodoItem;
using Query;
using Repository.Common;

namespace Repository.TodoItem;

public class TodoItemRepo : ITodoItemRepo
{
    private readonly TodoItemQuery _query = new();
    private readonly ApplicationDbContext _db;

    public TodoItemRepo(ApplicationDbContext db) => _db = db;

    // ── reads: Dapper, inside the caller's transaction ──

    public async Task<TodoItemModel?> GetByIdAsync(
        IUnitOfWork uow,
        int id,
        CancellationToken cancellationToken = default) =>
        await uow.Connection.QueryFirstOrDefaultAsync<TodoItemModel>(
            new CommandDefinition(
                _query.GetById(),
                new { Id = id },
                uow.Transaction,
                cancellationToken: cancellationToken));

    public async Task<PagedResultModel<TodoItemModel>> GetListAsync(
        IUnitOfWork uow,
        BaseListRequestModel request,
        int createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var sql = _query.GetListWithCount(request, createdByUserId);
        var search = FilterUtil.FilterLike(request.SearchQuery);

        var command = new CommandDefinition(
            sql,
            new
            {
                CreatedByUserId = createdByUserId,
                SearchQuery = string.IsNullOrWhiteSpace(request.SearchQuery) ? null : search,
                PageSize = request.PageSize,
                Offset = FilterUtil.PaginationOffset(request.PageNumber, request.PageSize)
            },
            uow.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await uow.Connection.QueryMultipleAsync(command);

        var list = (await multi.ReadAsync<TodoItemModel>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();

        return new PagedResultModel<TodoItemModel>
        {
            List = list,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    // ── writes: EF Core, enlisting the caller's transaction through the context ──

    public async Task<int> CreateAsync(
        IUnitOfWork uow,
        TodoItemModel item,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var entity = TodoItemMapper.ToEntity(item);
            _db.TodoItems.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity.Id;
        });
    }

    public async Task<bool> UpdateAsync(
        IUnitOfWork uow,
        TodoItemModel item,
        int createdByUserId,
        CancellationToken cancellationToken = default)
    {
        // Set-based: a single UPDATE with the owner in the WHERE clause. Nothing is
        // loaded, so there is no read-then-write gap and no stale tracked copy.
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var affected = await _db.TodoItems
                .Where(t => t.Id == item.Id && t.CreatedByUserId == createdByUserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Title, item.Title)
                    .SetProperty(t => t.Description, item.Description)
                    .SetProperty(t => t.IsCompleted, item.IsCompleted)
                    .SetProperty(t => t.UpdatedAt, item.UpdatedAt),
                    cancellationToken);

            return affected > 0;
        });
    }

    public async Task<bool> DeleteAsync(
        IUnitOfWork uow,
        int id,
        int createdByUserId,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var affected = await _db.TodoItems
                .Where(t => t.Id == id && t.CreatedByUserId == createdByUserId)
                .ExecuteDeleteAsync(cancellationToken);

            return affected > 0;
        });
    }
}
