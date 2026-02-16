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

namespace CNM.Gateway.Tests.UnitTests
{
    // Unit tests for gateway controller proxy behavior and startup wiring
    public class GatewayUnitTests
    {
        // Verifies NotFound is returned when the requested downstream service is not configured.
        [Fact]
        public async Task Proxy_UnknownService_ReturnsNotFound()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()).Build();
            var gatewayController = new GatewayController(new FakeHttpClientFactory(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))), configuration)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var actionResult = await gatewayController.Proxy("unknown", "api/test");

            var notFoundObjectResult = Assert.IsType<NotFoundObjectResult>(actionResult);
            Assert.Contains("Unknown service", notFoundObjectResult.Value?.ToString());
        }

        // Ensures GET requests are proxied correctly: headers forwarded, response headers applied, and content returned.
        [Fact]
        public async Task Proxy_ForwardsGetWithHeaders_AndReturnsFileWithResponseHeaders()
        {
            // Arrange config for known service
            var downstreamConfiguration = new Dictionary<string, string> { ["DownstreamServices:movies"] = "http://movies.local" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(downstreamConfiguration).Build();

            HttpRequestMessage capturedDownstreamRequest = null;
            var fakeHandler = new FakeHandler(req =>
            {
                capturedDownstreamRequest = req;
                var downstreamResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("hello"))
                };
                downstreamResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                downstreamResponse.Headers.TryAddWithoutValidation("x-downstream", "ok");
                downstreamResponse.Headers.TryAddWithoutValidation("transfer-encoding", "chunked");
                return downstreamResponse;
            });
            var httpClient = new HttpClient(fakeHandler);
            var gatewayController = new GatewayController(new FakeHttpClientFactory(httpClient), configuration)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Add request headers, one general and one content-related to exercise both branches
            gatewayController.Request.Method = HttpMethods.Get;
            gatewayController.Request.Headers["x-test"] = "abc";
            gatewayController.Request.Headers["Content-Language"] = "en-US";

            // Act
            var actionResult = await gatewayController.Proxy("movies", "v1/list");

            // Assert request forwarding
            Assert.NotNull(capturedDownstreamRequest);
            Assert.Equal(HttpMethod.Get, capturedDownstreamRequest.Method);
            Assert.Equal("http://movies.local/v1/list", capturedDownstreamRequest.RequestUri!.ToString());
            Assert.True(capturedDownstreamRequest.Headers.TryGetValues("x-test", out var xTestValues) && xTestValues.Contains("abc"));
            // Content-Language should end up in content headers for GET
            Assert.NotNull(capturedDownstreamRequest.Content);
            Assert.True(capturedDownstreamRequest.Content!.Headers.TryGetValues("Content-Language", out var contentLanguages) && contentLanguages.Contains("en-US"));

            // Assert response mapping
            var fileContentResult = Assert.IsType<FileContentResult>(actionResult);
            Assert.Equal("text/plain", fileContentResult.ContentType);
            Assert.Equal("ok", gatewayController.Response.Headers["x-downstream"].ToString());
            Assert.False(gatewayController.Response.Headers.ContainsKey("transfer-encoding"));
            Assert.Equal("hello", Encoding.UTF8.GetString(fileContentResult.FileContents));
        }

        // Confirms POST/PUT/PATCH bodies and content type are forwarded to downstream.
        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        public async Task Proxy_ForwardsBodyAndContentType_ForMethodsWithBody(string method)
        {
            var downstreamConfiguration = new Dictionary<string, string> { ["DownstreamServices:showtimes"] = "http://showtimes.local" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(downstreamConfiguration).Build();

            HttpRequestMessage capturedDownstreamRequest = null;
            var fakeHandler = new FakeHandler(req =>
            {
                capturedDownstreamRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
            });
            var gatewayController = new GatewayController(new FakeHttpClientFactory(new HttpClient(fakeHandler)), configuration)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            gatewayController.Request.Method = method;
            var requestBodyBytes = Encoding.UTF8.GetBytes("{\"a\":1}");
            gatewayController.Request.Body = new MemoryStream(requestBodyBytes);
            gatewayController.Request.ContentType = "application/json";

            var actionResult = await gatewayController.Proxy("showtimes", "v2/create");

            Assert.NotNull(capturedDownstreamRequest);
            Assert.NotNull(capturedDownstreamRequest.Content);
            Assert.Equal("application/json", capturedDownstreamRequest.Content!.Headers.ContentType!.MediaType);
            var forwardedRequestBody = await capturedDownstreamRequest.Content.ReadAsStringAsync();
            Assert.Equal("{\"a\":1}", forwardedRequestBody);
            Assert.IsType<FileContentResult>(actionResult);
        }

        // Smoke test that the web host builder is created and can build a host.
        [Fact]
        public void Program_CreateHostBuilder_Builds()
        {
            var builder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(builder);
            using var host = builder.Build();
            Assert.NotNull(host.Services);
        }

        // Ensures Startup.ConfigureServices registers HttpClient factory and MVC.
        [Fact]
        public void Startup_ConfigureServices_RegistersHttpClientAndMvc()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);

            startup.ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.NotNull(serviceProvider.GetService<IHttpClientFactory>());
            Assert.NotNull(serviceProvider.GetService<IActionDescriptorCollectionProvider>());
        }

        // Smoke test that Startup.Configure runs without throwing.
        [Fact]
        public void Startup_Configure_DoesNotThrow()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);
            startup.ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var appBuilder = new ApplicationBuilder(serviceProvider);
            var environment = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };

            var exception = Record.Exception(() => startup.Configure(appBuilder, environment));
            Assert.Null(exception);
        }

        // Minimal IHttpClientFactory implementation returning a provided HttpClient.
        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public FakeHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name = null) => _client;
        }

        // HttpMessageHandler stub that returns a response from a provided delegate.
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_handler(request));
        }

        // Simple IWebHostEnvironment for testing Startup.Configure.
        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(GatewayUnitTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}