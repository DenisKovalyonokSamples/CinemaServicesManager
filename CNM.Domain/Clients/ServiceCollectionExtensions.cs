using CNM.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CNM.Domain.Clients
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient<IImdbClient, ImdbClient>(client =>
            {
                var baseUrl = configuration["Imdb:BaseUrl"] ?? "https://imdb-api.com";
                client.BaseAddress = new System.Uri(baseUrl.TrimEnd('/') + "/");
            });
            return services;
        }
    }
}