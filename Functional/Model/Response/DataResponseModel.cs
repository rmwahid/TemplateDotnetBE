namespace Model.Response;

public class DataResponseModel<T> : BaseResponseModel
{
    public T? Data { get; set; }
}
