using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Migrations.Core.Configurations;

public class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> entity)
    {
        entity.ToTable("refresh_token_families");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UserId).IsRequired();
        entity.HasIndex(e => e.UserId);
    }
}
