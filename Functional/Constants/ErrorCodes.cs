namespace Constants;

public static class ErrorCodes
{
    public const string User_UsernameExists = "USER_USERNAME_EXISTS";
    public const string User_EmailExists = "USER_EMAIL_EXISTS";
    public const string User_InvalidCredentials = "USER_INVALID_CREDENTIALS";
    public const string User_NotFound = "USER_NOT_FOUND";
    public const string RefreshToken_Invalid = "REFRESH_TOKEN_INVALID";
    public const string RefreshToken_Expired = "REFRESH_TOKEN_EXPIRED";
    public const string RefreshToken_Revoked = "REFRESH_TOKEN_REVOKED";
    public const string TodoItem_NotFound = "TODO_ITEM_NOT_FOUND";
    public const string TodoItem_TitleRequired = "TODO_ITEM_TITLE_REQUIRED";
    public const string General_InvalidInput = "GENERAL_INVALID_INPUT";
    public const string Forbidden_InsufficientPermission = "FORBIDDEN_INSUFFICIENT_PERMISSION";
}
