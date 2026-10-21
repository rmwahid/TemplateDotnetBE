namespace Model.Requests.TodoItem;

public class TodoItemCreateRequestModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
