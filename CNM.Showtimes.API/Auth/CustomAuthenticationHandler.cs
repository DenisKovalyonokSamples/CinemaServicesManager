using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using CNM.Application.Auth;

namespace CNM.Showtimes.API.Auth
{
    public class CustomAuthenticationHandler : AuthenticationHandler<CustomAuthenticationSchemeOptions>
    {
        private readonly ICustomAuthenticationTokenService _tokenService;

        public CustomAuthenticationHandler(
            IOptionsMonitor<CustomAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            ICustomAuthenticationTokenService tokenService) : base(options, logger, encoder, clock)
        {
            _tokenService = tokenService;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Validate presence of header
            if (!Request.Headers.TryGetValue("ApiKey", out var apiKeyValues) || Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(apiKeyValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var apiKey = apiKeyValues.ToString().Trim();

            try
            {
                var principal = _tokenService.Read(apiKey);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
            }
            catch (ReadTokenException)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid ApiKey token."));
            }
            catch (System.Exception ex)
            {
                return Task.FromResult(AuthenticateResult.Fail(ex));
            }
        }
    }
}
