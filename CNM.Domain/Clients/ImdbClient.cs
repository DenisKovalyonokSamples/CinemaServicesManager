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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            try
            {
                var resp = await _http.GetAsync("API/Top250Movies");
                success = resp.IsSuccessStatusCode;
                return success;
            }
            catch
            {
                return false;
            }
            finally
            {
                stopwatch.Stop();
                System.Diagnostics.Trace.WriteLine($"ImdbClient.PingAsync duration={stopwatch.ElapsedMilliseconds}ms success={success}");
            }
        }

        public async Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            try
            {
                var resp = await _http.GetAsync($"API/Title/{apiKey}/{imdbId}");
                success = resp.IsSuccessStatusCode;
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                var model = JsonSerializer.Deserialize<ImdbTitleResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return model;
            }
            finally
            {
                stopwatch.Stop();
                System.Diagnostics.Trace.WriteLine($"ImdbClient.GetByIdAsync duration={stopwatch.ElapsedMilliseconds}ms success={success}");
            }
        }
    }
}
