using Infrastructure.Database;
using Microsoft.Extensions.Options;
using Model.Auth;
using Model.Requests;
using Model.Response;
using Model.Shared;
using Repository.RefreshToken;
using Repository.User;
using Service.Common;
using Service.RefreshToken;
using Service.Token;
using Xunit;

namespace Tests;

public class RefreshTokenServiceTests
{
    [Fact]
    public async Task RefreshAsync_rotates_active_token_and_creates_replacement()
    {
        var repo = new FakeRefreshTokenRepo();
        var original = NewToken("old", userId: 1);
        repo.Tokens["old"] = original;
        var service = CreateService(repo, NewUser(1));

        var result = await service.RefreshAsync("old");

        Assert.True(result.Status);
        Assert.NotNull(result.Data);
        Assert.NotEqual("old", result.Data!.RefreshToken);
        Assert.NotNull(original.RevokedAt);
        Assert.Equal(result.Data.RefreshToken, original.ReplacedByToken);
        Assert.True(repo.Tokens.ContainsKey(result.Data.RefreshToken));
        Assert.Equal(1, repo.CreateCalls);
    }

    [Fact]
    public async Task RefreshAsync_replays_replacement_within_grace_period()
    {
        var repo = new FakeRefreshTokenRepo();
        var original = NewToken("old", userId: 1);
        original.RevokedAt = DateTime.UtcNow.AddSeconds(-1);
        original.ReplacedByToken = "new";
        repo.Tokens["old"] = original;
        repo.Tokens["new"] = NewToken("new", userId: 1);
        var service = CreateService(repo, NewUser(1));

        var result = await service.RefreshAsync("old");

        Assert.True(result.Status);
        Assert.Equal("new", result.Data!.RefreshToken);
        Assert.Equal(0, repo.CreateCalls);
        Assert.Equal(0, repo.RevokeFamilyCalls);
    }

