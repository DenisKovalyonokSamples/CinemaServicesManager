using System.Collections.Generic;
using System.Security.Claims;

namespace CNM.Application.Auth
{
    public class CustomAuthenticationTokenService : ICustomAuthenticationTokenService
    {
        public ClaimsPrincipal Read(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ReadTokenException(value, new System.FormatException("Empty token"));
            }

            try
            {
                var decodedString = DecodeBase64OrBase64Url(value);

                var parts = decodedString.Split(new char[] { '|' });
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new System.FormatException("Token payload must be 'user|role'.");
                }

                return new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, parts[0]),
                    new Claim(ClaimTypes.Role, parts[1]),
                }, CustomAuthenticationSchemeOptions.AuthenticationScheme));
            }
            catch (System.Exception ex)
            {
                throw new ReadTokenException(value, ex);
            }
        }

        private static string DecodeBase64OrBase64Url(string input)
        {
            // Try standard Base64 first
            try
            {
                var decodedBytes = System.Convert.FromBase64String(input.Trim());
                return System.Text.Encoding.UTF8.GetString(decodedBytes);
            }
            catch (System.FormatException)
            {
                // Then try Base64Url (RFC 4648) normalization: '-'->'+', '_'->'/' and add padding
                var normalized = input.Trim().Replace('-', '+').Replace('_', '/');
                switch (normalized.Length % 4)
                {
                    case 2: normalized += "=="; break;
                    case 3: normalized += "="; break;
                }
                var decodedBytes = System.Convert.FromBase64String(normalized);
                return System.Text.Encoding.UTF8.GetString(decodedBytes);
            }
        }
    }
}