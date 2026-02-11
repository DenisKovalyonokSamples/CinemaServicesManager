using CNM.Application.Auth;
using DomainDb = CNM.Domain.Database;
using Repositories = CNM.Domain.Repositories;
using CNM.Application.Middleware;
using CNM.Showtimes.API.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using CNM.Domain.Interfaces;
using CNM.Domain.Clients;

namespace CNM.Showtimes.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Consolidated single DbContext registration
            services.AddDbContext<DomainDb.DatabaseContext>(options =>
            {
                options.UseInMemoryDatabase("CinemaDb")
                    .EnableSensitiveDataLogging()
                    .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
            services.AddScoped<IShowtimesRepository, Repositories.ShowtimesRepository>();
            services.AddSingleton<ICustomAuthenticationTokenService, CustomAuthenticationTokenService>();
            services.AddSingleton<Services.ImdbStatusSingleton>();
            services.AddHttpClient<IImdbClient, ImdbClient>();
            services.AddHostedService<Services.ImdbStatusBackgroundService>();
            services.AddAuthentication(options =>
            {
                options.AddScheme<CustomAuthenticationHandler>(CustomAuthenticationSchemeOptions.AuthenticationScheme, CustomAuthenticationSchemeOptions.AuthenticationScheme);
                options.RequireAuthenticatedSignIn = true;
                options.DefaultScheme = CustomAuthenticationSchemeOptions.AuthenticationScheme;
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Read", policy => policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, "Read"));
                options.AddPolicy("Write", policy => policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, "Write"));
            });
            services.AddControllers()
                .AddNewtonsoftJson(opts =>
                {
                    opts.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                    {
                        NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                    };
                });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Showtimes API", Version = "v1" });
            });
            services.AddSwaggerGenNewtonsoftSupport();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Showtimes API v1");
                });
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<RequestTimingMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Startup guard: ensure CinemaContext is resolvable
            // Seeding disabled to avoid compile-time dependency on domain entity types
            // SampleData.Initialize(app);
        }
    }
}
