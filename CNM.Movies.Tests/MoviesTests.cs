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
        [Theory]
        [InlineData(true, "ok")]
        [InlineData(false, "fail")]
        public async Task MoviesController_Ping_ReturnsExpectedStatus(bool pingResult, string expected)
        {
            var fake = new FakeImdbClient { Ping = pingResult };
            var controller = new MoviesController(fake)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.Ping();
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value;
            var prop = value.GetType().GetProperty("status");
            Assert.Equal(expected, prop.GetValue(value)?.ToString());
        }

        [Fact]
        public async Task MoviesController_GetById_ReturnsOkWithClientResult_AndPassesParams()
        {
            var fake = new FakeImdbClient
            {
                GetById = new ImdbTitleResponse { id = "tt123", title = "The Film", stars = "A,B", releaseDate = "2020-01-01" }
            };
            var controller = new MoviesController(fake)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.GetById("tt123", "APIKEY");

            var ok = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsType<ImdbTitleResponse>(ok.Value);
            Assert.Equal("tt123", model.id);
            Assert.Equal("APIKEY", fake.LastApiKey);
            Assert.Equal("tt123", fake.LastImdbId);
        }

        [Fact]
        public async Task ImdbClient_PingAsync_ReturnsTrue_On200()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://imdb/") };
            var client = new ImdbClient(http);

            var ok = await client.PingAsync();
            Assert.True(ok);
        }

        [Fact]
        public async Task ImdbClient_PingAsync_ReturnsFalse_OnNonSuccess()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://imdb/") };
            var client = new ImdbClient(http);

            var ok = await client.PingAsync();
            Assert.False(ok);
        }

        [Fact]
        public async Task ImdbClient_PingAsync_ReturnsFalse_OnException()
        {
            var handler = new ThrowingHandler();
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://imdb/") };
            var client = new ImdbClient(http);

            var ok = await client.PingAsync();
            Assert.False(ok);
        }

        [Fact]
        public async Task ImdbClient_GetByIdAsync_ParsesJson()
        {
            var json = "{\"id\":\"tt123\",\"title\":\"The Film\",\"stars\":\"A,B\",\"releaseDate\":\"2020\"}";
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://imdb/") };
            var client = new ImdbClient(http);

            var model = await client.GetByIdAsync("tt123", "APIKEY");
            Assert.Equal("tt123", model.id);
            Assert.Equal("The Film", model.title);
            Assert.Equal("A,B", model.stars);
            Assert.Equal("2020", model.releaseDate);
        }

        [Fact]
        public async Task ImdbClient_GetByIdAsync_Throws_OnNonSuccess()
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://imdb/") };
            var client = new ImdbClient(http);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetByIdAsync("tt404", "APIKEY"));
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
            Assert.NotNull(provider.GetService<IImdbClient>());
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

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_handler(request));
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new HttpRequestException("boom");
        }

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
