using Model.Auth;
using Model.Requests;
using Model.Response;

namespace Service.User;

public interface IUserService
{
    Task<ListResponseModel<UserModel>> GetListAsync(
        BaseListRequestModel request,
        CancellationToken cancellationToken = default);
}
