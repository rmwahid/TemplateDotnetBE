using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Requests;
using Service.User;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string searchQuery = "",
        CancellationToken cancellationToken = default)
    {
        var request = new BaseListRequestModel
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchQuery = searchQuery
        };

        var result = await _userService.GetListAsync(request, cancellationToken);
        return Ok(result);
    }
}
