using Microsoft.AspNetCore.Authentication;

namespace CNM.Application.Auth
{
    public class CustomAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public const string AuthenticationScheme = "CustomAuthentication";
    }
}
