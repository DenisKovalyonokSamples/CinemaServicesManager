using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CNM.Domain.Database;
using Middleware = CNM.Showtimes.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CNM.Application.Tests
{
    public class ApplicationTests
    {
        // Verifies the host builder can be created and built successfully.
        [Fact]
        public void Program_CreateHostBuilder_Builds()
        {
            var builder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(builder);
            using var host = builder.Build();
            Assert.NotNull(host.Services);
        }

        // Ensures ConfigureServices registers EF Core, repository, MVC, and health checks.
        [Fact]
        public void Startup_ConfigureServices_RegistersDependenciesAndHealthChecks()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);
            var environment = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };
            serviceCollection.AddSingleton<IWebHostEnvironment>(environment);

            startup.ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<IShowtimesRepository>());
            Assert.NotNull(serviceProvider.GetService<DatabaseContext>());
            Assert.NotNull(serviceProvider.GetService<IActionDescriptorCollectionProvider>());
            Assert.NotNull(serviceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
        }

        // Smoke test for Configure: pipeline wiring, sample data init, and endpoints mapping.
        [Fact]
        public void Startup_Configure_SetsPipeline_InitializesSampleData_AndMapsEndpoints()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);
            var environment = new SimpleWebHostEnvironment { EnvironmentName = Environments.Production };
            serviceCollection.AddSingleton<IWebHostEnvironment>(environment);
            // Add minimal services used in Configure
            serviceCollection.AddRouting();
            serviceCollection.AddControllers();
            serviceCollection.AddDbContext<DatabaseContext>(opts => opts.UseInMemoryDatabase("test"));
            serviceCollection.AddTransient<IShowtimesRepository, ShowtimesRepository>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var appBuilder = new ApplicationBuilder(serviceProvider);

            var exception = Record.Exception(() => startup.Configure(appBuilder, environment));
            Assert.Null(exception);
        }

        // SampleData was removed; skip seeding validation.

        // Covers repository add, query, update, and delete flows against in-memory EF Core.
        [Fact]
        public void ShowtimesRepository_Add_Update_Delete_AndQueries_Work()
        {
            var options = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase("repo").Options;
            using var dbContext = new DatabaseContext(options);
            var repository = new ShowtimesRepository(dbContext);

            // Add
            var newShowtime = new CNM.Domain.Database.Entities.ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 1, 2),
                AuditoriumId = 10,
                Schedule = new[] { "10:00" },
                Movie = new CNM.Domain.Database.Entities.MovieEntity { Title = "First", ImdbId = "tt1", Stars = "A", ReleaseDate = new DateTime(2019, 1, 1) }
            };
            var addedShowtime = repository.Add(newShowtime);
            Assert.Equal(1, dbContext.Showtimes.Count());

            // GetCollection (no filter returns all)
            var allShowtimes = repository.GetCollection();
            Assert.Single(allShowtimes);

            // GetCollection with filter (matching)
            var filteredShowtimes = repository.GetCollection(q => q.Any(x => x.AuditoriumId == 10));
            Assert.Single(filteredShowtimes);

            // Update existing
            var updatedShowtime = repository.Update(new CNM.Domain.Database.Entities.ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 1, 3),
                EndDate = new DateTime(2020, 1, 4),
                AuditoriumId = 11,
                Schedule = new[] { "12:00" },
                Movie = new CNM.Domain.Database.Entities.MovieEntity { Title = "Updated", ImdbId = "tt1", Stars = "B", ReleaseDate = new DateTime(2020, 1, 1) }
            });
            Assert.NotNull(updatedShowtime);
            Assert.Equal(11, updatedShowtime.AuditoriumId);
            Assert.Equal("Updated", updatedShowtime.Movie.Title);

            // Delete existing
            var deletedShowtime = repository.Delete(1);
            Assert.NotNull(deletedShowtime);
            Assert.Empty(dbContext.Showtimes);

            // Delete non-existing
            var nonExistingDeletedShowtime = repository.Delete(2);
            Assert.Null(nonExistingDeletedShowtime);
        }

        // Ensures the error handling middleware catches exceptions and returns JSON 500.
        [Fact]
        public void ErrorHandlingMiddleware_CatchesExceptionsAndWritesJson()
        {
            var logger = new LoggerFactory().CreateLogger<Middleware.ErrorHandlingMiddleware>();
            var middleware = new Middleware.ErrorHandlingMiddleware(async httpContext => throw new Exception("boom"), logger);
            var httpContext = new DefaultHttpContext();
            var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;

            var exception = Record.ExceptionAsync(() => middleware.Invoke(httpContext)).Result;
            Assert.Null(exception);
            memoryStream.Position = 0;
            var responseText = new StreamReader(memoryStream).ReadToEnd();
            Assert.Contains("internal_server_error", responseText);
            Assert.Equal("application/json", httpContext.Response.ContentType);
            Assert.Equal(500, httpContext.Response.StatusCode);
        }

        // Confirms timing middleware logs for requests under /showtime path.
        [Fact]
        public void RequestTimingMiddleware_LogsForShowtimeRoutes()
        {
            var loggerFactory = new LoggerFactory();
            var logMessages = new List<string>();
            loggerFactory.AddProvider(new ListLoggerProvider(logMessages));
            var logger = loggerFactory.CreateLogger<Middleware.RequestTimingMiddleware>();
            var middleware = new Middleware.RequestTimingMiddleware(async httpContext => await Task.Delay(10), logger);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/showtime/list";
            middleware.Invoke(httpContext).GetAwaiter().GetResult();
            Assert.True(logMessages.Any(message => message.Contains("ShowtimeController request")));
        }

        // Minimal IWebHostEnvironment implementation for tests.
        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(ApplicationTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        // Simple logger provider capturing formatted log messages to a list.
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
    }
}
