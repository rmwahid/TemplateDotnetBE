using Dapper;
using Infrastructure.Database;
using Infrastructure.Utils;
using Migrations.Core;
using Model.Auth;
using Model.Requests;
using Model.Shared;
using Query;
using Repository.Common;

namespace Repository.User;

public class UserRepo : IUserRepo
{
    private readonly UserQuery _query = new();
    private readonly ApplicationDbContext _db;

    public UserRepo(ApplicationDbContext db) => _db = db;

    // ── reads: Dapper, inside the caller's transaction ──

    public async Task<UserAuthModel?> GetByUsernameAsync(
        IUnitOfWork uow,
        string username,
        CancellationToken cancellationToken = default) =>
        await uow.Connection.QueryFirstOrDefaultAsync<UserAuthModel>(
            new CommandDefinition(
                _query.GetByUsername(),
                new { Username = username },
                uow.Transaction,
                cancellationToken: cancellationToken));

    public async Task<UserAuthModel?> GetByIdAsync(
        IUnitOfWork uow,
        int id,
        CancellationToken cancellationToken = default) =>
        await uow.Connection.QueryFirstOrDefaultAsync<UserAuthModel>(
            new CommandDefinition(
                _query.GetById(),
                new { Id = id },
                uow.Transaction,
                cancellationToken: cancellationToken));

    public async Task<int> CountByUsernameAsync(
        IUnitOfWork uow,
        string username,
        CancellationToken cancellationToken = default) =>
        await uow.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                _query.CountByUsername(),
                new { Username = username },
                uow.Transaction,
                cancellationToken: cancellationToken));

    public async Task<int> CountByEmailAsync(
        IUnitOfWork uow,
        string email,
        CancellationToken cancellationToken = default) =>
        await uow.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                _query.CountByEmail(),
                new { Email = email },
                uow.Transaction,
                cancellationToken: cancellationToken));

    public async Task<PagedResultModel<UserAuthModel>> GetListAsync(
        IUnitOfWork uow,
        BaseListRequestModel request,
        CancellationToken cancellationToken = default)
    {
        var sql = _query.GetListWithCount(request);
        var search = FilterUtil.FilterLike(request.SearchQuery);

        var command = new CommandDefinition(
            sql,
            new
            {
                SearchQuery = string.IsNullOrWhiteSpace(request.SearchQuery) ? null : search,
                PageSize = request.PageSize,
                Offset = FilterUtil.PaginationOffset(request.PageNumber, request.PageSize)
            },
            uow.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await uow.Connection.QueryMultipleAsync(command);

        var list = (await multi.ReadAsync<UserAuthModel>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();

        return new PagedResultModel<UserAuthModel>
        {
            List = list,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    // ── write: EF Core, enlisting the caller's transaction through the context ──

    public async Task<int> CreateAsync(
        IUnitOfWork uow,
        UserAuthModel user,
        CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionTranslator.ExecuteAsync(async () =>
        {
            var entity = UserMapper.ToEntity(user);
            _db.Users.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity.Id;
        });
    }
}
