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
        // Filters list by date and title; validates only matching showtime is returned.
        [Fact]
        public void ShowtimeController_Get_FiltersByDateAndTitle()
        {
            var repository = new FakeRepo();
            var imdbClient = new FakeImdbClient();
            var showtimeController = new ShowtimeController(repository, imdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var filterDate = new DateTime(2020, 1, 5);
            var actionResult = showtimeController.Get(filterDate, "Star");
            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
            var returnedShowtimes = Assert.IsAssignableFrom<IEnumerable<ShowtimeEntity>>(okObjectResult.Value);
            Assert.Single(returnedShowtimes);
            Assert.Equal("Star Movie", returnedShowtimes.First().Movie.Title);
        }

        // Creates a showtime, fetches IMDB data, and enriches movie fields before returning Created.
        [Fact]
        public async Task ShowtimeController_Post_ValidatesAndCreates_AndFetchesImdb()
        {
            var repository = new FakeRepo();
            var imdbClient = new FakeImdbClient
            {
                GetById = new ImdbTitleResponse { id = "tt42", title = "Answer", stars = "A,B", releaseDate = "2020-02-02" }
            };
            var showtimeController = new ShowtimeController(repository, imdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var newShowtime = new CNM.Domain.Database.Entities.ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 2, 1),
                EndDate = new DateTime(2020, 2, 10),
                Movie = new CNM.Domain.Database.Entities.MovieEntity { ImdbId = "tt42" }
            };

            var actionResult = await showtimeController.Post(newShowtime, "APIKEY");
            var createdResult = Assert.IsType<CreatedResult>(actionResult);
            var createdShowtime = Assert.IsType<ShowtimeEntity>(createdResult.Value);
            Assert.Equal("Answer", createdShowtime.Movie.Title);
            Assert.Equal("A,B", createdShowtime.Movie.Stars);
            Assert.Equal(new DateTime(2020, 2, 2), createdShowtime.Movie.ReleaseDate);
        }

        // Returns BadRequest when POST payload has missing/empty imdb id.
        [Fact]
        public async Task ShowtimeController_Post_ReturnsBadRequest_WhenMissingImdbId()
        {
            var showtimeController = new ShowtimeController(new FakeRepo(), new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var invalidShowtime = new CNM.Domain.Database.Entities.ShowtimeEntity { Movie = new CNM.Domain.Database.Entities.MovieEntity { ImdbId = "" } };
            var actionResult = await showtimeController.Post(invalidShowtime, "APIKEY");
            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Contains("required", badRequest.Value?.ToString());
        }

        // Updates showtime and refreshes IMDB fields when imdb id is present.
        [Fact]
        public async Task ShowtimeController_Put_UpdatesAndFetchesImdb_WhenImdbIdPresent()
        {
            var repository = new FakeRepo { UpdateResult = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 2, Movie = new CNM.Domain.Database.Entities.MovieEntity() } };
            var imdbClient = new FakeImdbClient { GetById = new ImdbTitleResponse { title = "NewTitle", stars = "C,D", releaseDate = "2021-01-01" } };
            var showtimeController = new ShowtimeController(repository, imdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var updatedShowtimePayload = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 2, Movie = new CNM.Domain.Database.Entities.MovieEntity { ImdbId = "tt99" } };
            var actionResult = await showtimeController.Put(updatedShowtimePayload, "APIKEY");
            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
            var updatedShowtime = Assert.IsType<ShowtimeEntity>(okObjectResult.Value);
            Assert.Equal("NewTitle", updatedShowtime.Movie.Title);
            Assert.Equal("C,D", updatedShowtime.Movie.Stars);
            Assert.Equal(new DateTime(2021, 1, 1), updatedShowtime.Movie.ReleaseDate);
        }

        // Returns NotFound when repository update yields null.
        [Fact]
        public async Task ShowtimeController_Put_ReturnsNotFound_WhenUpdateNull()
        {
            var showtimeController = new ShowtimeController(new FakeRepo { UpdateResult = null }, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var notFoundPayload = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 3 };
            var actionResult = await showtimeController.Put(notFoundPayload, "APIKEY");
            Assert.IsType<NotFoundResult>(actionResult);
        }

        // Delete returns NoContent for existing id; NotFound otherwise.
        [Fact]
        public void ShowtimeController_Delete_NoContentOrNotFound()
        {
            var repository = new FakeRepo { DeleteResult = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 5 } };
            var showtimeController = new ShowtimeController(repository, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var noContentResult = showtimeController.Delete(5);
            Assert.IsType<NoContentResult>(noContentResult);

            showtimeController = new ShowtimeController(new FakeRepo { DeleteResult = null }, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var notFoundResult = showtimeController.Delete(6);
            Assert.IsType<NotFoundResult>(notFoundResult);
        }

        // Middleware catches exceptions and writes JSON 500 response.
        [Fact]
        public void ErrorHandlingMiddleware_CatchesExceptionsAndWritesJson()
        {
            var logger = new LoggerFactory().CreateLogger<ErrorHandlingMiddleware>();
            var errorHandlingMiddleware = new ErrorHandlingMiddleware(async httpContext => throw new Exception("boom"), logger);
            var httpContext = new DefaultHttpContext();
            var responseStream = new MemoryStream();
            httpContext.Response.Body = responseStream;

            var exception = Record.ExceptionAsync(() => errorHandlingMiddleware.Invoke(httpContext)).Result;
            Assert.Null(exception);
            responseStream.Position = 0;
            var responseText = new StreamReader(responseStream).ReadToEnd();
            Assert.Contains("internal_server_error", responseText);
            Assert.Equal("application/json", httpContext.Response.ContentType);
            Assert.Equal(500, httpContext.Response.StatusCode);
        }

        // Middleware logs timings for requests under /showtime path.
        [Fact]
        public void RequestTimingMiddleware_LogsForShowtimeRoutes()
        {
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RequestTimingMiddleware>();
            var logMessages = new List<string>();
            loggerFactory.AddProvider(new ListLoggerProvider(logMessages));
            var loggerWithProvider = loggerFactory.CreateLogger<RequestTimingMiddleware>();
            var requestTimingMiddleware = new RequestTimingMiddleware(async httpContext => await Task.Delay(10), loggerWithProvider);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/showtime/list";
            requestTimingMiddleware.Invoke(httpContext).GetAwaiter().GetResult();
            Assert.True(logMessages.Any(message => message.Contains("ShowtimeController request")));
        }

        // Background service increments shared singleton on each ping iteration.
        [Fact]
        public async Task ImdbStatusBackgroundService_IncrementsSingleton_OnPing()
        {
            var logMessages = new List<string>();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new ListLoggerProvider(logMessages));
            var imdbClient = new FakeImdbClient { Ping = true };
            var statusSingleton = new ImdbStatusSingleton();
            var backgroundService = new ImdbStatusBackgroundService(loggerFactory.CreateLogger<ImdbStatusBackgroundService>(), imdbClient, statusSingleton);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(50);
            await backgroundService.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(60);
            await backgroundService.StopAsync(cancellationTokenSource.Token);

            Assert.True(statusSingleton.StatusChecks >= 1);
        }

        // Token service parses base64 token into claims; throws for invalid.
        [Fact]
        public void CustomAuthenticationTokenService_Read_ParsesOrThrows()
        {
            var tokenService = new CustomAuthenticationTokenService();
            var principal = tokenService.Read(Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Read")));
            Assert.Equal("user", principal.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal("Read", principal.FindFirstValue(ClaimTypes.Role));

            Assert.Throws<ReadTokenException>(() => tokenService.Read("notbase64"));
        }

        // Authentication handler succeeds with valid header; fails with invalid.
        [Fact]
        public void CustomAuthenticationHandler_SucceedsOrFails()
        {
            var tokenService = new CustomAuthenticationTokenService();
            var optionsMonitor = new OptionsMonitorInline<CustomAuthenticationSchemeOptions>(new CustomAuthenticationSchemeOptions());
            var authenticationHandler = new CustomAuthenticationHandler(optionsMonitor, new LoggerFactory(), System.Text.Encodings.Web.UrlEncoder.Default, new SystemClock(), tokenService);

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
            httpContext.Request.Headers["ApiKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Read"));
            authenticationHandler.InitializeAsync(new AuthenticationScheme(CustomAuthenticationSchemeOptions.AuthenticationScheme, null, typeof(CustomAuthenticationHandler)), httpContext);
            var authenticateResult = authenticationHandler.AuthenticateAsync().GetAwaiter().GetResult();
            Assert.True(authenticateResult.Succeeded);

            httpContext = new DefaultHttpContext();
            httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
            httpContext.Request.Headers["ApiKey"] = "invalid";
            authenticationHandler.InitializeAsync(new AuthenticationScheme(CustomAuthenticationSchemeOptions.AuthenticationScheme, null, typeof(CustomAuthenticationHandler)), httpContext);
            authenticateResult = authenticationHandler.AuthenticateAsync().GetAwaiter().GetResult();
            Assert.False(authenticateResult.Succeeded);

            // local inline options monitor stub
        }

        // Simple inline IOptionsMonitor implementation for handler initialization.
        private sealed class OptionsMonitorInline<T> : Microsoft.Extensions.Options.IOptionsMonitor<T>
        {
            private readonly T _current;
            public OptionsMonitorInline(T current) { _current = current; }
            public T CurrentValue => _current;
            public T Get(string name) => _current;
            public IDisposable OnChange(Action<T, string> listener) => null;
        }

        // Smoke test: host builder creates and builds successfully.
        [Fact]
        public void Program_CreateHostBuilder_Builds()
        {
            var hostBuilder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(hostBuilder);
            using var host = hostBuilder.Build();
            Assert.NotNull(host.Services);
        }

        // Ensures Startup.ConfigureServices registers required services.
        [Fact]
        public void Startup_ConfigureServices_RegistersDependencies()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);

            startup.ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<CNM.Domain.Database.IShowtimesRepository>());
            Assert.NotNull(serviceProvider.GetService<IImdbClient>());
            Assert.NotNull(serviceProvider.GetService<ImdbStatusSingleton>());
            Assert.NotNull(serviceProvider.GetService<IActionDescriptorCollectionProvider>());
        }

        // Smoke test: Startup.Configure pipeline executes without throwing.
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

        // In-memory repository test double used by controller tests.
        private sealed class FakeRepo : CNM.Domain.Database.IShowtimesRepository
        {
            public CNM.Domain.Database.Entities.ShowtimeEntity DeleteResult { get; set; }
            public CNM.Domain.Database.Entities.ShowtimeEntity UpdateResult { get; set; }
            public IEnumerable<CNM.Domain.Database.Entities.ShowtimeEntity> GetCollection() => _data;
            public IEnumerable<CNM.Domain.Database.Entities.ShowtimeEntity> GetCollection(Func<IQueryable<CNM.Domain.Database.Entities.ShowtimeEntity>, bool> filter)
            {
                var q = _data.AsQueryable();
                return filter(q) ? _data : Enumerable.Empty<CNM.Domain.Database.Entities.ShowtimeEntity>();
            }
            public CNM.Domain.Database.Entities.ShowtimeEntity GetByMovie(Func<IQueryable<CNM.Domain.Database.Entities.MovieEntity>, bool> filter) => null;
            public CNM.Domain.Database.Entities.ShowtimeEntity Add(CNM.Domain.Database.Entities.ShowtimeEntity showtimeEntity)
            {
                _data.Add(showtimeEntity);
                return showtimeEntity;
            }
            public CNM.Domain.Database.Entities.ShowtimeEntity Update(CNM.Domain.Database.Entities.ShowtimeEntity showtimeEntity) => UpdateResult;
            public CNM.Domain.Database.Entities.ShowtimeEntity Delete(int id) => DeleteResult;
            private readonly List<CNM.Domain.Database.Entities.ShowtimeEntity> _data = new List<CNM.Domain.Database.Entities.ShowtimeEntity>
            {
                new CNM.Domain.Database.Entities.ShowtimeEntity
                {
                    Id = 1,
                    StartDate = new DateTime(2020, 1, 1),
                    EndDate = new DateTime(2020, 1, 10),
                    Movie = new CNM.Domain.Database.Entities.MovieEntity { Title = "Star Movie" }
                },
                new CNM.Domain.Database.Entities.ShowtimeEntity
                {
                    Id = 2,
                    StartDate = new DateTime(2020, 1, 20),
                    EndDate = new DateTime(2020, 1, 25),
                    Movie = new CNM.Domain.Database.Entities.MovieEntity { Title = "Another" }
                }
            };
        }

        // IImdbClient stub with configurable responses for tests.
        private sealed class FakeImdbClient : IImdbClient
        {
            public bool Ping { get; set; }
            public ImdbTitleResponse GetById { get; set; }
            public Task<bool> PingAsync() => Task.FromResult(Ping);
            public Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey) => Task.FromResult(GetById);
        }

        // Logger provider capturing formatted messages to a list for assertions.
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

        // Minimal IWebHostEnvironment implementation for Startup tests.
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
