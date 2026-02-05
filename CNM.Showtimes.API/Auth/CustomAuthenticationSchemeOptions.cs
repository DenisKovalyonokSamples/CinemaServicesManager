using Microsoft.AspNetCore.Authentication;

namespace CNM.Showtimes.API.Auth
{
    public class CustomAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public const string AuthenticationScheme = "CustomAuthentication";
    }
}
