using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CNM.Domain.Database.Entities;
using DomainDb = CNM.Domain.Database;
using Interfaces = CNM.Domain.Interfaces;
using CNM.Showtimes.API;
using CNM.Showtimes.API.Auth;
using CNM.Showtimes.API.Controllers;
using CNM.Application.Middleware;
using CNM.Application.Auth;
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
using CNM.Domain.Interfaces;
using CNM.Domain.Models;

namespace CNM.Showtimes.Tests
{
    public class ShowtimesUnitTests
    {
        [Fact]
        public void ShowtimeController_Get_FiltersByDateAndTitle()
        {
            var showtimesRepository = new FakeRepo();
            var imdbClient = new FakeImdbClient();
            var showtimeController = new ShowtimeController(showtimesRepository, imdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var dateFilter = new DateTime(2020, 1, 5);
            var actionResult = showtimeController.Get(dateFilter, "Star");
            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
            var returnedShowtimesList = Assert.IsAssignableFrom<IEnumerable<ShowtimeEntity>>(okObjectResult.Value);
            Assert.Single(returnedShowtimesList);
            Assert.Equal("Star Movie", returnedShowtimesList.First().Movie.Title);
        }

        [Fact]
        public async Task ShowtimeController_Post_ValidatesAndCreates_AndFetchesImdb()
        {
            var showtimesRepository = new FakeRepo();
            var imdbClient = new FakeImdbClient
            {
                GetById = new ImdbTitleResponse { id = "tt42", title = "Answer", stars = "A,B", releaseDate = "2020-02-02" }
            };
            var showtimeController = new ShowtimeController(showtimesRepository, imdbClient)
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
            var createdShowtimeEntity = Assert.IsType<ShowtimeEntity>(createdResult.Value);
            Assert.Equal("Answer", createdShowtimeEntity.Movie.Title);
            Assert.Equal("A,B", createdShowtimeEntity.Movie.Stars);
            Assert.Equal(new DateTime(2020, 2, 2), createdShowtimeEntity.Movie.ReleaseDate);
        }

        [Fact]
        public async Task ShowtimeController_Post_ReturnsProblemDetails_WhenMissingImdbId()
        {
            var showtimeController = new ShowtimeController(new FakeRepo(), new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var invalidShowtime = new CNM.Domain.Database.Entities.ShowtimeEntity { Movie = new CNM.Domain.Database.Entities.MovieEntity { ImdbId = string.Empty } };
            var actionResult = await showtimeController.Post(invalidShowtime, "APIKEY");
            var badRequest = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(400, badRequest.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
            Assert.Contains("required", problem.Detail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ShowtimeController_Put_UpdatesAndFetchesImdb_WhenImdbIdPresent()
        {
            var showtimesRepository = new FakeRepo { UpdateResult = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 2, Movie = new CNM.Domain.Database.Entities.MovieEntity() } };
            var imdbClient = new FakeImdbClient { GetById = new ImdbTitleResponse { title = "NewTitle", stars = "C,D", releaseDate = "2021-01-01" } };
            var showtimeController = new ShowtimeController(showtimesRepository, imdbClient)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var updatedShowtime = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 2, Movie = new CNM.Domain.Database.Entities.MovieEntity { ImdbId = "tt99" } };
            var actionResult = await showtimeController.Put(updatedShowtime, "APIKEY");
            var okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
            var updatedShowtimeEntity = Assert.IsType<ShowtimeEntity>(okObjectResult.Value);
            Assert.Equal("NewTitle", updatedShowtimeEntity.Movie.Title);
            Assert.Equal("C,D", updatedShowtimeEntity.Movie.Stars);
            Assert.Equal(new DateTime(2021, 1, 1), updatedShowtimeEntity.Movie.ReleaseDate);
        }

        [Fact]
        public async Task ShowtimeController_Put_ReturnsProblemDetails_WhenUpdateNull()
        {
            var showtimeController = new ShowtimeController(new FakeRepo { UpdateResult = null }, new FakeImdbClient())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var notFoundPayload = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 3 };
            var actionResult = await showtimeController.Put(notFoundPayload, "APIKEY");
            var notFound = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(404, notFound.StatusCode);
            Assert.IsType<ProblemDetails>(notFound.Value);
        }

        [Fact]
        public void ShowtimeController_Delete_NoContentOrProblemDetails()
        {
            var showtimesRepository = new FakeRepo { DeleteResult = new CNM.Domain.Database.Entities.ShowtimeEntity { Id = 5 } };
            var showtimeController = new ShowtimeController(showtimesRepository, new FakeImdbClient())
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
            var obj = Assert.IsType<ObjectResult>(notFoundResult);
            Assert.Equal(404, obj.StatusCode);
            Assert.IsType<ProblemDetails>(obj.Value);
        }

        [Fact]
        public void ErrorHandlingMiddleware_CatchesExceptionsAndWritesProblemDetails()
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
            Assert.Contains("Internal Server Error", responseText);
            Assert.Equal("application/problem+json", httpContext.Response.ContentType);
            Assert.Equal(500, httpContext.Response.StatusCode);
        }

        [Fact]
        public void RequestTimingMiddleware_LogsForShowtimeRoutes()
        {
            var loggerFactory = new LoggerFactory();
            var requestTimingLogger = loggerFactory.CreateLogger<RequestTimingMiddleware>();
            var logMessages = new List<string>();
            loggerFactory.AddProvider(new ListLoggerProvider(logMessages));
            var requestTimingLoggerWithProvider = loggerFactory.CreateLogger<RequestTimingMiddleware>();
            var requestTimingMiddleware = new RequestTimingMiddleware(async httpContext => await Task.Delay(10), requestTimingLoggerWithProvider);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/showtime/list";
            requestTimingMiddleware.Invoke(httpContext).GetAwaiter().GetResult();
            Assert.True(logMessages.Any(message => message.Contains("ShowtimeController request")));
        }

        [Fact]
        public async Task ImdbStatusBackgroundService_IncrementsCache_OnPing()
        {
            var logMessages = new List<string>();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new ListLoggerProvider(logMessages));
            var imdbClient = new FakeImdbClient { Ping = true };
            var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var backgroundService = new ImdbStatusBackgroundService(loggerFactory.CreateLogger<ImdbStatusBackgroundService>(), imdbClient, memoryCache);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(50);
            await backgroundService.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(60);
            await backgroundService.StopAsync(cancellationTokenSource.Token);

            memoryCache.TryGetValue("ImdbStatusChecks", out object countObj);
            var count = countObj is int i ? i : 0;
            Assert.True(count >= 1);
        }

