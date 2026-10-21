namespace Service.Common;

public sealed class IdempotencyContext : IIdempotencyContext
{
    public bool IsPresent { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string OwnerScope { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;

    public string Operation =>
        string.IsNullOrEmpty(Method) || string.IsNullOrEmpty(Path)
            ? string.Empty
            : $"{Method} {Path}";

    public void Set(
        string key,
        string method,
        string path,
        string ownerScope,
        string requestHash)
    {
        IsPresent = true;
        Key = key;
        Method = method;
        Path = path;
        OwnerScope = ownerScope;
        RequestHash = requestHash;
    }
}
