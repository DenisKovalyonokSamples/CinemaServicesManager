using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CNM.Application.Services
{
    public class ImdbStatusBackgroundService : BackgroundService
    {
        private readonly IImdbClient _client;
        private readonly ImdbStatusSingleton _status;
        private readonly ILogger<ImdbStatusBackgroundService> _logger;
        public ImdbStatusBackgroundService(IImdbClient client, ImdbStatusSingleton status, ILogger<ImdbStatusBackgroundService> logger) // Added: DI constructor
        {
            _client = client;
            _status = status;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) // Added: periodic status update loop
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var up = await _client.PingAsync();
                    _status.Up = up;
                    _status.LastCall = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating IMDB status");
                }
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}
