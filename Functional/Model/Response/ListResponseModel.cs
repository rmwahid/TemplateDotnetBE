namespace Model.Response;

public class ListResponseModel<T> : BaseResponseModel
{
    public List<T> List { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
