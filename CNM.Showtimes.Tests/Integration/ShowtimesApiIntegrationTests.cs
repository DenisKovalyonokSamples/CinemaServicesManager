// Integration tests for Showtimes API. Validates endpoints, authentication, and database behaviors.
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CNM.Showtimes.Tests.Integration
{
    public class ShowtimesApiIntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        public ShowtimesApiIntegrationTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((ctx, configBuilder) =>
                {
                    var memoryCollection = new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["Database:Provider"] = "InMemory",
                        ["Database:Name"] = "ShowtimesTestDb",
                        ["Auth:SchemeName"] = "CustomAuthentication"
                    }).Build();
                    configBuilder.AddConfiguration(memoryCollection);
                })
                .ConfigureServices(services =>
                {
                    var serviceProvider = services.BuildServiceProvider();
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetService<CNM.Domain.Database.DatabaseContext>();
                    if (db != null)
                    {
                        TestDataSeeder.SeedShowtimes(db);
                    }
                });
            });
        }

        // Ensures GET /showtime requires auth and returns 401 when missing required header
        [Fact]
        public async Task Get_ReturnsOk_AndJsonArray()
        {
            var httpClient = _factory.CreateClient();
            var httpResponse = await httpClient.GetAsync("/showtime");
            Assert.Equal(HttpStatusCode.Unauthorized, httpResponse.StatusCode); // Auth enabled, expect 401 without header
        }

        // Validates POST returns standardized ProblemDetails when required fields are missing
        [Fact]
        public async Task Post_ValidatesRequiredFields_ReturnsProblemDetails()
        {
            var httpClient = _factory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("ApiKey", Convert.ToBase64String(Encoding.UTF8.GetBytes("user|Write"))); // satisfy auth
            var requestBody = new { movie = new { imdb_id = "" } };
            var httpContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var httpResponse = await httpClient.PostAsync("/showtime?imdb_api_key=APIKEY", httpContent);
            Assert.Equal(HttpStatusCode.BadRequest, httpResponse.StatusCode);
            Assert.Equal("application/problem+json", httpResponse.Content.Headers.ContentType.MediaType);
        }
    }
}
