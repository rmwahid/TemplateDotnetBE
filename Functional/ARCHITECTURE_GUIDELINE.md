# Architecture Guideline — Functional Project

This document explains the project layout, responsibilities, and rules for keeping the codebase consistent.

## Project Responsibilities

```
API/                   → HTTP layer, controllers, middleware, DI registration
API/Common/            → API-specific middleware concerns (ErrorResponse, exceptions)
                         and shared HTTP shapes
Constants/             → Shared constants (no project dependencies)
Entities/              → Database schema classes, referenced only by Migrations.Core and Repository
Infrastructure/        → Connection providers, unit-of-work abstractions, hashers, utils
Migrations.Core/       → Shared EF Core DbContext and entity configurations
Migrations.PostgreSQL/ → PostgreSQL-specific migrations and design-time factory
Migrations.MySQL/      → MySQL-specific migrations and design-time factory
Model/                 → All runtime DTOs: requests, responses, repository models,
                         report models; shared shapes live in Requests/, Response/,
                         and Shared/
Query/                 → Visible read SQL used by Dapper repositories
Repository/            → One folder per feature: reads via Dapper, writes via EF Core,
                         and that feature's Model-to-Entity mapping
Repository/Common/     → Repository-wide helpers shared by all features
Service/               → Business logic, the use-case transaction boundary, and
                         mapping Model DTOs to response DTOs
Service/Common/        → Use-case plumbing shared by all services (executor,
                         idempotency context, metrics)
UnitOfWork/            → One connection and, when transactional, one transaction per
                         use case, shared by Dapper reads and EF writes
Logger/                → Logging abstractions and implementations
```

Two axes, kept apart:

- **Feature projects** — `API`, `Service`, `Repository`, `Model` — are organised by
  feature. Each entity gets one folder, holding its interface, implementation, and
  mapping together. Cross-cutting code shared by features lives in that project's
  `Common/` folder (`Shared/` in Model); it follows the same dependency rules as the
  rest of the project. The existing shared folders (`API/Common`, `Model/Requests`,
  `Model/Response`, `Model/Shared`, `Repository/Common`, `Service/Common`) are part of
  this rule, not exceptions to it.
- **Functionality projects** — `Infrastructure`, `UnitOfWork`, `Logger`, `Constants`
  — are organised by what they do, and know nothing about features.

## The Golden Rule

> **`Service` and `API` must never use `Entities` classes directly.**
>
> `Repository` and `Migrations.Core` are the only projects that reference `Entities`.
> `Repository` needs them because an EF write requires an entity instance, and it is
> where the Model-to-Entity mapping lives — kept `internal` so a tracked entity
> cannot be returned from a query. Every other project is kept away from `Entities`
> by its project references, which makes a violation a compile error rather than a
> review comment. Do not "just add a reference" to get an entity type into a service.
>
> The `Entities` reference in `Repository` and `Migrations.Core` is marked
> `PrivateAssets="compile"`. Both projects compile against entities; their consumers
> receive only the runtime asset, so `using Entities` in `Service` or `API` fails to
> compile while `Entities.dll` is still copied to the output.
>
> That compile-time boundary covers `Entities`. `Service` and `API` still receive
> `Query` and `Migrations.Core` transitively through `Repository`; those edges are not
> hidden. If a strict boundary is wanted for them too, add
> `PrivateAssets="compile"` to the relevant project references and add explicit
> references where required (for example, `API` needs `Migrations.Core` for DbContext
> registration).

At runtime, Dapper maps query results directly to `Model` DTOs for reads, and EF maps
`Model` DTOs to `Entities` for writes. No entity ever leaves `Repository`.

| Layer | May reference Entities? | May reference Model? | May reference Constants? |
|---|---|---|---|
| API | No | Yes | Yes |
| Service | No | Yes | Yes |
| **Repository** | **Yes** — writes need entity instances | Yes | Yes |
| Query | No | Yes | Yes |
| Model | No | Yes | Yes |
| Infrastructure | No | Yes | Yes |
| UnitOfWork | No | No | No |
| Constants | No | No | No |
| Entities | Yes (for constants) | No | Yes |

Query uses Model for typed parameters such as `BaseListRequestModel`; it never uses
`Entities`.

## Request & Response Models

### Request Suffixes

