using Model.TodoItem;
using TodoItemEntity = Entities.TodoItem;

namespace Repository.TodoItem;

/// <summary>
/// Translates the todo Model into the entity EF writes. See <c>UserMapper</c> for why
/// this is internal, why it maps in one direction only, and why the alias is needed.
/// </summary>
internal static class TodoItemMapper
{
    public static TodoItemEntity ToEntity(TodoItemModel model) => new()
    {
        Title = model.Title,
        Description = model.Description,
        IsCompleted = model.IsCompleted,
        CreatedByUserId = model.CreatedByUserId,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt,
        // Id is not copied: the database generates it on insert. Updates are
        // set-based, so no entity is ever materialised for them.
    };
}
