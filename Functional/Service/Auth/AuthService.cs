using Constants;
using Infrastructure.Exceptions;
using Infrastructure.Hashers;
using Microsoft.Extensions.Options;
using Model.Auth;
using Model.Response;
using Model.Shared;
using Repository.User;
using Service.Common;
using Service.RefreshToken;
using Service.Token;

namespace Service.Auth;

public class AuthService : IAuthService
{
    private readonly IUnitOfWorkExecutor _unitOfWorkExecutor;
    private readonly IUserRepo _userRepo;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AdminConfig _adminConfig;

    public AuthService(
        IUnitOfWorkExecutor unitOfWorkExecutor,
        IUserRepo userRepo,
        IRefreshTokenService refreshTokenService,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IOptions<AdminConfig> adminConfig)
    {
        _unitOfWorkExecutor = unitOfWorkExecutor;
        _userRepo = userRepo;
        _refreshTokenService = refreshTokenService;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _adminConfig = adminConfig.Value;
    }

    public Task<SubmitResponseModel> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        return _unitOfWorkExecutor.ExecuteAsync(nameof(RegisterAsync), async uow =>
        {
            // The existence checks and the insert share one unit of work: the checks
            // must see a consistent picture, and returning a failed result leaves the
            // transaction uncommitted, which rolls the whole thing back.
            var result = new SubmitResponseModel { Status = true };

            if (await _userRepo.CountByUsernameAsync(uow, request.Username, cancellationToken) > 0)
            {
                result.AddError(ErrorCodes.User_UsernameExists, "Username already exists");
            }

            if (await _userRepo.CountByEmailAsync(uow, request.Email, cancellationToken) > 0)
            {
                result.AddError(ErrorCodes.User_EmailExists, "Email already exists");
            }

            if (!result.Status)
            {
                result.Message = "Registration failed";
                return result;
            }

            try
            {
                await _userRepo.CreateAsync(uow, new UserAuthModel
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = _passwordHasher.HashPassword(request.Password),
                    Role = Roles.User,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }, cancellationToken);
            }
            catch (UniqueConstraintException ex)
            {
                // Two concurrent registrations can pass the COUNT checks above. The
                // unique index is the real guard; translate its failure into the same
                // business result instead of letting it become a 500.
                AddUniqueUserError(result, ex);
                result.Message = "Registration failed";
                return result;
            }

            result.Message = "Registration successful";
            return result;
        }, cancellationToken);
    }

    public async Task<SubmitResponseModel> RegisterAdminAsync(
        RegisterAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_adminConfig.SecretKey) ||
            !string.Equals(request.SecretKey, _adminConfig.SecretKey, StringComparison.Ordinal))
        {
            var rejected = new SubmitResponseModel { Status = true };
            rejected.AddError(
                ErrorCodes.Forbidden_InsufficientPermission,
                "Invalid admin secret key");
            rejected.Message = "Registration failed";
            return rejected;
        }

        return await _unitOfWorkExecutor.ExecuteAsync(nameof(RegisterAdminAsync), async uow =>
        {
            // A fresh result per attempt keeps retries from inheriting errors added by
            // a previous attempt.
            var result = new SubmitResponseModel { Status = true };

            if (await _userRepo.CountByUsernameAsync(uow, request.Username, cancellationToken) > 0)
            {
                result.AddError(ErrorCodes.User_UsernameExists, "Username already exists");
            }

            if (await _userRepo.CountByEmailAsync(uow, request.Email, cancellationToken) > 0)
            {
                result.AddError(ErrorCodes.User_EmailExists, "Email already exists");
            }

            if (!result.Status)
            {
                result.Message = "Registration failed";
                return result;
            }

            try
            {
                await _userRepo.CreateAsync(uow, new UserAuthModel
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = _passwordHasher.HashPassword(request.Password),
                    Role = Roles.Admin,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }, cancellationToken);
            }
            catch (UniqueConstraintException ex)
            {
                AddUniqueUserError(result, ex);
                result.Message = "Registration failed";
                return result;
            }

            result.Message = "Admin registration successful";
            return result;
        }, cancellationToken);
    }

    public Task<DataResponseModel<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return _unitOfWorkExecutor.ExecuteAsync(nameof(LoginAsync), async uow =>
        {
            var user = await _userRepo.GetByUsernameAsync(uow, request.Username, cancellationToken);
            if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return new DataResponseModel<AuthResponse>
                {
                    Status = false,
                    Message = "Invalid username or password"
                };
            }

            // A login starts a new token family. Other devices keep their own
            // families, so signing in here does not log them out.
            var refreshToken = await _refreshTokenService.CreateSessionAsync(
                uow, user.Id, cancellationToken);

            return new DataResponseModel<AuthResponse>
            {
                Status = true,
                Message = "Login successful",
                Data = new AuthResponse
                {
                    Token = _tokenService.GenerateToken(user),
                    RefreshToken = refreshToken.Token,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role
                }
            };
        }, cancellationToken, useIdempotency: false);
    }

    private static void AddUniqueUserError(
        SubmitResponseModel result,
        UniqueConstraintException exception)
    {
        var constraint = exception.ConstraintName ?? string.Empty;

        if (constraint.Contains("Username", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError(ErrorCodes.User_UsernameExists, "Username already exists");
        }
        else if (constraint.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError(ErrorCodes.User_EmailExists, "Email already exists");
        }
        else
        {
            // The driver did not expose the index name. Both checks are the only
            // unique constraints on this insert, so report both.
            result.AddError(ErrorCodes.User_UsernameExists, "Username already exists");
            result.AddError(ErrorCodes.User_EmailExists, "Email already exists");
        }
    }
}
