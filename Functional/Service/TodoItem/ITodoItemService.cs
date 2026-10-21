using Model.Requests;
using Model.Requests.TodoItem;
using Model.Response;
using Model.TodoItem;
using System.Security.Claims;

namespace Service.TodoItem;

public interface ITodoItemService
{
    Task<ListResponseModel<TodoItemListModel>> GetListAsync(
        BaseListRequestModel request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<TodoItemListModel?> GetByIdAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<SubmitResponseModel> CreateAsync(
        TodoItemCreateRequestModel model,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<SubmitResponseModel> UpdateAsync(
        TodoItemUpdateRequestModel model,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<SubmitResponseModel> DeleteAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
