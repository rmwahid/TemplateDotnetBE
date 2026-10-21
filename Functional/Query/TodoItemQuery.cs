using Constants;
using Model.Requests;

namespace Query;

public class TodoItemQuery
{
    /// <summary>
    /// Columns a caller may sort by, kept in step with the SELECT list below.
    ///
    /// A column name cannot be a bound parameter, so it has to be matched against
    /// a list rather than interpolated. The matched value comes from this array,
    /// never from the request.
    /// </summary>
    private static readonly string[] SortableColumns =
    {
        "Id", "Title", "Description", "IsCompleted", "CreatedByUserId", "CreatedAt", "UpdatedAt"
    };

    private static string ResolveSortColumn(string? sortBy) =>
        SortableColumns.FirstOrDefault(c => c.Equals(sortBy, StringComparison.OrdinalIgnoreCase))
        ?? "CreatedAt";

    public string GetById() => $"""
        SELECT Id, Title, Description, IsCompleted, CreatedByUserId, CreatedAt, UpdatedAt
        FROM {Tables.TodoItems}
        WHERE Id = @Id;
        """;

    public string GetListWithCount(BaseListRequestModel request, int createdByUserId)
    {
        var sortColumn = ResolveSortColumn(request.SortBy);
        var sortDirection = request.SortDescending ? "DESC" : "ASC";

        return $"""
            SELECT Id, Title, Description, IsCompleted, CreatedByUserId, CreatedAt, UpdatedAt
            FROM {Tables.TodoItems}
            WHERE CreatedByUserId = @CreatedByUserId
              AND (@SearchQuery IS NULL OR @SearchQuery = '' OR Title LIKE @SearchQuery)
            ORDER BY {sortColumn} {sortDirection}
            LIMIT @PageSize OFFSET @Offset;

            SELECT COUNT(Id)
            FROM {Tables.TodoItems}
            WHERE CreatedByUserId = @CreatedByUserId
              AND (@SearchQuery IS NULL OR @SearchQuery = '' OR Title LIKE @SearchQuery);
            """;
    }
}
