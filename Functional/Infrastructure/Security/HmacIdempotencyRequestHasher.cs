using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Security;

/// <summary>
/// Fingerprints the request with HMAC-SHA256 instead of a plain hash. Request bodies
/// can contain credentials (registration), and a plain hash of a low-entropy password
/// would be open to offline guessing if the database leaked.
/// </summary>
public sealed class HmacIdempotencyRequestHasher : IIdempotencyRequestHasher
{
    private readonly byte[] _key;

    public HmacIdempotencyRequestHasher(IOptions<IdempotencyOptions> options)
    {
        _key = options.Value.Key;

        if (_key.Length < 32)
        {
            throw new InvalidOperationException(
                "Security:IdempotencyHmacKey must be a Base64-encoded key of at least 32 bytes.");
        }
    }

    public string Hash(string method, string path, string body)
    {
        var payload = $"{method}\n{path}\n{body}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(_key, bytes);
        return Convert.ToHexString(hash);
    }
}
