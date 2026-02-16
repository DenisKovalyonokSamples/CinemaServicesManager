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
            services.AddOptions();
            var imdbOptions = configuration.GetSection("Imdb").Get<ImdbOptions>() ?? new ImdbOptions { BaseUrl = "https://imdb-api.com", TimeoutSeconds = 10, RetryCount = 3 };
            if (string.IsNullOrWhiteSpace(imdbOptions.BaseUrl))
            {
                throw new System.InvalidOperationException("Imdb:BaseUrl is required");
            }

            services.AddHttpClient<IImdbClient, ImdbClient>(httpClient =>
            {
                httpClient.BaseAddress = new System.Uri((imdbOptions.BaseUrl ?? "https://imdb-api.com").TrimEnd('/') + "/");
                httpClient.Timeout = TimeSpan.FromSeconds(imdbOptions.TimeoutSeconds); // Set reasonable timeout
            })
            .AddPolicyHandler(GetRetryPolicy(imdbOptions.RetryCount)); // Apply retry/backoff policy
            return services;
        }

        // Creates retry policy for transient/429 errors
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx, 408, network errors
                .OrResult(result => (int)result.StatusCode == 429) // Too Many Requests
                .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // Exponential backoff
        }
    }
}