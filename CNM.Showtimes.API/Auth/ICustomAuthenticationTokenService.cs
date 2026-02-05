using System.Security.Claims;

namespace CNM.Showtimes.API.Auth
{
    public interface ICustomAuthenticationTokenService
    {
        ClaimsPrincipal Read(string value);
    }
}
