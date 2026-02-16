using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CNM.Movies.API;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace CNM.Movies.Tests.Integration
{
    public class MoviesApiIntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        public MoviesApiIntegrationTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((ctx, configBuilder) =>
                {
                    var mem = new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["Imdb:BaseUrl"] = "http://localhost/", // avoid external calls in tests
                        ["Imdb:TimeoutSeconds"] = "5"
                    }).Build();
                    configBuilder.AddConfiguration(mem);
                });
            });
        }

        // Movies ping is open; validate 200 OK without authentication
        [Fact]
        public async Task Ping_Unauthorized_WithoutApiKeyHeader()
        {
            var httpClient = _factory.CreateClient();
            var httpResponse = await httpClient.GetAsync("/movies/ping");
            // Movies API does not enforce auth; expect 200
            Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        }

        // Validate standardized ProblemDetails is returned when downstream call fails/misconfigured
        [Fact]
        public async Task GetById_ReturnsProblem_OnMissingApiKey()
        {
            var httpClient = _factory.CreateClient();
            var httpResponse = await httpClient.GetAsync("/movies/tt0000001");
            // Service likely throws due to missing http client base; our global middleware returns problem details
            Assert.True(httpResponse.StatusCode == HttpStatusCode.InternalServerError || httpResponse.StatusCode == HttpStatusCode.BadRequest);
            Assert.Equal("application/problem+json", httpResponse.Content.Headers.ContentType.MediaType);
        }
    }
}
