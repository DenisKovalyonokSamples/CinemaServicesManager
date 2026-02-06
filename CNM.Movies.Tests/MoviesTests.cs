using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CNM.Movies.API;
using CNM.Movies.API.Controllers;
using CNM.Movies.API.Services;
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

namespace CNM.Movies.Tests
{
    public class MoviesTests
    {
        // Verifies MoviesController.Ping returns status based on IImdbClient.PingAsync.
        [Theory]
        [InlineData(true, "ok")]
        [InlineData(false, "fail")]
        public async Task MoviesController_Ping_ReturnsExpectedStatus(bool pingResult, string expected)
        {
            var fakeImdbClient = new FakeImdbClient { Ping = pingResult };
            var moviesController = new MoviesController(fakeImdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var actionResult = await moviesController.Ping();
            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
            var responseValue = okObjectResult.Value;
            var statusProperty = responseValue.GetType().GetProperty("status");
            Assert.Equal(expected, statusProperty.GetValue(responseValue)?.ToString());
        }

        // Ensures MoviesController.GetById returns Ok with model and passes imdbId/apiKey to client.
        [Fact]
        public async Task MoviesController_GetById_ReturnsOkWithClientResult_AndPassesParams()
        {
            var fakeImdbClient = new FakeImdbClient
            {
                GetById = new ImdbTitleResponse { id = "tt123", title = "The Film", stars = "A,B", releaseDate = "2020-01-01" }
            };
            var moviesController = new MoviesController(fakeImdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var actionResult = await moviesController.GetById("tt123", "APIKEY");

            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
            var responseModel = Assert.IsType<ImdbTitleResponse>(okObjectResult.Value);
            Assert.Equal("tt123", responseModel.id);
            Assert.Equal("APIKEY", fakeImdbClient.LastApiKey);
            Assert.Equal("tt123", fakeImdbClient.LastImdbId);
        }

        // ImdbClient.PingAsync returns true on HTTP 200.
        [Fact]
        public async Task ImdbClient_PingAsync_ReturnsTrue_On200()
        {
            var fakeHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://imdb/") };
            var imdbClient = new ImdbClient(httpClient);

            var pingOk = await imdbClient.PingAsync();
            Assert.True(pingOk);
        }

        // ImdbClient.PingAsync returns false on non-success status code.
        [Fact]
        public async Task ImdbClient_PingAsync_ReturnsFalse_OnNonSuccess()
        {
            var fakeHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://imdb/") };
            var imdbClient = new ImdbClient(httpClient);

            var pingOk = await imdbClient.PingAsync();
            Assert.False(pingOk);
        }

        // ImdbClient.PingAsync returns false when HttpClient throws.
        [Fact]
        public async Task ImdbClient_PingAsync_ReturnsFalse_OnException()
        {
            var throwingHandler = new ThrowingHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("http://imdb/") };
            var imdbClient = new ImdbClient(httpClient);

            var pingOk = await imdbClient.PingAsync();
            Assert.False(pingOk);
        }

        // ImdbClient.GetByIdAsync parses JSON payload into ImdbTitleResponse.
        [Fact]
        public async Task ImdbClient_GetByIdAsync_ParsesJson()
        {
            var json = "{\"id\":\"tt123\",\"title\":\"The Film\",\"stars\":\"A,B\",\"releaseDate\":\"2020\"}";
            var fakeHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://imdb/") };
            var imdbClient = new ImdbClient(httpClient);

            var responseModel = await imdbClient.GetByIdAsync("tt123", "APIKEY");
            Assert.Equal("tt123", responseModel.id);
            Assert.Equal("The Film", responseModel.title);
            Assert.Equal("A,B", responseModel.stars);
            Assert.Equal("2020", responseModel.releaseDate);
        }

        // ImdbClient.GetByIdAsync throws on non-success responses.
        [Fact]
        public async Task ImdbClient_GetByIdAsync_Throws_OnNonSuccess()
        {
            var fakeHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://imdb/") };
            var imdbClient = new ImdbClient(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(() => imdbClient.GetByIdAsync("tt404", "APIKEY"));
        }

        // Smoke test: Program.CreateHostBuilder creates a builder and builds a host.
        [Fact]
        public void Program_CreateHostBuilder_Builds()
        {
            var hostBuilder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(hostBuilder);
            using var host = hostBuilder.Build();
            Assert.NotNull(host.Services);
        }

        // Ensures Startup.ConfigureServices registers IImdbClient and MVC services.
        [Fact]
        public void Startup_ConfigureServices_RegistersHttpClientAndMvc()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);

            startup.ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.NotNull(serviceProvider.GetService<IImdbClient>());
            Assert.NotNull(serviceProvider.GetService<IActionDescriptorCollectionProvider>());
        }

        // Smoke test: Startup.Configure runs without exception.
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

        // Test double for IImdbClient to control responses and capture parameters.
        private sealed class FakeImdbClient : IImdbClient
        {
            public bool Ping { get; set; }
            public ImdbTitleResponse GetById { get; set; }
            public string LastImdbId { get; private set; }
            public string LastApiKey { get; private set; }
            public Task<bool> PingAsync() => Task.FromResult(Ping);
            public Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey)
            {
                LastImdbId = imdbId;
                LastApiKey = apiKey;
                return Task.FromResult(GetById);
            }
        }

        // HttpMessageHandler stub that returns a response using a delegate.
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_handler(request));
        }

        // HttpMessageHandler that throws to simulate HttpClient errors.
        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new HttpRequestException("boom");
        }

        // Minimal IWebHostEnvironment implementation for Startup tests.
        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(MoviesTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
