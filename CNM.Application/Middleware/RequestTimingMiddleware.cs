using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CNM.Showtimes.API.Middleware
{
    public class RequestTimingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestTimingMiddleware> _logger;
        public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger) // Added: DI constructor
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context) // Added: log execution time of showtime endpoints
        {
            var sw = Stopwatch.StartNew();
            await _next(context);
            sw.Stop();
            if (context.Request.Path.StartsWithSegments("/showtime"))
            {
                _logger.LogInformation("ShowtimeController request {Path} took {Elapsed} ms", context.Request.Path, sw.ElapsedMilliseconds);
            }
        }
    }
}
