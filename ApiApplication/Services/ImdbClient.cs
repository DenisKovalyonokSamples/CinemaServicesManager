using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CNM.Application.Services
{
    public interface IImdbClient
    {
        Task<bool> PingAsync(); // Added: checks IMDB API availability
        Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey); // Added: fetch movie details by IMDB id
    }

    public class ImdbClient : IImdbClient
    {
        private readonly HttpClient _http;
        public ImdbClient(HttpClient http)
        {
            _http = http;
            _http.BaseAddress = new Uri("https://imdb-api.com/");
        }

        public async Task<bool> PingAsync() // Added: simple ping request
        {
            try
            {
                var resp = await _http.GetAsync("API/Top250Movies");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey) // Added: title lookup by id
        {
            var resp = await _http.GetAsync($"API/Title/{apiKey}/{imdbId}");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<ImdbTitleResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return model;
        }
    }
}
