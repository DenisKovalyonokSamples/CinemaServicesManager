using CNM.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CNM.Showtimes.API.Services
{
    public class ImdbStatusBackgroundService : BackgroundService
    {
        private readonly ILogger<ImdbStatusBackgroundService> _logger;
        private readonly IImdbClient _client;
        private readonly IMemoryCache _cache;

        public ImdbStatusBackgroundService(ILogger<ImdbStatusBackgroundService> logger, IImdbClient client, IMemoryCache cache)
        {
            _logger = logger;
            _client = client;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ok = await _client.PingAsync();
                    var count = _cache.Get<int>("ImdbStatusChecks");
                    _cache.Set("ImdbStatusChecks", count + 1);
                    _logger.LogInformation("IMDB ping status: {Status}", ok);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pinging IMDB API");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
