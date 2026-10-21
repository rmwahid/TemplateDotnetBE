using Infrastructure.Utils;
using Model.Auth;
using Model.Requests;
using Model.Response;
using Repository.User;
using Service.Common;

namespace Service.User;

public class UserService : IUserService
{
    private readonly IUnitOfWorkExecutor _unitOfWorkExecutor;
    private readonly IUserRepo _userRepo;

    public UserService(
        IUnitOfWorkExecutor unitOfWorkExecutor,
        IUserRepo userRepo)
    {
        _unitOfWorkExecutor = unitOfWorkExecutor;
        _userRepo = userRepo;
    }

    public Task<ListResponseModel<UserModel>> GetListAsync(
        BaseListRequestModel request,
        CancellationToken cancellationToken = default)
    {
        return _unitOfWorkExecutor.ExecuteReadOnlyAsync(nameof(GetListAsync), async uow =>
        {
            var result = await _userRepo.GetListAsync(uow, request, cancellationToken);

            return new ListResponseModel<UserModel>
            {
                Status = true,
                List = result.List.Select(u => new UserModel
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    IsActive = u.IsActive
                }).ToList(),
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalPages = FilterUtil.PaginationTotalPages(result.TotalCount, result.PageSize)
            };
        }, cancellationToken);
    }
}
