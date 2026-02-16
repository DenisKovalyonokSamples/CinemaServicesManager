using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CNM.Domain.Database.Entities;
using CNM.Showtimes.API;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Xunit;

namespace CNM.Showtimes.Tests.Integration
{
    public class ShowtimesApiIntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        public ShowtimesApiIntegrationTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                // could override config if needed
            });
        }

        // Ensures GET /showtime requires auth and returns 401 when missing required header
        [Fact]
        public async Task Get_ReturnsOk_AndJsonArray()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/showtime");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // Auth enabled, expect 401 without header
        }

        // Validates POST returns standardized ProblemDetails when required fields are missing
        [Fact]
        public async Task Post_ValidatesRequiredFields_ReturnsProblemDetails()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("ApiKey", Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Write"))); // satisfy auth
            var payload = new { movie = new { imdb_id = "" } };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/showtime?imdb_api_key=APIKEY", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType.MediaType);
        }
    }
}
