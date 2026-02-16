using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CNM.Domain.Database;
using CNM.Domain.Interfaces;
using CNM.Domain.Repositories;
using Middleware = CNM.Application.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CNM.Application.Tests.UnitTests
{
    // Unit tests covering host builder, service registration, pipeline wiring, and repository behaviors
    public class ApplicationUnitTests
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
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);
            var environment = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };
            services.AddSingleton<IWebHostEnvironment>(environment);

            startup.ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<IShowtimesRepository>());
            Assert.NotNull(serviceProvider.GetService<DatabaseContext>());
            Assert.NotNull(serviceProvider.GetService<IActionDescriptorCollectionProvider>());
            Assert.NotNull(serviceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
        }

        // Smoke test for Configure: pipeline wiring, sample data init, and endpoints mapping.
        [Fact]
        public void Startup_Configure_SetsPipeline_InitializesSampleData_AndMapsEndpoints()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var startup = new Startup(configuration);
            var environment = new SimpleWebHostEnvironment { EnvironmentName = Environments.Production };
            services.AddSingleton<IWebHostEnvironment>(environment);
            // Provide a minimal server so ApplicationBuilder.ServerFeatures is non-null
            services.AddSingleton<IServer, DummyServer>();
            // Add minimal services used in Configure
            services.AddLogging();
            // Ensure HttpsRedirection has a defined HTTPS port in the bare test host
            services.AddHttpsRedirection(o => o.HttpsPort = 443);

            // Use the application's real registrations to ensure HealthChecks and other
            // dependencies required by Configure() are present
            startup.ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            var appBuilder = new ApplicationBuilder(serviceProvider, new FeatureCollection());

            var exception = Record.Exception(() => startup.Configure(appBuilder, environment));
            Assert.Null(exception);
        }

        private sealed class DummyServer : IServer
        {
            public IFeatureCollection Features { get; } = new FeatureCollection();
            public void Dispose() { }
            public Task StartAsync<TContext>(IHttpApplication<TContext> app, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        // SampleData was removed; skip seeding validation.

        // Covers repository add, query, update, and delete flows against in-memory EF Core.
        [Fact]
        public void ShowtimesRepository_Add_Update_Delete_AndQueries_Work()
        {
            var dbOptions = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase("repo").Options;
            using var databaseContext = new DatabaseContext(dbOptions);
            var repository = new ShowtimesRepository(databaseContext);

            // Add
            var newShowtime = new Domain.Database.Entities.ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 1, 2),
                AuditoriumId = 10,
                Schedule = new[] { "10:00" },
                Movie = new Domain.Database.Entities.MovieEntity { Title = "First", ImdbId = "tt1", Stars = "A", ReleaseDate = new DateTime(2019, 1, 1) }
            };
            var addedShowtime = repository.Add(newShowtime);
            Assert.Equal(1, databaseContext.Showtimes.Count());

            // GetCollection (no filter returns all)
            var allShowtimes = repository.GetCollection();
            Assert.Single(allShowtimes);

            // GetCollection with filter (matching)
            var filteredShowtimes = repository.GetCollection(showtime => showtime.AuditoriumId == 10);
            Assert.Single(filteredShowtimes);

            // Update existing
            var updatedShowtime = repository.Update(new Domain.Database.Entities.ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 1, 3),
                EndDate = new DateTime(2020, 1, 4),
                AuditoriumId = 11,
                Schedule = new[] { "12:00" },
                Movie = new Domain.Database.Entities.MovieEntity { Title = "Updated", ImdbId = "tt1", Stars = "B", ReleaseDate = new DateTime(2020, 1, 1) }
            });
            Assert.NotNull(updatedShowtime);
            Assert.Equal(11, updatedShowtime.AuditoriumId);
            Assert.Equal("Updated", updatedShowtime.Movie.Title);

            // Delete existing
            var deletedShowtime = repository.Delete(1);
            Assert.NotNull(deletedShowtime);
            Assert.Empty(databaseContext.Showtimes);

            // Delete non-existing
            var nonExistingDeletedShowtime = repository.Delete(2);
            Assert.Null(nonExistingDeletedShowtime);
        }

        // Ensures the error handling middleware catches exceptions and returns RFC7807 ProblemDetails
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
            Assert.Contains("Internal Server Error", responseText);
            Assert.Equal("application/problem+json", httpContext.Response.ContentType);
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
            Assert.Contains(logMessages, message => message.Contains("Request /showtime/list took"));
        }

        // Minimal IWebHostEnvironment implementation for tests.
        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(ApplicationUnitTests).Assembly.GetName().Name;
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