| Operation | Suffix | Example |
|---|---|---|
| List / Filter | `ListRequestModel` | `TodoItemListRequestModel` |
| Create | `CreateRequestModel` | `TodoItemCreateRequestModel` |
| Update | `UpdateRequestModel` | `TodoItemUpdateRequestModel` |
| Delete | `DeleteRequestModel` (or `int id` in route) | `TodoItemDeleteRequestModel` |
| Auth | `Request` | `LoginRequest`, `RegisterRequest` |

### Response Suffixes

| Shape | Suffix | Example |
|---|---|---|
| Base | `ResponseModel` | `BaseResponseModel` |
| List | `ListResponseModel<T>` | `ListResponseModel<TodoItemListModel>` |
| Single data | `DataResponseModel<T>` | `DataResponseModel<AuthResponse>` |
| Submit | `SubmitResponseModel` | `SubmitResponseModel` |
| Auth payload | `Response` | `AuthResponse` |

### Response Rules

1. Do not put feature-specific filters in `BaseListRequestModel`. Extend it.
2. Always use `BaseResponseModel` as the root for controller responses. The one
   exception is `API/Common/Response/ErrorResponse`, the middleware-level error shape,
   which is deliberately separate.
3. Use generic response models when possible.
4. Create composite detail models for `GET {id}` endpoints that return related data.
5. Keep dropdown/filter options in response models, not request models.

## Repository & Dapper Rules

1. **Repositories return Model DTOs**, never `Entities`.
2. **Service maps Model DTOs to response DTOs**.
3. **All read SQL stays in `Query/`** classes so it is visible and easy to review.
4. **Sort columns come from a whitelist.** A column name cannot be a bound
   parameter, so `SortBy` from a request is matched against a list inside the
   query class before it reaches `ORDER BY`. Every other caller-supplied value
   uses a parameter, never string interpolation.
5. **Writes go through EF, not hand-written SQL.** A hand-written write is also what
   forced provider-specific SQL such as `RETURNING` into queries shared by
   PostgreSQL and MySQL.
6. **Repositories never open a transaction.** They receive a unit of work and use the
   connection and transaction it carries. See below.
7. **Provider-specific write failures are translated in `Repository`.** A unique
   index violation becomes `UniqueConstraintException` and an optimistic-concurrency
   loss becomes `ConcurrencyConflictException`, so `Service` never references EF Core
   or a database driver.
8. **Paginated reads return `PagedResultModel<T>`.** The repository supplies the page
   and the total count; the service computes `TotalPages` and builds the response
   model.

## Read and Write Paths

Reads and writes are served differently, on purpose.

**Reads — Dapper, with the SQL written out in `Query/`.** Reads are where the
complexity lives: joins, grouping, reporting. Keeping them as explicit SQL means a
query can be read, reviewed, pasted into a database console, and tuned against an
index without guessing what an ORM generated. Dapper also skips change tracking,
so materialising results costs less.

**Writes — Entity Framework Core.** EF is used for writes because it handles the
parts that are easy to get wrong: it reads back database-generated keys, orders
inserts across related tables, and wraps the change in a transaction. A write is
rarely the place you need hand-tuned SQL, so little is given up and a lot of
correctness is gained. For many rows at once, `AddRange` plus a single
`SaveChanges` also sends the batch in far fewer round trips than inserting row by
row.

This split is a recognised pattern — sometimes called simplified CQRS: EF Core for
the write model, Dapper for queries.

### One unit of work per use case

Both paths have to share a transaction whenever an operation reads in order to
decide what to write. Checking stock and then decrementing it in two separate
transactions is how stock goes negative, and creating an order, its lines, and a
payment in three separate transactions is how half-written orders happen.

So `Service` uses `IUnitOfWorkExecutor` and gives it the use case as a delegate:

```csharp
return await _unitOfWorkExecutor.ExecuteAsync(nameof(CreateOrderAsync), async uow =>
{
    var product = await _productRepo.GetForUpdateAsync(uow, id);   // Dapper, inside the transaction
    if (product.Stock < qty)
    {
        return result;                                             // Status=false → rollback
    }

    await _orderRepo.CreateAsync(uow, order);                      // EF, same transaction
    return result;                                                 // Status=true → commit
});
```

`GetForUpdateAsync` is illustrative. In this codebase, most writes use conditional
updates with the ownership or state in the `WHERE` clause, and refresh-token rotation
uses `LockFamilyAsync` (`SELECT ... FOR UPDATE`) on the family row. Use whichever fits
the operation; the rule is that the decision read and the write share one transaction.

- `ExecuteAsync` opens one connection and one transaction, invokes the delegate, then
  commits when `result.Status` is `true` and rolls back when it is `false`.
