using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using CNM.Application.Middleware;
using CNM.Application.Auth;
using CNM.Domain.Clients;
using CNM.Gateway.API.Options;

namespace CNM.Gateway.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ICustomAuthenticationTokenService, CustomAuthenticationTokenService>();
            // Logging is configured by default in .NET Core 3.1; explicit config not required unless customizing providers.
            services.AddDomainServices(Configuration);
            services.AddOptions<GatewayOptions>()
                .Bind(Configuration.GetSection("Gateway"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.ShowtimesEndpoint), "Gateway:ShowtimesEndpoint is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.MoviesEndpoint), "Gateway:MoviesEndpoint is required");
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gateway API", Version = "v1" });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway API v1");
                });
            }

            // Correlation ID middleware
            app.Use(async (context, next) =>
            {
                var header = context.Request.Headers["X-Correlation-ID"];
                var correlationId = header.Count > 0 ? header[0] : Guid.NewGuid().ToString();
                context.Items["CorrelationId"] = correlationId;
                context.Response.Headers["X-Correlation-ID"] = correlationId;
                await next();
            });
            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseMiddleware<ErrorHandlingMiddleware>();
            app.UseMiddleware<RequestTimingMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
