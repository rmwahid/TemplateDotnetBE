using Model.Auth;
using Model.Response;

namespace Service.Auth;

public interface IAuthService
{
    Task<SubmitResponseModel> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<SubmitResponseModel> RegisterAdminAsync(
        RegisterAdminRequest request,
        CancellationToken cancellationToken = default);

    Task<DataResponseModel<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
