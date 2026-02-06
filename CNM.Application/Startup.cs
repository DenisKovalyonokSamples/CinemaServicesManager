using DomainDb = CNM.Domain.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;

namespace CNM.Application
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
            services.AddDbContext<DomainDb.CinemaContext>((sp, options) =>
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                options.UseInMemoryDatabase("CinemaDb")
                    .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                if (env.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                }
            });
            services.AddTransient<DomainDb.IShowtimesRepository, DomainDb.ShowtimesRepository>();
            services.AddAuthorization();
            services.AddControllers()
                .AddNewtonsoftJson(opts =>
                {
                    opts.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                    {
                        NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
                    };
                });
            services.AddHealthChecks();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Application API", Version = "v1" });
            });
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
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Application API v1");
                });
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();

            // Authentication/Authorization must be ordered after routing and before endpoints
            app.UseAuthorization();

            // Seeding disabled: CNM.Showtimes.API.Database.SampleData was removed

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });
        }
    }
}
