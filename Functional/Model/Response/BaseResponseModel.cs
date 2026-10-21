namespace Model.Response;

public class BaseResponseModel
{
    public bool Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ResponseErrorModel> Errors { get; set; } = new();
}
