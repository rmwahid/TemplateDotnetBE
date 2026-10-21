using Infrastructure.Database;
using Model.Auth;
using Model.Requests;
using Model.Shared;

namespace Repository.User;

/// <summary>
/// Both sides of the user store: Dapper for reads, EF Core for writes, on the same
/// unit of work. See <c>ITodoItemRepo</c> for the reasoning behind the merged shape.
/// </summary>
public interface IUserRepo
{
    Task<UserAuthModel?> GetByUsernameAsync(
        IUnitOfWork uow,
        string username,
        CancellationToken cancellationToken = default);

    Task<UserAuthModel?> GetByIdAsync(
        IUnitOfWork uow,
        int id,
        CancellationToken cancellationToken = default);

    Task<int> CountByUsernameAsync(
        IUnitOfWork uow,
        string username,
        CancellationToken cancellationToken = default);

    Task<int> CountByEmailAsync(
        IUnitOfWork uow,
        string email,
        CancellationToken cancellationToken = default);

    Task<PagedResultModel<UserAuthModel>> GetListAsync(
        IUnitOfWork uow,
        BaseListRequestModel request,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts the user and returns the identifier the database generated.</summary>
    Task<int> CreateAsync(
        IUnitOfWork uow,
        UserAuthModel user,
        CancellationToken cancellationToken = default);
}
