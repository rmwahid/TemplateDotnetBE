using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Migrations.Core.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Username).IsUnique();
        entity.HasIndex(e => e.Email).IsUnique();
        entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
        entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
    }
}
