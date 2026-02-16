// Unit tests for Gateway API controller proxy behavior and startup wiring. Covers NotFound and proxy logic.
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
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
            var controller = new GatewayController(
                new FakeHttpClientFactory(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))),
                configuration,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GatewayController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.Proxy("unknown", "api/test");

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Unknown service", notFound.Value?.ToString());
        }

        // Ensures GET requests are proxied correctly: headers forwarded, response headers applied, and content returned.
        [Fact]
        public async Task Proxy_ForwardsGetWithHeaders_AndReturnsFileWithResponseHeaders()
        {
            // Arrange config for known service
            var downstreamConfiguration = new Dictionary<string, string> { ["DownstreamServices:movies"] = "http://movies.local" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(downstreamConfiguration).Build();

            HttpRequestMessage capturedRequest = null;
            var fakeHandler = new FakeHandler(req =>
            {
                capturedRequest = req;
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
            var controller = new GatewayController(
                new FakeHttpClientFactory(httpClient),
                configuration,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GatewayController>.Instance)
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
            Assert.True(capturedRequest.Headers.TryGetValues("x-test", out var xTestValues) && xTestValues.Contains("abc"));
            // Content-Language should end up in content headers for GET
            Assert.NotNull(capturedRequest.Content);
            Assert.True(capturedRequest.Content!.Headers.TryGetValues("Content-Language", out var contentLanguages) && contentLanguages.Contains("en-US"));

            // Assert response mapping
            var fileContent = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/plain", fileContent.ContentType);
            Assert.Equal("ok", controller.Response.Headers["x-downstream"].ToString());
            Assert.False(controller.Response.Headers.ContainsKey("transfer-encoding"));
            Assert.Equal("hello", Encoding.UTF8.GetString(fileContent.FileContents));
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

            HttpRequestMessage capturedRequest = null;
            var fakeHandler = new FakeHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
            });
            var controller = new GatewayController(
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                configuration,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GatewayController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            controller.Request.Method = method;
            var requestBodyBytes = Encoding.UTF8.GetBytes("{\"a\":1}");
            controller.Request.Body = new MemoryStream(requestBodyBytes);
            controller.Request.ContentType = "application/json";

            var result = await controller.Proxy("showtimes", "v2/create");

            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Content);
            Assert.Equal("application/json", capturedRequest.Content!.Headers.ContentType!.MediaType);
            var forwardedRequestBody = await capturedRequest.Content.ReadAsStringAsync();
            Assert.Equal("{\"a\":1}", forwardedRequestBody);
            Assert.IsType<FileContentResult>(result);
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
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);

            startup.ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<IHttpClientFactory>());
            Assert.NotNull(provider.GetService<IActionDescriptorCollectionProvider>());
        }

        // Smoke test that Startup.Configure runs without throwing.
        [Fact]
        public void Startup_Configure_DoesNotThrow()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);
            startup.ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            var appBuilder = new ApplicationBuilder(provider);
            // Provide a feature collection so UseHttpsRedirection (and other components) can access server features safely
            var features = new FeatureCollection();
            features.Set<IServerAddressesFeature>(new ServerAddressesFeature());
            appBuilder.Properties["server.Features"] = features;
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