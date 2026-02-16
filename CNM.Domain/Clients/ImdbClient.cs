using CNM.Domain.Interfaces;
using CNM.Domain.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CNM.Domain.Clients
{
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
