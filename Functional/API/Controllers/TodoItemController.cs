using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Requests;
using Model.Requests.TodoItem;
using Model.Response;
using Model.TodoItem;
using Service.TodoItem;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TodoItemController : ControllerBase
{
    private readonly ITodoItemService _todoItemService;

    public TodoItemController(ITodoItemService todoItemService)
    {
        _todoItemService = todoItemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string searchQuery = "",
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var request = new BaseListRequestModel
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchQuery = searchQuery,
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await _todoItemService.GetListAsync(request, User, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await _todoItemService.GetByIdAsync(id, User, cancellationToken);
        if (result == null)
        {
            return NotFound(new DataResponseModel<TodoItemListModel>
            {
                Status = false,
                Message = "Todo item not found"
            });
        }
        return Ok(new DataResponseModel<TodoItemListModel>
        {
            Status = true,
            Data = result
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TodoItemCreateRequestModel model,
        CancellationToken cancellationToken = default)
    {
        var result = await _todoItemService.CreateAsync(model, User, cancellationToken);
        return StatusCode(result.Status ? 201 : 400, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] TodoItemUpdateRequestModel model,
        CancellationToken cancellationToken = default)
    {
        model.Id = id;
        var result = await _todoItemService.UpdateAsync(model, User, cancellationToken);
        return StatusCode(result.Status ? 200 : 400, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await _todoItemService.DeleteAsync(id, User, cancellationToken);
        return StatusCode(result.Status ? 200 : 400, result);
    }
}
