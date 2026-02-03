using System.Security.Claims;

namespace CNM.Application.Auth
{
    public interface ICustomAuthenticationTokenService
    {
        ClaimsPrincipal Read(string value);
    }
}