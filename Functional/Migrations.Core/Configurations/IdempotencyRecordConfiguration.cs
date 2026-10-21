using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Migrations.Core.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> entity)
    {
        entity.ToTable("idempotency_keys");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Operation).HasMaxLength(200).IsRequired();
        entity.Property(e => e.OwnerScope).HasMaxLength(100).IsRequired();
        entity.Property(e => e.IdempotencyKey).HasMaxLength(255).IsRequired();
        entity.Property(e => e.RequestHash).HasMaxLength(128).IsRequired();
        entity.Property(e => e.ResponseBody).IsRequired();
        entity.HasIndex(e => new { e.Operation, e.OwnerScope, e.IdempotencyKey }).IsUnique();
        entity.HasIndex(e => e.ExpiresAt);
    }
}