- `ExecuteReadOnlyAsync` opens only a connection and never commits. Use it when
  nothing is written; wrapping pure reads in a transaction buys nothing.
- The executor owns `BeginAsync`, `CommitAsync`, and `RollbackAsync` for normal use
  cases. A service does not manage the transaction, except for the explicit
  security-side-effect commit described under Failure handling.
- An exception leaves the delegate, is rolled back by the executor, and is rethrown.
  `ErrorHandlerMiddleware` is the only place that logs it at Error.
- If a transactional scope is disposed without either commit or rollback, that is a
  lifecycle bug: `UnitOfWorkScope` rolls back and logs Error. Business failures do not
  reach that path because the executor rolls them back explicitly.
- `IUnitOfWorkExecutionStrategy` wraps the whole begin/execute/commit sequence, so
  enabling `Database:EnableRetryOnFailure` retries a complete transaction rather than
  a single command. Retry is off by default: a commit can succeed before its
  acknowledgement is lost, so enable it only when writes are idempotent or protected
  by an outbox.

The `DbContext` used by writes is scoped to the request while the unit of work is
scoped to the operation, so the unit of work clears the change tracker when it ends.
Otherwise a second operation in the same request would inherit entities tracked by
the first.

### Failure handling and tracing

- **Expected failures** — validation, business rules, not found — return a result
  object (`SubmitResponseModel`, `DataResponseModel`) with `Status = false`. They are
  part of the contract, and one response can carry several at once. The executor
  rolls back and logs at Information/Debug; this is not an error.
- **A failed use case may persist a security side effect.** For example, refresh-token
  reuse detection revokes a token family and then returns a failed result. In that
  case the service commits explicitly; the executor sees `IUnitOfWork.IsCommitted` and
  leaves it committed. This is reserved for security or audit side effects, not
  ordinary business writes.
- **Unexpected failures** throw. The executor rolls back and rethrows;
  `ErrorHandlerMiddleware` logs them with the stack trace and a trace id, and returns
  500.
- **Known provider failures** are translated in `Repository`: a unique index becomes
  `UniqueConstraintException`, an optimistic-concurrency loss becomes
  `ConcurrencyConflictException`. Services can map the former to a business result;
  the API maps both to 409.
- **Never catch `Exception` in a service just to turn it into a result.** That is what
  made a database outage look like `400 Invalid request parameters` and lost the
  stack trace.
- **500 responses never carry `e.Message`.** It can name tables, queries, or paths.
- A client disconnect (`OperationCanceledException` with `RequestAborted`) is not an
  application error: the API returns 499 without a body and does not log Error.

### Idempotency and retry

Only `POST` requests that create state accept an `Idempotency-Key` header. The shape
follows the widely used Stripe contract:

- The client generates a unique key (a UUID or equivalent random value, at most 255
  characters). The key must not contain sensitive data.
- The server stores the key, a fingerprint of the request, and the successful
  response. Records are retained for 24 hours.
- A retry with the same key and the same request returns the stored response.
- The same key with a different request returns 409 Conflict.
- The unique index on the key serialises concurrent requests; the loser reads the
  completed record and replays it.
- The record is inserted inside the same transaction as the business write. A
  business failure or exception rolls the record back, so the operation can be
  retried with the same key.
- `PUT` and `DELETE` are idempotent by HTTP semantics and ignore the header. `GET`
  ignores it as well.
- Request fingerprints are HMAC-SHA256 over method, path, and body. The HMAC key is
  `Security:IdempotencyHmacKey`, supplied through an environment variable or a secret
  manager. In Development a random key is generated when it is absent, and the app
  logs a warning.
- Expired records are deleted daily by `IdempotencyCleanupJob`.

Auth endpoints do not use idempotency keys. RFC 9700 requires refresh token rotation
for public clients and reuse detection; the correct behaviour is to rotate the token
and treat reuse of an old token as potential theft, not to replay a stored token
response. Login starts a new session and is not idempotent. `Login`, `RefreshToken`,
and `RevokeToken` therefore opt out of idempotency and automatic retry. A login
creates a new token family, so other devices stay signed in. A refresh token reused
within `RefreshToken:ReuseGracePeriodSeconds` replays its replacement; reuse outside
that window revokes every active token in that family only. Both rotation and family
revocation lock the family row with `SELECT ... FOR UPDATE`, so a concurrent refresh
cannot insert a token past a family revocation.

