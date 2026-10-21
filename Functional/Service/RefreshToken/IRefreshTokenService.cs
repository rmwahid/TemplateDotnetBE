using Infrastructure.Database;
using Model.Auth;
using Model.Response;

namespace Service.RefreshToken;

public interface IRefreshTokenService
{
    /// <summary>
    /// Starts a new session: creates a token family and returns its initial refresh
    /// token. Logging in again creates a separate family, so other devices are not
    /// logged out.
    /// </summary>
    Task<RefreshTokenModel> CreateSessionAsync(
        IUnitOfWork uow,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Validates the supplied token and rotates it within its family.</summary>
    Task<DataResponseModel<AuthResponse>> RefreshAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes an active refresh token.</summary>
    Task<SubmitResponseModel> RevokeAsync(
        string token,
        CancellationToken cancellationToken = default);
}
