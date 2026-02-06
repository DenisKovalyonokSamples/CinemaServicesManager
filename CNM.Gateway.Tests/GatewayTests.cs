using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CNM.Gateway.API;
using CNM.Gateway.API.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CNM.Gateway.Tests
{
    public class GatewayTests
    {
        [Fact]
        public async Task Proxy_UnknownService_ReturnsNotFound()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()).Build();
            var controller = new GatewayController(new FakeHttpClientFactory(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))), config)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.Proxy("unknown", "api/test");

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Unknown service", notFound.Value?.ToString());
        }

        [Fact]
        public async Task Proxy_ForwardsGetWithHeaders_AndReturnsFileWithResponseHeaders()
        {
            // Arrange config for known service
            var dict = new Dictionary<string, string> { ["DownstreamServices:movies"] = "http://movies.local" };
            var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

            HttpRequestMessage capturedRequest = null;
            var handler = new FakeHandler(req =>
            {
                capturedRequest = req;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("hello"))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                response.Headers.TryAddWithoutValidation("x-downstream", "ok");
                response.Headers.TryAddWithoutValidation("transfer-encoding", "chunked");
                return response;
            });
            var httpClient = new HttpClient(handler);
            var controller = new GatewayController(new FakeHttpClientFactory(httpClient), config)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Add request headers, one general and one content-related to exercise both branches
            controller.Request.Method = HttpMethods.Get;
            controller.Request.Headers["x-test"] = "abc";
            controller.Request.Headers["Content-Language"] = "en-US";

            // Act
            var result = await controller.Proxy("movies", "v1/list");

            // Assert request forwarding
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Equal("http://movies.local/v1/list", capturedRequest.RequestUri!.ToString());
            Assert.True(capturedRequest.Headers.TryGetValues("x-test", out var xVals) && xVals.Contains("abc"));
            // Content-Language should end up in content headers for GET
            Assert.NotNull(capturedRequest.Content);
            Assert.True(capturedRequest.Content!.Headers.TryGetValues("Content-Language", out var langs) && langs.Contains("en-US"));

            // Assert response mapping
            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/plain", file.ContentType);
            Assert.Equal("ok", controller.Response.Headers["x-downstream"].ToString());
            Assert.False(controller.Response.Headers.ContainsKey("transfer-encoding"));
            Assert.Equal("hello", Encoding.UTF8.GetString(file.FileContents));
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        public async Task Proxy_ForwardsBodyAndContentType_ForMethodsWithBody(string method)
        {
            var dict = new Dictionary<string, string> { ["DownstreamServices:showtimes"] = "http://showtimes.local" };
            var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

            HttpRequestMessage capturedRequest = null;
            var handler = new FakeHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
            });
            var controller = new GatewayController(new FakeHttpClientFactory(new HttpClient(handler)), config)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.Request.Method = method;
            var bodyBytes = Encoding.UTF8.GetBytes("{\"a\":1}");
            controller.Request.Body = new MemoryStream(bodyBytes);
            controller.Request.ContentType = "application/json";

            var result = await controller.Proxy("showtimes", "v2/create");

            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Content);
            Assert.Equal("application/json", capturedRequest.Content!.Headers.ContentType!.MediaType);
            var forwardedBody = await capturedRequest.Content.ReadAsStringAsync();
            Assert.Equal("{\"a\":1}", forwardedBody);
            Assert.IsType<FileContentResult>(result);
        }

        [Fact]
        public void Program_CreateHostBuilder_Builds()
        {
            var builder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(builder);
            using var host = builder.Build();
            Assert.NotNull(host.Services);
        }

        [Fact]
        public void Startup_ConfigureServices_RegistersHttpClientAndMvc()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var startup = new Startup(config);

            startup.ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IHttpClientFactory>());
            Assert.NotNull(provider.GetService<IActionDescriptorCollectionProvider>());
        }

        [Fact]
        public void Startup_Configure_DoesNotThrow()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var startup = new Startup(config);
            startup.ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(provider);
            var env = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };

            var ex = Record.Exception(() => startup.Configure(app, env));
            Assert.Null(ex);
        }

        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public FakeHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name = null) => _client;
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_handler(request));
        }

        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(GatewayTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}