Retry is off by default (`Database:EnableRetryOnFailure`). When enabled, only reads
and keyed `POST` requests are retried; unkeyed writes and auth endpoints are not.
EF Core requires the whole transaction to run inside the provider execution strategy,
which `IUnitOfWorkExecutionStrategy` does. A commit can succeed before its
acknowledgement is lost; the idempotency record is what lets the retry replay instead
of duplicating the write.

### Why "many inserts" favours EF

`AddRange()` followed by one `SaveChanges()` sends the batch in fewer round trips:
EF groups statements into batches (up to 42 per batch on SQL Server by default;
thresholds differ per provider). Looping one INSERT per row through Dapper issues
one round trip per row unless you wrap the loop in your own transaction. For
genuinely large loads, in the hundreds of thousands of rows, neither is right: use
the provider's bulk mechanism (`COPY` on PostgreSQL, `SqlBulkCopy`,
`MySqlBulkCopy`).

## Migrations

- `Migrations.Core` contains the shared `ApplicationDbContext` and `IEntityTypeConfiguration<T>` classes.
- `Migrations.PostgreSQL` and `Migrations.MySQL` contain provider-specific migrations and design-time factories.
- The design-time connection string is read from `ConnectionStrings:DefaultConnection`
  or `Database:ConnectionString` (via `appsettings.json`, user secrets, or the
  `ConnectionStrings__DefaultConnection` / `Database__ConnectionString` environment
  variables).

## Where to Put Code

1. **Mapped to a database table** → `Entities/`.
2. **API contract or runtime DTO** → `Model/` (`Requests/`, `Response/`, `Shared/`,
   or the feature folder).
3. **Shared constant** → `Constants/`.
4. **Middleware-level response or exception type** → `API/Common/`.
5. **HTTP pipeline class** (controller, middleware, filter, background job) →
   `API/Controllers/`, `API/Services/`, or `API/Jobs/`.
6. **Read SQL** → `Query/`.
7. **Feature data access or Model-to-Entity mapping** → `Repository/<Feature>/`;
   repository-wide helpers go in `Repository/Common/`.
8. **Business logic or response mapping** → `Service/<Feature>/`; use-case plumbing
   goes in `Service/Common/`.
9. **Connection/transaction orchestration** → `UnitOfWork/`; shared abstractions and
   utilities go in `Infrastructure/`.
10. **Logging abstractions or implementations** → `Logger/`.
11. **EF model or migrations** → `Migrations.Core/` or the provider-specific
    migrations project.

## Naming Conventions

| Type | Suffix | Example |
|---|---|---|
| Entity | None | `User`, `TodoItem` |
| Request DTO | `Request` / `RequestModel` | `RegisterRequest`, `TodoItemCreateRequestModel` |
| Response DTO | `ResponseModel` | `SubmitResponseModel`, `ListResponseModel<T>` |
| Auth payload | `Response` | `AuthResponse` |
| Internal repo model | `Model` | `UserAuthModel`, `TodoItemModel` |
| Report model | `ReportModel` | `SalesReportModel` |
| Constants | `Tables`, `Roles`, `ErrorCodes` | `Constants.Tables` |

## Summary

- **Entities** = DB schema: migrations and the EF write path, nothing else.
- **Migrations.Core** = shared EF Core context and entity configurations.
- **Migrations.PostgreSQL / Migrations.MySQL** = provider-specific migrations.
- **Model** = everything that moves at runtime.
- **Constants** = shared constants with no dependencies.
- **Infrastructure** = connection providers, unit-of-work abstractions, hashing, utilities.
- **API/Common** = API-specific middleware concerns.
- **Repository** = reads via Dapper, writes via EF, both on the caller's unit of work.
- **Service** = business logic, owns the use-case transaction boundary through
  `IUnitOfWorkExecutor`, maps Model DTOs to response DTOs.
- **Reports** use dedicated Model DTOs, never Entities.

Keep these boundaries strict and the project will remain easy to extend.

## References

- EF Core transactions — <https://learn.microsoft.com/en-us/ef/core/saving/transactions>
- EF Core connection resiliency (execution strategies and the commit ambiguity) —
  <https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency>
- ASP.NET Core logging levels —
  <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/>
- .NET exception best practices —
  <https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions>
- RFC 9700, Best Current Practice for OAuth 2.0 Security —
  <https://www.rfc-editor.org/rfc/rfc9700.html>
- Stripe idempotent requests — <https://docs.stripe.com/api/idempotent_requests>
- Unit of Work (Fowler) — <https://martinfowler.com/eaaCatalog/unitOfWork.html>
