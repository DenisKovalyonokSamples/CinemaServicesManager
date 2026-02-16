using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// no hosting dependency to keep domain decoupled

namespace CNM.Domain.Database
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, bool useInMemory)
        {
            var provider = configuration["Database:Provider"]; // e.g., InMemory
            var useInMemoryFlag = bool.TryParse(configuration["Database:UseInMemory"], out var flag) && flag;
            var dbName = configuration["Database:Name"] ?? "CinemaDb";

            services.AddDbContext<DatabaseContext>(options =>
            {
                // Use InMemory in tests/dev or when configured explicitly
                if (useInMemoryFlag || string.Equals(provider, "InMemory", System.StringComparison.OrdinalIgnoreCase) || useInMemory)
                {
                    options.UseInMemoryDatabase(dbName);
#if DEBUG
                    options.EnableSensitiveDataLogging();
#endif
                }
                else
                {
                    // Fallback to InMemory if real provider isn't configured
                    options.UseInMemoryDatabase(dbName);
                }
            });

            return services;
        }
    }
}