    [Fact]
    public async Task RefreshAsync_revokes_family_on_reuse_outside_grace_period()
    {
        var repo = new FakeRefreshTokenRepo();
        var original = NewToken("old", userId: 1, familyId: 1);
        original.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        original.ReplacedByToken = "new";
        repo.Tokens["old"] = original;
        repo.Tokens["new"] = NewToken("new", userId: 1, familyId: 1);
        repo.Tokens["other-device"] = NewToken("other-device", userId: 1, familyId: 2);
        var service = CreateService(repo, NewUser(1));

        var result = await service.RefreshAsync("old");

        Assert.False(result.Status);
        Assert.Equal(1, repo.RevokeFamilyCalls);
        Assert.NotNull(repo.Tokens["new"].RevokedAt);
        Assert.Null(repo.Tokens["other-device"].RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_returns_invalid_for_unknown_token()
    {
        var repo = new FakeRefreshTokenRepo();
        var service = CreateService(repo, NewUser(1));

        var result = await service.RefreshAsync("missing");

        Assert.False(result.Status);
        Assert.Equal(0, repo.RevokeFamilyCalls);
        Assert.Equal(0, repo.LockFamilyCalls);
    }

    [Fact]
    public async Task CreateSessionAsync_creates_a_new_family_and_token()
    {
        var repo = new FakeRefreshTokenRepo { NextFamilyId = 7 };
        var service = CreateService(repo, NewUser(1));

        var token = await service.CreateSessionAsync(new FakeUnitOfWork(), 1);

        Assert.Equal(7, token.FamilyId);
        Assert.Equal(1, repo.CreateFamilyCalls);
        Assert.Equal(1, repo.CreateCalls);
        Assert.True(repo.Tokens.ContainsKey(token.Token));
    }

    private static RefreshTokenService CreateService(
        FakeRefreshTokenRepo repo,
        UserAuthModel user)
    {
        return new RefreshTokenService(
            new FakeUnitOfWorkExecutor(new FakeUnitOfWork()),
            repo,
            new FakeUserRepo(user),
            new FakeTokenService(),
            Options.Create(new RefreshTokenConfig
            {
                LifetimeDays = 7,
                ReuseGracePeriodSeconds = 30
            }));
    }

    private static RefreshTokenModel NewToken(
        string token,
        int userId,
        int familyId = 1) => new()
    {
        UserId = userId,
        FamilyId = familyId,
        Token = token,
        Expires = DateTime.UtcNow.AddDays(1),
        CreatedAt = DateTime.UtcNow.AddDays(-1)
    };

    private static UserAuthModel NewUser(int id) => new()
    {
        Id = id,
        Username = $"user{id}",
        Email = $"user{id}@example.com",
        Role = "User",
        IsActive = true
    };

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public System.Data.Common.DbConnection Connection => null!;
        public System.Data.Common.DbTransaction? Transaction => null;
        public bool IsCommitted => false;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeUnitOfWorkExecutor : IUnitOfWorkExecutor
    {
        private readonly IUnitOfWork _uow;

        public FakeUnitOfWorkExecutor(IUnitOfWork uow) => _uow = uow;

        public Task<T> ExecuteAsync<T>(
            string operationName,
            Func<IUnitOfWork, Task<T>> operation,
            CancellationToken cancellationToken = default,
            bool useIdempotency = true)
            where T : BaseResponseModel
        {
            return operation(_uow);
        }

        public Task<T> ExecuteReadOnlyAsync<T>(
            string operationName,
            Func<IUnitOfWork, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(_uow);
        }
    }

    private sealed class FakeRefreshTokenRepo : IRefreshTokenRepo
    {
        public Dictionary<string, RefreshTokenModel> Tokens { get; } = new(StringComparer.Ordinal);
        public int CreateCalls { get; private set; }
        public int CreateFamilyCalls { get; private set; }
        public int LockFamilyCalls { get; private set; }
        public int RevokeFamilyCalls { get; private set; }
        public int NextFamilyId { get; set; } = 1;

        public Task<RefreshTokenModel?> GetByTokenAsync(
            IUnitOfWork uow,
            string token,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens.GetValueOrDefault(token));

        public Task<int> CreateFamilyAsync(
            IUnitOfWork uow,
            int userId,
            CancellationToken cancellationToken = default)
        {
            CreateFamilyCalls++;
            return Task.FromResult(NextFamilyId);
        }

        public Task<bool> LockFamilyAsync(
            IUnitOfWork uow,
            int familyId,
            CancellationToken cancellationToken = default)
        {
            LockFamilyCalls++;
            return Task.FromResult(true);
        }

        public Task<bool> TryRotateAsync(
            IUnitOfWork uow,
            string token,
            DateTime revokedAt,
            string replacementToken,
            CancellationToken cancellationToken = default)
        {
            if (!Tokens.TryGetValue(token, out var current) || current.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            current.RevokedAt = revokedAt;
            current.ReplacedByToken = replacementToken;
            return Task.FromResult(true);
        }

        public Task CreateAsync(
            IUnitOfWork uow,
            RefreshTokenModel token,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            Tokens[token.Token] = token;
            return Task.CompletedTask;
        }

        public Task<int> RevokeFamilyAsync(
            IUnitOfWork uow,
            int familyId,
            DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            RevokeFamilyCalls++;
            var count = 0;

            foreach (var token in Tokens.Values)
            {
                if (token.FamilyId == familyId && token.RevokedAt is null)
                {
                    token.RevokedAt = revokedAt;
                    count++;
                }
            }

            return Task.FromResult(count);
        }

        public Task<bool> RevokeAsync(
            IUnitOfWork uow,
            string token,
            DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            if (!Tokens.TryGetValue(token, out var current) || current.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            current.RevokedAt = revokedAt;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeUserRepo : IUserRepo
    {
        private readonly UserAuthModel _user;

        public FakeUserRepo(UserAuthModel user) => _user = user;

        public Task<UserAuthModel?> GetByIdAsync(
            IUnitOfWork uow,
            int id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAuthModel?>(id == _user.Id ? _user : null);

        public Task<UserAuthModel?> GetByUsernameAsync(
            IUnitOfWork uow,
            string username,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountByUsernameAsync(
            IUnitOfWork uow,
            string username,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountByEmailAsync(
            IUnitOfWork uow,
            string email,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PagedResultModel<UserAuthModel>> GetListAsync(
            IUnitOfWork uow,
            BaseListRequestModel request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CreateAsync(
            IUnitOfWork uow,
            UserAuthModel user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateToken(UserAuthModel user) => $"access-{user.Id}";
    }
}
