using Constants;
using Infrastructure.Database;
using Microsoft.Extensions.Options;
using Model.Auth;
using Model.Response;
using Model.Shared;
using Repository.RefreshToken;
using Repository.User;
using Service.Common;
using Service.Token;
using System.Security.Cryptography;

namespace Service.RefreshToken;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IUnitOfWorkExecutor _unitOfWorkExecutor;
    private readonly IRefreshTokenRepo _refreshTokenRepo;
    private readonly IUserRepo _userRepo;
    private readonly ITokenService _tokenService;
    private readonly RefreshTokenConfig _config;

    public RefreshTokenService(
        IUnitOfWorkExecutor unitOfWorkExecutor,
        IRefreshTokenRepo refreshTokenRepo,
        IUserRepo userRepo,
        ITokenService tokenService,
        IOptions<RefreshTokenConfig> config)
    {
        _unitOfWorkExecutor = unitOfWorkExecutor;
        _refreshTokenRepo = refreshTokenRepo;
        _userRepo = userRepo;
        _tokenService = tokenService;
        _config = config.Value;
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private RefreshTokenModel BuildReplacement(int userId, int familyId) => new()
    {
        UserId = userId,
        FamilyId = familyId,
        Token = GenerateRefreshToken(),
        Expires = DateTime.UtcNow.AddDays(_config.LifetimeDays),
        CreatedAt = DateTime.UtcNow
    };

    public async Task<RefreshTokenModel> CreateSessionAsync(
        IUnitOfWork uow,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var familyId = await _refreshTokenRepo.CreateFamilyAsync(
            uow, userId, cancellationToken);

        var token = BuildReplacement(userId, familyId);
        await _refreshTokenRepo.CreateAsync(uow, token, cancellationToken);
        return token;
    }

    public Task<DataResponseModel<AuthResponse>> RefreshAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return _unitOfWorkExecutor.ExecuteAsync(nameof(RefreshAsync), async uow =>
        {
            var presented = await _refreshTokenRepo.GetByTokenAsync(
                uow, token, cancellationToken);

            // Tokens created before refresh-token families existed have no family
            // and must log in again.
            if (presented is null || presented.FamilyId is null)
            {
                return Invalid();
            }

            var familyId = presented.FamilyId.Value;

            // Every refresh and family revocation locks the family row. This closes
            // the gap where a token inserted by a concurrent refresh could escape
            // a revocation that read an earlier snapshot.
            if (!await _refreshTokenRepo.LockFamilyAsync(
                    uow, familyId, cancellationToken))
            {
                return Invalid();
            }

            // Another request may have rotated the token while this one waited for
            // the family lock, so re-read before deciding.
            presented = await _refreshTokenRepo.GetByTokenAsync(
                uow, token, cancellationToken);

            if (presented is null || presented.FamilyId != familyId)
            {
                return Invalid();
            }

            var now = DateTime.UtcNow;

            if (presented.IsActive)
            {
                var replacement = BuildReplacement(presented.UserId, familyId);
                var rotated = await _refreshTokenRepo.TryRotateAsync(
                    uow,
                    presented.Token,
                    now,
                    replacement.Token,
                    cancellationToken);

                if (rotated)
                {
                    await _refreshTokenRepo.CreateAsync(uow, replacement, cancellationToken);

                    var user = await _userRepo.GetByIdAsync(
                        uow, presented.UserId, cancellationToken);
                    return user is null ? Invalid() : BuildResponse(user, replacement);
                }

                // Defensive: if a path that does not take the family lock rotated
                // the token first, re-read and let the grace/reuse logic decide.
                presented = await _refreshTokenRepo.GetByTokenAsync(
                    uow, token, cancellationToken);

                if (presented is null || presented.FamilyId != familyId)
                {
                    return Invalid();
                }
            }

            // A revoked token presented within the grace window is a retry of the
            // request that rotated it. Returning the same replacement is safe.
            if (presented.RevokedAt is not null &&
                !string.IsNullOrEmpty(presented.ReplacedByToken) &&
                now - presented.RevokedAt.Value <=
                TimeSpan.FromSeconds(_config.ReuseGracePeriodSeconds))
            {
                var replacement = await _refreshTokenRepo.GetByTokenAsync(
                    uow, presented.ReplacedByToken, cancellationToken);

                if (replacement is not null &&
                    replacement.IsActive &&
                    replacement.FamilyId == familyId)
                {
                    var user = await _userRepo.GetByIdAsync(
                        uow, replacement.UserId, cancellationToken);
                    return user is null ? Invalid() : BuildResponse(user, replacement);
                }
            }

            // Reuse outside the grace window is treated as token theft. Revoke this
            // family only; sessions in other families stay signed in. The family
            // lock ensures a concurrent refresh cannot slip a new token past this.
            await _refreshTokenRepo.RevokeFamilyAsync(
                uow, familyId, now, CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
            return Invalid();
        }, cancellationToken, useIdempotency: false);
    }

    public Task<SubmitResponseModel> RevokeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return _unitOfWorkExecutor.ExecuteAsync(nameof(RevokeAsync), async uow =>
        {
            var result = new SubmitResponseModel { Status = true };

            if (!await _refreshTokenRepo.RevokeAsync(
                    uow, token, DateTime.UtcNow, cancellationToken))
            {
                result.AddError(ErrorCodes.RefreshToken_Invalid, "Invalid refresh token");
                result.Message = "Revoke failed";
                return result;
            }

            result.Message = "Token revoked successfully";
            return result;
        }, cancellationToken, useIdempotency: false);
    }

    private DataResponseModel<AuthResponse> BuildResponse(
        UserAuthModel user,
        RefreshTokenModel refreshToken)
    {
        return new DataResponseModel<AuthResponse>
        {
            Status = true,
            Message = "Token refreshed successfully",
            Data = new AuthResponse
            {
                Token = _tokenService.GenerateToken(user),
                RefreshToken = refreshToken.Token,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            }
        };
    }

    private static DataResponseModel<AuthResponse> Invalid() => new()
    {
        Status = false,
        Message = "Invalid refresh token"
    };
}
