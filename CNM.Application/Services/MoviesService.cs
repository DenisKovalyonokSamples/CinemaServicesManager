using System.Threading.Tasks;
using CNM.Domain.Interfaces;
using CNM.Domain.Models;

namespace CNM.Application.Services
{
    public class MoviesService
    {
        private readonly IImdbClient _imdbClient;
        public MoviesService(IImdbClient imdbClient)
        {
            _imdbClient = imdbClient;
        }

        public Task<bool> PingAsync() => _imdbClient.PingAsync();
        public Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey) => _imdbClient.GetByIdAsync(imdbId, apiKey);
    }
}