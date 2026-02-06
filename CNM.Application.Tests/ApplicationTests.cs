using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CNM.Application;
using CNM.Showtimes.API.Database;
using CNM.Showtimes.API.Database.Entities;
using CNM.Showtimes.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
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
        [Fact]
        public void Program_CreateHostBuilder_Builds()
        {
            var builder = Program.CreateHostBuilder(Array.Empty<string>());
            Assert.NotNull(builder);
            using var host = builder.Build();
            Assert.NotNull(host.Services);
        }

        [Fact]
        public void Startup_ConfigureServices_RegistersDependenciesAndHealthChecks()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var startup = new Startup(config);
            var env = new SimpleWebHostEnvironment { EnvironmentName = Environments.Development };
            services.AddSingleton<IWebHostEnvironment>(env);

            startup.ConfigureServices(services);
            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IShowtimesRepository>());
            Assert.NotNull(provider.GetService<CinemaContext>());
            Assert.NotNull(provider.GetService<IActionDescriptorCollectionProvider>());
            Assert.NotNull(provider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
        }

        [Fact]
        public void Startup_Configure_SetsPipeline_InitializesSampleData_AndMapsEndpoints()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var startup = new Startup(config);
            var env = new SimpleWebHostEnvironment { EnvironmentName = Environments.Production };
            services.AddSingleton<IWebHostEnvironment>(env);
            // Add minimal services used in Configure
            services.AddRouting();
            services.AddControllers();
            services.AddDbContext<CinemaContext>(opts => opts.UseInMemoryDatabase("test"));
            services.AddTransient<IShowtimesRepository, ShowtimesRepository>();
            var provider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(provider);

            var ex = Record.Exception(() => startup.Configure(app, env));
            Assert.Null(ex);
        }

        [Fact]
        public void SampleData_Initialize_PopulatesDatabase()
        {
            var services = new ServiceCollection();
            services.AddDbContext<CinemaContext>(opts => opts.UseInMemoryDatabase("sample"));
            var provider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(provider);

            SampleData.Initialize(app);

            using var scope = provider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<CinemaContext>();
            Assert.True(ctx.Auditoriums.Any());
            Assert.True(ctx.Showtimes.Any());
            Assert.True(ctx.Movies.Any());
        }

        [Fact]
        public void ShowtimesRepository_Add_Update_Delete_AndQueries_Work()
        {
            var options = new DbContextOptionsBuilder<CinemaContext>().UseInMemoryDatabase("repo").Options;
            using var ctx = new CinemaContext(options);
            var repo = new ShowtimesRepository(ctx);

            // Add
            var entity = new ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 1, 2),
                AuditoriumId = 10,
                Schedule = new[] { "10:00" },
                Movie = new MovieEntity { Title = "First", ImdbId = "tt1", Stars = "A", ReleaseDate = new DateTime(2019, 1, 1) }
            };
            var added = repo.Add(entity);
            Assert.Equal(1, ctx.Showtimes.Count());

            // GetCollection (no filter returns all)
            var all = repo.GetCollection();
            Assert.Single(all);

            // GetCollection with filter (matching)
            var filtered = repo.GetCollection(q => q.Any(x => x.AuditoriumId == 10));
            Assert.Single(filtered);

            // Update existing
            var updated = repo.Update(new ShowtimeEntity
            {
                Id = 1,
                StartDate = new DateTime(2020, 1, 3),
                EndDate = new DateTime(2020, 1, 4),
                AuditoriumId = 11,
                Schedule = new[] { "12:00" },
                Movie = new MovieEntity { Title = "Updated", ImdbId = "tt1", Stars = "B", ReleaseDate = new DateTime(2020, 1, 1) }
            });
            Assert.NotNull(updated);
            Assert.Equal(11, updated.AuditoriumId);
            Assert.Equal("Updated", updated.Movie.Title);

            // Delete existing
            var deleted = repo.Delete(1);
            Assert.NotNull(deleted);
            Assert.Empty(ctx.Showtimes);

            // Delete non-existing
            var deletedNull = repo.Delete(2);
            Assert.Null(deletedNull);
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
            var logs = new List<string>();
            factory.AddProvider(new ListLoggerProvider(logs));
            var logger = factory.CreateLogger<RequestTimingMiddleware>();
            var mw = new RequestTimingMiddleware(async ctx => await Task.Delay(10), logger);
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/showtime/list";
            mw.Invoke(ctx).GetAwaiter().GetResult();
            Assert.True(logs.Any(l => l.Contains("ShowtimeController request")));
        }

        private sealed class SimpleWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = typeof(ApplicationTests).Assembly.GetName().Name;
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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
    }
}
