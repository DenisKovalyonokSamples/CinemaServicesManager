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
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var pingSucceeded = await _imdbClient.PingAsync();
                    var currentCount = _memoryCache.Get<int>("ImdbStatusChecks");
                    _memoryCache.Set("ImdbStatusChecks", currentCount + 1);
                    stopwatch.Stop();
                    _logger.LogInformation("IMDB ping status: {Status} (duration={Duration}ms)", pingSucceeded, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Error pinging IMDB API (duration={Duration}ms)", stopwatch.ElapsedMilliseconds);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
