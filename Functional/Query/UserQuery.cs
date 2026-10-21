using Constants;
using Model.Requests;

namespace Query;

public class UserQuery
{
    /// <summary>
    /// Columns a caller may sort by, kept in step with the SELECT list below.
    /// The matched value comes from this array, never from the request.
    /// </summary>
    private static readonly string[] SortableColumns =
    {
        "Id", "Username", "Email", "Role", "CreatedAt", "IsActive"
    };

    private static string ResolveSortColumn(string? sortBy) =>
        SortableColumns.FirstOrDefault(c => c.Equals(sortBy, StringComparison.OrdinalIgnoreCase))
        ?? "CreatedAt";

    public string GetByUsername() => $"""
        SELECT Id, Username, Email, PasswordHash, Role, CreatedAt, IsActive
        FROM {Tables.Users}
        WHERE Username = @Username;
        """;

    public string GetById() => $"""
        SELECT Id, Username, Email, PasswordHash, Role, CreatedAt, IsActive
        FROM {Tables.Users}
        WHERE Id = @Id;
        """;

    public string CountByUsername() => $"""
        SELECT COUNT(Id)
        FROM {Tables.Users}
        WHERE Username = @Username;
        """;

    public string CountByEmail() => $"""
        SELECT COUNT(Id)
        FROM {Tables.Users}
        WHERE Email = @Email;
        """;

    public string GetListWithCount(BaseListRequestModel request)
    {
        var sortColumn = ResolveSortColumn(request.SortBy);
        var sortDirection = request.SortDescending ? "DESC" : "ASC";

        return $"""
            SELECT Id, Username, Email, PasswordHash, Role, CreatedAt, IsActive
            FROM {Tables.Users}
            WHERE (@SearchQuery IS NULL OR @SearchQuery = '' OR Username LIKE @SearchQuery OR Email LIKE @SearchQuery)
            ORDER BY {sortColumn} {sortDirection}
            LIMIT @PageSize OFFSET @Offset;

            SELECT COUNT(Id)
            FROM {Tables.Users}
            WHERE (@SearchQuery IS NULL OR @SearchQuery = '' OR Username LIKE @SearchQuery OR Email LIKE @SearchQuery);
            """;
    }
}
