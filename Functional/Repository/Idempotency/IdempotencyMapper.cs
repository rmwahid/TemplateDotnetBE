using Model.Idempotency;
using IdempotencyEntity = Entities.IdempotencyRecord;

namespace Repository.Idempotency;

internal static class IdempotencyMapper
{
    public static IdempotencyEntity ToEntity(IdempotencyRecordModel model) => new()
    {
        Operation = model.Operation,
        OwnerScope = model.OwnerScope,
        IdempotencyKey = model.IdempotencyKey,
        RequestHash = model.RequestHash,
        ResponseBody = model.ResponseBody,
        CreatedAt = model.CreatedAt,
        ExpiresAt = model.ExpiresAt
    };
}
