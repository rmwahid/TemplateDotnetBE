using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Migrations.Core.Configurations;

public class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> entity)
    {
        entity.ToTable("todo_items");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
        entity.HasIndex(e => e.CreatedByUserId);
    }
}
