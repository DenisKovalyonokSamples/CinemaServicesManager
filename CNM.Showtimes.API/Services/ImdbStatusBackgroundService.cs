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
        private readonly IImdbClient _imdbClient;
        private readonly IMemoryCache _memoryCache;

        public ImdbStatusBackgroundService(ILogger<ImdbStatusBackgroundService> logger, IImdbClient imdbClient, IMemoryCache memoryCache)
        {
            _logger = logger;
            _imdbClient = imdbClient;
            _memoryCache = memoryCache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var pingSucceeded = await _imdbClient.PingAsync();
                    var currentCount = _memoryCache.Get<int>("ImdbStatusChecks");
                    _memoryCache.Set("ImdbStatusChecks", currentCount + 1);
                    _logger.LogInformation("IMDB ping status: {Status}", pingSucceeded);
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
