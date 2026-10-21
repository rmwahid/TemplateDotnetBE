namespace Infrastructure.Security;

public interface IIdempotencyRequestHasher
{
    string Hash(string method, string path, string body);
}
