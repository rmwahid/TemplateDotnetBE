using Constants;

namespace Query;

public class IdempotencyQuery
{
    public string GetByKey() => $"""
        SELECT Id, Operation, OwnerScope, IdempotencyKey, RequestHash, ResponseBody, CreatedAt, ExpiresAt
        FROM {Tables.IdempotencyKeys}
        WHERE Operation = @Operation
          AND OwnerScope = @OwnerScope
          AND IdempotencyKey = @IdempotencyKey;
        """;
}