        [Fact]
        public void CustomAuthenticationTokenService_Read_ParsesOrThrows()
        {
            var tokenService = new CustomAuthenticationTokenService();
            var principal = tokenService.Read(Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Read")));
            Assert.Equal("user", principal.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal("Read", principal.FindFirstValue(ClaimTypes.Role));

            Assert.Throws<ReadTokenException>(() => tokenService.Read("notbase64"));
        }

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
            var hostBuilder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(hostBuilder);
            using var host = hostBuilder.Build();
            Assert.NotNull(host.Services);
        }

        [Fact]
        public void Startup_ConfigureServices_RegistersDependencies()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var environment = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };
            var startup = new Startup(configuration, environment);

            startup.ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<Interfaces.IShowtimesRepository>());
            Assert.NotNull(serviceProvider.GetService<IImdbClient>());
            Assert.NotNull(serviceProvider.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
            Assert.NotNull(serviceProvider.GetService<IActionDescriptorCollectionProvider>());
        }

        [Fact]
        public void Startup_Configure_DoesNotThrow()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var env = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };
            var startup = new Startup(configuration, env);
            startup.ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            var appBuilder = new ApplicationBuilder(serviceProvider);
            var runtimeEnv = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };

            var exception = Record.Exception(() => startup.Configure(appBuilder, runtimeEnv));
            Assert.Null(exception);
        }

        private sealed class FakeRepo : Interfaces.IShowtimesRepository
        {
            public DomainDb.Entities.ShowtimeEntity DeleteResult { get; set; }
            public DomainDb.Entities.ShowtimeEntity UpdateResult { get; set; }
            public IEnumerable<DomainDb.Entities.ShowtimeEntity> GetCollection() => _data;
            public IEnumerable<DomainDb.Entities.ShowtimeEntity> GetCollection(Func<DomainDb.Entities.ShowtimeEntity, bool> predicate)
                => predicate == null ? _data : _data.Where(predicate);
            public DomainDb.Entities.ShowtimeEntity GetByMovie(Func<DomainDb.Entities.MovieEntity, bool> predicate) => null;
            public DomainDb.Entities.ShowtimeEntity Add(DomainDb.Entities.ShowtimeEntity showtimeEntity)
            {
                _data.Add(showtimeEntity);
                return showtimeEntity;
            }
            public DomainDb.Entities.ShowtimeEntity Update(DomainDb.Entities.ShowtimeEntity showtimeEntity) => UpdateResult;
            public DomainDb.Entities.ShowtimeEntity Delete(int id) => DeleteResult;
            private readonly List<DomainDb.Entities.ShowtimeEntity> _data = new List<DomainDb.Entities.ShowtimeEntity>
            {
                new DomainDb.Entities.ShowtimeEntity
                {
                    Id = 1,
                    StartDate = new DateTime(2020, 1, 1),
                    EndDate = new DateTime(2020, 1, 10),
                    Movie = new DomainDb.Entities.MovieEntity { Title = "Star Movie" }
                },
                new DomainDb.Entities.ShowtimeEntity
                {
                    Id = 2,
                    StartDate = new DateTime(2020, 1, 20),
                    EndDate = new DateTime(2020, 1, 25),
                    Movie = new DomainDb.Entities.MovieEntity { Title = "Another" }
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
            public string ApplicationName { get; set; } = typeof(ShowtimesUnitTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
