using Model.Response;

namespace Model.Shared;

public static class SubmitResponseModelExtensions
{
    public static void AddError(this SubmitResponseModel result, string code, string message = "")
    {
        result.Status = false;
        result.Errors.Add(new ResponseErrorModel
        {
            Code = code,
            Message = message
        });
    }
}
