using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CNM.Movies.API.Services
{
    public interface IImdbClient
    {
        Task<bool> PingAsync();
        Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey);
    }

    public class ImdbClient : IImdbClient
    {
        private readonly HttpClient _http;
        public ImdbClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<bool> PingAsync()
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

        public async Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey)
        {
            var resp = await _http.GetAsync($"API/Title/{apiKey}/{imdbId}");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<ImdbTitleResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return model;
        }
    }
}
