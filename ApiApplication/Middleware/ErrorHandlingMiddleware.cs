using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace CNM.Application.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger) // Added: DI constructor
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context) // Added: catch and return 500 with JSON
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var payload = JsonSerializer.Serialize(new { error = "internal_server_error" });
                await context.Response.WriteAsync(payload);
            }
        }
    }
}
