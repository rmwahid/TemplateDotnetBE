namespace Service.Common;

public interface IIdempotencyContext
{
    bool IsPresent { get; }
    string Key { get; }
    string Method { get; }
    string Path { get; }
    string OwnerScope { get; }
    string RequestHash { get; }
    string Operation { get; }

    void Set(
        string key,
        string method,
        string path,
        string ownerScope,
        string requestHash);
}
