namespace Model.Shared;

/// <summary>
/// Pagination result produced by a repository. It carries only data and paging
/// metadata; the service turns it into a response model.
/// </summary>
public class PagedResultModel<T>
{
    public List<T> List { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
