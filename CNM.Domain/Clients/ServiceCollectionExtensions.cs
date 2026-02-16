using CNM.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly; // Resilience policies core
using Polly.Extensions.Http; // HTTP-specific policy helpers
using System;
using System.Net.Http;

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
                client.Timeout = TimeSpan.FromSeconds(10); // Set reasonable timeout
            })
            .AddPolicyHandler(GetRetryPolicy()); // Apply retry/backoff policy
            return services;
        }

        // Creates retry policy for transient/429 errors
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx, 408, network errors
                .OrResult(result => (int)result.StatusCode == 429) // Too Many Requests
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // Exponential backoff
        }
    }
}