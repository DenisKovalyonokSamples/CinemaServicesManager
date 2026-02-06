using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CNM.Showtimes.API;
using CNM.Showtimes.API.Auth;
using CNM.Showtimes.API.Controllers;
using CNM.Showtimes.API.Database;
using CNM.Showtimes.API.Database.Entities;
using CNM.Showtimes.API.Middleware;
using CNM.Showtimes.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CNM.Showtimes.Tests
{
    public class ShowtimesTests
    {
        [Fact]
        public void ShowtimeController_Get_FiltersByDateAndTitle()
        {
            var repo = new FakeRepo();
            var imdb = new FakeImdbClient();
            var controller = new ShowtimeController(repo, imdb)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var date = new DateTime(2020, 1, 5);
            var result = controller.Get(date, "Star");
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<ShowtimeEntity>>(ok.Value);
            Assert.Single(list);
            Assert.Equal("Star Movie", list.First().Movie.Title);
        }

        [Fact]
        public async Task ShowtimeController_Post_ValidatesAndCreates_AndFetchesImdb()
        {
            var repo = new FakeRepo();
            var imdb = new FakeImdbClient
            {
                GetById = new ImdbTitleResponse { id = "tt42", title = "Answer", stars = "A,B", releaseDate = "2020-02-02" }
            };
            var controller = new ShowtimeController(repo, imdb)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var payload = new ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 2, 1),
                EndDate = new DateTime(2020, 2, 10),
                Movie = new MovieEntity { ImdbId = "tt42" }
            };

            var result = await controller.Post(payload, "APIKEY");
            var created = Assert.IsType<CreatedResult>(result);
            var entity = Assert.IsType<ShowtimeEntity>(created.Value);
            Assert.Equal("Answer", entity.Movie.Title);
            Assert.Equal("A,B", entity.Movie.Stars);
            Assert.Equal(new DateTime(2020, 2, 2), entity.Movie.ReleaseDate);
        }

        [Fact]
        public async Task ShowtimeController_Post_ReturnsBadRequest_WhenMissingImdbId()
        {
            var controller = new ShowtimeController(new FakeRepo(), new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var payload = new ShowtimeEntity { Movie = new MovieEntity { ImdbId = "" } };
            var result = await controller.Post(payload, "APIKEY");
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("required", bad.Value?.ToString());
        }

        [Fact]
        public async Task ShowtimeController_Put_UpdatesAndFetchesImdb_WhenImdbIdPresent()
        {
            var repo = new FakeRepo { UpdateResult = new ShowtimeEntity { Id = 2, Movie = new MovieEntity() } };
            var imdb = new FakeImdbClient { GetById = new ImdbTitleResponse { title = "NewTitle", stars = "C,D", releaseDate = "2021-01-01" } };
            var controller = new ShowtimeController(repo, imdb)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var payload = new ShowtimeEntity { Id = 2, Movie = new MovieEntity { ImdbId = "tt99" } };
            var result = await controller.Put(payload, "APIKEY");
            var ok = Assert.IsType<OkObjectResult>(result);
            var entity = Assert.IsType<ShowtimeEntity>(ok.Value);
            Assert.Equal("NewTitle", entity.Movie.Title);
            Assert.Equal("C,D", entity.Movie.Stars);
            Assert.Equal(new DateTime(2021, 1, 1), entity.Movie.ReleaseDate);
        }

        [Fact]
        public async Task ShowtimeController_Put_ReturnsNotFound_WhenUpdateNull()
        {
            var controller = new ShowtimeController(new FakeRepo { UpdateResult = null }, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var payload = new ShowtimeEntity { Id = 3 };
            var result = await controller.Put(payload, "APIKEY");
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public void ShowtimeController_Delete_NoContentOrNotFound()
        {
            var repo = new FakeRepo { DeleteResult = new ShowtimeEntity { Id = 5 } };
            var controller = new ShowtimeController(repo, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var nc = controller.Delete(5);
            Assert.IsType<NoContentResult>(nc);

            controller = new ShowtimeController(new FakeRepo { DeleteResult = null }, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var nf = controller.Delete(6);
            Assert.IsType<NotFoundResult>(nf);
        }

        [Fact]
        public void ErrorHandlingMiddleware_CatchesExceptionsAndWritesJson()
        {
            var logger = new LoggerFactory().CreateLogger<ErrorHandlingMiddleware>();
            var mw = new ErrorHandlingMiddleware(async ctx => throw new Exception("boom"), logger);
            var ctx = new DefaultHttpContext();
            var ms = new MemoryStream();
            ctx.Response.Body = ms;

            var ex = Record.ExceptionAsync(() => mw.Invoke(ctx)).Result;
            Assert.Null(ex);
            ms.Position = 0;
            var text = new StreamReader(ms).ReadToEnd();
            Assert.Contains("internal_server_error", text);
            Assert.Equal("application/json", ctx.Response.ContentType);
            Assert.Equal(500, ctx.Response.StatusCode);
        }

        [Fact]
        public void RequestTimingMiddleware_LogsForShowtimeRoutes()
        {
            var factory = new LoggerFactory();
            var logger = factory.CreateLogger<RequestTimingMiddleware>();
            var logs = new List<string>();
            factory.AddProvider(new ListLoggerProvider(logs));
            var loggerWithProvider = factory.CreateLogger<RequestTimingMiddleware>();
            var mw = new RequestTimingMiddleware(async ctx => await Task.Delay(10), loggerWithProvider);
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/showtime/list";
            mw.Invoke(ctx).GetAwaiter().GetResult();
            Assert.True(logs.Any(l => l.Contains("ShowtimeController request")));
        }

        [Fact]
        public async Task ImdbStatusBackgroundService_IncrementsSingleton_OnPing()
        {
            var logs = new List<string>();
            var provider = new LoggerFactory();
            provider.AddProvider(new ListLoggerProvider(logs));
            var client = new FakeImdbClient { Ping = true };
            var singleton = new ImdbStatusSingleton();
            var svc = new ImdbStatusBackgroundService(provider.CreateLogger<ImdbStatusBackgroundService>(), client, singleton);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);
            await svc.StartAsync(cts.Token);
            await Task.Delay(60);
            await svc.StopAsync(cts.Token);

            Assert.True(singleton.StatusChecks >= 1);
        }

        [Fact]
        public void CustomAuthenticationTokenService_Read_ParsesOrThrows()
        {
            var svc = new CustomAuthenticationTokenService();
            var principal = svc.Read(Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Read")));
            Assert.Equal("user", principal.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal("Read", principal.FindFirstValue(ClaimTypes.Role));

            Assert.Throws<ReadTokenException>(() => svc.Read("notbase64"));
        }

        [Fact]
        public void CustomAuthenticationHandler_SucceedsOrFails()
        {
            var tokenSvc = new CustomAuthenticationTokenService();
            var opts = new OptionsMonitorInline<CustomAuthenticationSchemeOptions>(new CustomAuthenticationSchemeOptions());
            var handler = new CustomAuthenticationHandler(opts, new LoggerFactory(), System.Text.Encodings.Web.UrlEncoder.Default, new SystemClock(), tokenSvc);

            var ctx = new DefaultHttpContext();
            ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
            ctx.Request.Headers["ApiKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Read"));
            handler.InitializeAsync(new AuthenticationScheme(CustomAuthenticationSchemeOptions.AuthenticationScheme, null, typeof(CustomAuthenticationHandler)), ctx);
            var result = handler.AuthenticateAsync().GetAwaiter().GetResult();
            Assert.True(result.Succeeded);

            ctx = new DefaultHttpContext();
            ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
            ctx.Request.Headers["ApiKey"] = "invalid";
            handler.InitializeAsync(new AuthenticationScheme(CustomAuthenticationSchemeOptions.AuthenticationScheme, null, typeof(CustomAuthenticationHandler)), ctx);
            result = handler.AuthenticateAsync().GetAwaiter().GetResult();
            Assert.False(result.Succeeded);

            // local inline options monitor stub
        }

        private sealed class OptionsMonitorInline<T> : Microsoft.Extensions.Options.IOptionsMonitor<T>
        {
            private readonly T _current;
            public OptionsMonitorInline(T current) { _current = current; }
            public T CurrentValue => _current;
            public T Get(string name) => _current;
            public IDisposable OnChange(Action<T, string> listener) => null;
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
        public void Startup_ConfigureServices_RegistersDependencies()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var startup = new Startup(config);

            startup.ConfigureServices(services);
            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IShowtimesRepository>());
            Assert.NotNull(provider.GetService<IImdbClient>());
            Assert.NotNull(provider.GetService<ImdbStatusSingleton>());
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

        private sealed class FakeRepo : IShowtimesRepository
        {
            public ShowtimeEntity DeleteResult { get; set; }
            public ShowtimeEntity UpdateResult { get; set; }
            public IEnumerable<ShowtimeEntity> GetCollection() => _data;
            public IEnumerable<ShowtimeEntity> GetCollection(Func<IQueryable<ShowtimeEntity>, bool> filter)
            {
                var q = _data.AsQueryable();
                return filter(q) ? _data : Enumerable.Empty<ShowtimeEntity>();
            }
            public ShowtimeEntity GetByMovie(Func<IQueryable<MovieEntity>, bool> filter) => null;
            public ShowtimeEntity Add(ShowtimeEntity showtimeEntity)
            {
                _data.Add(showtimeEntity);
                return showtimeEntity;
            }
            public ShowtimeEntity Update(ShowtimeEntity showtimeEntity) => UpdateResult;
            public ShowtimeEntity Delete(int id) => DeleteResult;
            private readonly List<ShowtimeEntity> _data = new List<ShowtimeEntity>
            {
                new ShowtimeEntity
                {
                    Id = 1,
                    StartDate = new DateTime(2020, 1, 1),
                    EndDate = new DateTime(2020, 1, 10),
                    Movie = new MovieEntity { Title = "Star Movie" }
                },
                new ShowtimeEntity
                {
                    Id = 2,
                    StartDate = new DateTime(2020, 1, 20),
                    EndDate = new DateTime(2020, 1, 25),
                    Movie = new MovieEntity { Title = "Another" }
                }
            };
        }

        private sealed class FakeImdbClient : IImdbClient
        {
            public bool Ping { get; set; }
            public ImdbTitleResponse GetById { get; set; }
            public Task<bool> PingAsync() => Task.FromResult(Ping);
            public Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey) => Task.FromResult(GetById);
        }

        private sealed class ListLoggerProvider : ILoggerProvider
        {
            private readonly List<string> _logs;
            public ListLoggerProvider(List<string> logs) => _logs = logs;
            public ILogger CreateLogger(string categoryName) => new ListLogger(_logs);
            public void Dispose() { }
            private sealed class ListLogger : ILogger
            {
                private readonly List<string> _logs;
                public ListLogger(List<string> logs) => _logs = logs;
                public IDisposable BeginScope<TState>(TState state) => null;
                public bool IsEnabled(LogLevel logLevel) => true;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    _logs.Add(formatter(state, exception));
                }
            }
        }

        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(ShowtimesTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
