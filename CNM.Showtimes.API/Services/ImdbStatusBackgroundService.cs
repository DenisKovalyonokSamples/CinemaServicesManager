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
        private readonly ImdbStatusSingleton _singleton;

        public ImdbStatusBackgroundService(ILogger<ImdbStatusBackgroundService> logger, IImdbClient client, ImdbStatusSingleton singleton)
        {
            _logger = logger;
            _client = client;
            _singleton = singleton;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ok = await _client.PingAsync();
                    _singleton.Increment();
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
