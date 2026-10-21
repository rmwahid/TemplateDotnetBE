namespace Model.Requests;

public class BaseListRequestModel
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SearchQuery { get; set; } = string.Empty;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}
