using Constants;
using Infrastructure.Utils;
using Model.Requests;
using Model.Requests.TodoItem;
using Model.Response;
using Model.Shared;
using Model.TodoItem;
using Repository.TodoItem;
using Service.Common;
using System.Security.Claims;

namespace Service.TodoItem;

public class TodoItemService : ITodoItemService
{
    private readonly IUnitOfWorkExecutor _unitOfWorkExecutor;
    private readonly ITodoItemRepo _todoItemRepo;

    public TodoItemService(
        IUnitOfWorkExecutor unitOfWorkExecutor,
        ITodoItemRepo todoItemRepo)
    {
        _unitOfWorkExecutor = unitOfWorkExecutor;
        _todoItemRepo = todoItemRepo;
    }

    private static int GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new ArgumentException("Invalid user token");
        }
        return userId;
    }

    public Task<ListResponseModel<TodoItemListModel>> GetListAsync(
        BaseListRequestModel request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);

        return _unitOfWorkExecutor.ExecuteReadOnlyAsync(nameof(GetListAsync), async uow =>
        {
            var result = await _todoItemRepo.GetListAsync(uow, request, userId, cancellationToken);

            return new ListResponseModel<TodoItemListModel>
            {
                Status = true,
                List = result.List.Select(item => new TodoItemListModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    IsCompleted = item.IsCompleted,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                }).ToList(),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalPages = FilterUtil.PaginationTotalPages(result.TotalCount, result.PageSize)
            };
        }, cancellationToken);
    }

    public Task<TodoItemListModel?> GetByIdAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);

        return _unitOfWorkExecutor.ExecuteReadOnlyAsync(nameof(GetByIdAsync), async uow =>
        {
            var item = await _todoItemRepo.GetByIdAsync(uow, id, cancellationToken);
            if (item == null || item.CreatedByUserId != userId)
            {
                return null;
            }

            return new TodoItemListModel
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                IsCompleted = item.IsCompleted,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
        }, cancellationToken);
    }

    public Task<SubmitResponseModel> CreateAsync(
        TodoItemCreateRequestModel model,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            var rejected = new SubmitResponseModel { Status = true };
            rejected.AddError(ErrorCodes.TodoItem_TitleRequired, "Title is required");
            rejected.Message = "Validation failed";
            return Task.FromResult(rejected);
        }

        var userId = GetUserId(user);

        return _unitOfWorkExecutor.ExecuteAsync(nameof(CreateAsync), async uow =>
        {
            var result = new SubmitResponseModel { Status = true };

            var item = new TodoItemModel
            {
                Title = model.Title,
                Description = model.Description,
                IsCompleted = false,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            item.Id = await _todoItemRepo.CreateAsync(uow, item, cancellationToken);

            result.Message = "Todo item created successfully";
            return result;
        }, cancellationToken);
    }

    public Task<SubmitResponseModel> UpdateAsync(
        TodoItemUpdateRequestModel model,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            var rejected = new SubmitResponseModel { Status = true };
            rejected.AddError(ErrorCodes.TodoItem_TitleRequired, "Title is required");
            rejected.Message = "Validation failed";
            return Task.FromResult(rejected);
        }

        var userId = GetUserId(user);

        return _unitOfWorkExecutor.ExecuteAsync(nameof(UpdateAsync), async uow =>
        {
            var result = new SubmitResponseModel { Status = true };

            // Ownership is part of the WHERE clause, so there is no read-then-write
            // pair and nothing to go stale in between.
            var updated = await _todoItemRepo.UpdateAsync(uow, new TodoItemModel
            {
                Id = model.Id,
                Title = model.Title,
                Description = model.Description,
                IsCompleted = model.IsCompleted,
                UpdatedAt = DateTime.UtcNow
            }, userId, cancellationToken);

            if (!updated)
            {
                result.AddError(ErrorCodes.TodoItem_NotFound, "Todo item not found");
                result.Message = "Update failed";
                return result;
            }

            result.Message = "Todo item updated successfully";
            return result;
        }, cancellationToken);
    }

    public Task<SubmitResponseModel> DeleteAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);

        return _unitOfWorkExecutor.ExecuteAsync(nameof(DeleteAsync), async uow =>
        {
            var result = new SubmitResponseModel { Status = true };

            if (!await _todoItemRepo.DeleteAsync(uow, id, userId, cancellationToken))
            {
                result.AddError(ErrorCodes.TodoItem_NotFound, "Todo item not found");
                result.Message = "Delete failed";
                return result;
            }

            result.Message = "Todo item deleted successfully";
            return result;
        }, cancellationToken);
    }
}
