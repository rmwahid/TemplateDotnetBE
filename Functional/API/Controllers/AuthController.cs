using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Model.Auth;
using Model.Response;
using Service.Auth;
using Service.RefreshToken;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRefreshTokenService _refreshTokenService;

    public AuthController(IAuthService authService, IRefreshTokenService refreshTokenService)
    {
        _authService = authService;
        _refreshTokenService = refreshTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(result.Status ? 201 : 400, result);
    }

    [HttpPost("register-admin")]
    public async Task<IActionResult> RegisterAdmin(
        [FromBody] RegisterAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _authService.RegisterAdminAsync(request, cancellationToken);
        return StatusCode(result.Status ? 201 : 400, result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return StatusCode(result.Status ? 200 : 401, result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _refreshTokenService.RefreshAsync(request.Token, cancellationToken);
        return StatusCode(result.Status ? 200 : 401, result);
    }

    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken(
        [FromBody] RevokeTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _refreshTokenService.RevokeAsync(request.Token, cancellationToken);
        return StatusCode(result.Status ? 200 : 400, result);
    }
}
