using System.Data.Common;
using System.Text.Json;
using Infrastructure.Database;
using Infrastructure.Exceptions;
using Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model.Idempotency;
using Model.Response;
using Repository.Idempotency;
using Service.Common;
using Xunit;

namespace Tests;

public class UnitOfWorkExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_commits_when_result_is_successful()
    {
        var uow = new FakeUnitOfWork();
        var executor = CreateExecutor(uow);

        var result = await executor.ExecuteAsync(
            "test",
            _ => Task.FromResult(new SubmitResponseModel { Status = true }));

        Assert.True(result.Status);
        Assert.Equal(1, uow.CommitCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_rolls_back_when_result_is_a_business_failure()
    {
        var uow = new FakeUnitOfWork();
        var executor = CreateExecutor(uow);

        var result = await executor.ExecuteAsync(
            "test",
            _ => Task.FromResult(new SubmitResponseModel { Status = false }));

        Assert.False(result.Status);
        Assert.Equal(0, uow.CommitCalls);
        Assert.Equal(1, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_rolls_back_and_rethrows_when_operation_throws()
    {
        var uow = new FakeUnitOfWork();
        var executor = CreateExecutor(uow);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync<SubmitResponseModel>(
                "test",
                _ => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", exception.Message);
        Assert.Equal(0, uow.CommitCalls);
        Assert.Equal(1, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_rolls_back_when_cancelled()
    {
        var uow = new FakeUnitOfWork();
        var executor = CreateExecutor(uow);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync<SubmitResponseModel>(
                "test",
                _ => throw new OperationCanceledException()));

        Assert.Equal(0, uow.CommitCalls);
        Assert.Equal(1, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_rolls_back_when_commit_fails()
    {
        var uow = new FakeUnitOfWork { CommitThrows = true };
        var executor = CreateExecutor(uow);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                "test",
                _ => Task.FromResult(new SubmitResponseModel { Status = true })));

        Assert.Equal("commit failed", exception.Message);
        Assert.Equal(1, uow.CommitCalls);
        Assert.Equal(1, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteReadOnlyAsync_does_not_commit_or_roll_back()
    {
        var uow = new FakeUnitOfWork();
        var executor = CreateExecutor(uow);

        var result = await executor.ExecuteReadOnlyAsync(
            "test",
            _ => Task.FromResult(new SubmitResponseModel { Status = true }));

        Assert.True(result.Status);
        Assert.Equal(0, uow.CommitCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_keeps_commit_when_operation_commits_a_failure_side_effect()
    {
        var uow = new FakeUnitOfWork();
        var executor = CreateExecutor(uow);

        var result = await executor.ExecuteAsync("test", async u =>
        {
            await u.CommitAsync();
            return new SubmitResponseModel { Status = false };
        });

        Assert.False(result.Status);
        Assert.Equal(1, uow.CommitCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_replays_stored_response_without_running_operation()
    {
        var uow = new FakeUnitOfWork();
        var context = new FakeIdempotencyContext("key", "hash");
        var repo = new FakeIdempotencyRepo
        {
            Existing = new IdempotencyRecordModel
            {
                Id = 7,
                RequestHash = "hash",
                ResponseBody = JsonSerializer.Serialize(new SubmitResponseModel
                {
                    Status = true,
                    Message = "replayed"
                }),
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };
        var executor = CreateExecutor(uow, repo, context);
        var operationCalls = 0;

        var result = await executor.ExecuteAsync("test", _ =>
        {
            operationCalls++;
            return Task.FromResult(new SubmitResponseModel { Status = true });
        });

        Assert.Equal("replayed", result.Message);
        Assert.Equal(0, operationCalls);
        Assert.Equal(0, uow.CommitCalls);
        Assert.Equal(1, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_same_key_with_different_request()
    {
        var uow = new FakeUnitOfWork();
        var context = new FakeIdempotencyContext("key", "new-hash");
        var repo = new FakeIdempotencyRepo
        {
            Existing = new IdempotencyRecordModel
            {
                Id = 7,
                RequestHash = "old-hash",
                ResponseBody = "{}",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };
        var executor = CreateExecutor(uow, repo, context);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            executor.ExecuteAsync(
                "test",
                _ => Task.FromResult(new SubmitResponseModel { Status = true })));
    }

    [Fact]
    public async Task ExecuteAsync_stores_completed_response_for_new_key()
    {
        var uow = new FakeUnitOfWork();
        var context = new FakeIdempotencyContext("key", "hash");
        var repo = new FakeIdempotencyRepo();
        var executor = CreateExecutor(uow, repo, context);

        var result = await executor.ExecuteAsync(
            "test",
            _ => Task.FromResult(new SubmitResponseModel
            {
                Status = true,
                Message = "created"
            }));

        Assert.True(result.Status);
        Assert.Single(repo.Added);
        Assert.Single(repo.Completed);
        Assert.Equal(1, uow.CommitCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_store_business_failure()
    {
        var uow = new FakeUnitOfWork();
        var context = new FakeIdempotencyContext("key", "hash");
        var repo = new FakeIdempotencyRepo();
        var executor = CreateExecutor(uow, repo, context);

        var result = await executor.ExecuteAsync(
            "test",
            _ => Task.FromResult(new SubmitResponseModel { Status = false }));

        Assert.False(result.Status);
        Assert.Single(repo.Added);
        Assert.Empty(repo.Completed);
        Assert.Equal(0, uow.CommitCalls);
        Assert.Equal(1, uow.RollbackCalls);
    }

    private static UnitOfWorkExecutor CreateExecutor(
        FakeUnitOfWork uow,
        FakeIdempotencyRepo? repo = null,
        FakeIdempotencyContext? context = null)
    {
        return new UnitOfWorkExecutor(
            new FakeUnitOfWorkFactory(uow),
            new FakeExecutionStrategy(),
            repo ?? new FakeIdempotencyRepo(),
            context ?? new FakeIdempotencyContext(),
            Options.Create(new IdempotencyOptions
            {
                Key = new byte[32],
                RecordLifetime = TimeSpan.FromHours(24)
            }),
            NullLogger<UnitOfWorkExecutor>.Instance);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public DbConnection Connection => null!;

        public DbTransaction? Transaction => null;

        public bool IsCommitted { get; private set; }

        public bool CommitThrows { get; init; }

        public int CommitCalls { get; private set; }

        public int RollbackCalls { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalls++;

            if (CommitThrows)
            {
                throw new InvalidOperationException("commit failed");
            }

            IsCommitted = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly FakeUnitOfWork _uow;

        public FakeUnitOfWorkFactory(FakeUnitOfWork uow) => _uow = uow;

        public Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWork>(_uow);

        public Task<IUnitOfWork> BeginReadOnlyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWork>(_uow);
    }

    private sealed class FakeExecutionStrategy : IUnitOfWorkExecutionStrategy
    {
        public Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation();
        }

        public Task<T> ExecuteNoRetryAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation();
        }
    }

    private sealed class FakeIdempotencyRepo : IIdempotencyRepo
    {
        public IdempotencyRecordModel? Existing { get; set; }

        public List<IdempotencyRecordModel> Added { get; } = new();

        public List<int> Completed { get; } = new();

        public Task<IdempotencyRecordModel?> FindAsync(
            IUnitOfWork uow,
            string operation,
            string ownerScope,
            string key,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Existing);
        }

        public Task<int> AddAsync(
            IUnitOfWork uow,
            IdempotencyRecordModel record,
            CancellationToken cancellationToken = default)
        {
            record.Id = Added.Count + 1;
            Added.Add(record);
            return Task.FromResult(record.Id);
        }

        public Task CompleteAsync(
            IUnitOfWork uow,
            int id,
            string responseBody,
            CancellationToken cancellationToken = default)
        {
            Completed.Add(id);
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredAsync(
            IUnitOfWork uow,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeIdempotencyContext : IIdempotencyContext
    {
        public FakeIdempotencyContext()
        {
        }

        public FakeIdempotencyContext(string key, string requestHash)
        {
            Set(key, "POST", "/api/test", "user", requestHash);
        }

        public bool IsPresent { get; private set; }
        public string Key { get; private set; } = string.Empty;
        public string Method { get; private set; } = string.Empty;
        public string Path { get; private set; } = string.Empty;
        public string OwnerScope { get; private set; } = string.Empty;
        public string RequestHash { get; private set; } = string.Empty;

        public string Operation =>
            string.IsNullOrEmpty(Method) || string.IsNullOrEmpty(Path)
                ? string.Empty
                : $"{Method} {Path}";

        public void Set(
            string key,
            string method,
            string path,
            string ownerScope,
            string requestHash)
        {
            IsPresent = true;
            Key = key;
            Method = method;
            Path = path;
            OwnerScope = ownerScope;
            RequestHash = requestHash;
        }
    }
}
