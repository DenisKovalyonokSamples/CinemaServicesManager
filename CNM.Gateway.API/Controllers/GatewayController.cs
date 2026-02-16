using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace CNM.Gateway.API.Controllers
{
    [ApiController]
    [Route("{service}/{*path}")]
    public class GatewayController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GatewayController> _logger;

        public GatewayController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GatewayController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch]
        public async Task<IActionResult> Proxy(string service, string path)
        {
            var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? System.Guid.NewGuid().ToString();
            using (_logger.BeginScope(new Dictionary<string, object> { { "CorrelationId", correlationId }, { "Service", service }, { "Path", path } }))
            {
                var baseUrl = _configuration[$"DownstreamServices:{service}"];
                if (string.IsNullOrEmpty(baseUrl))
                {
                    _logger.LogWarning("Unknown service requested: {Service}", service);
                    return NotFound($"Unknown service '{service}'");
                }

                var client = _httpClientFactory.CreateClient();
                var targetUrl = $"{baseUrl.TrimEnd('/')}/{path}";
                var request = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);
                foreach (var header in Request.Headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                    {
                        request.Content ??= new StringContent("");
                        request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                if (Request.Body != null && (Request.Method == HttpMethod.Post.Method || Request.Method == HttpMethod.Put.Method || Request.Method == HttpMethod.Patch.Method))
                {
                    request.Content = new StreamContent(Request.Body);
                    if (Request.ContentType != null)
                    {
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Request.ContentType);
                    }
                }

                var stopwatch = Stopwatch.StartNew();
                HttpResponseMessage response = null;
                try
                {
                    _logger.LogInformation("Proxying request to {TargetUrl}", targetUrl);
                    response = await client.SendAsync(request);
                    stopwatch.Stop();
                    _logger.LogInformation("Proxy call completed: {StatusCode} in {Duration}ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Proxy call failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }

                var content = await response.Content.ReadAsByteArrayAsync();
                foreach (var header in response.Headers)
                {
                    Response.Headers[header.Key] = string.Join(",", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    Response.Headers[header.Key] = string.Join(",", header.Value);
                }
                Response.Headers.Remove("transfer-encoding");

                return File(content, response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", enableRangeProcessing: false);
            }
        }
    }
}
