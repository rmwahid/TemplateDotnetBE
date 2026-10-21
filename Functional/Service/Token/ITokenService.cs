using Model.Auth;

namespace Service.Token;

public interface ITokenService
{
    string GenerateToken(UserAuthModel user);
}
