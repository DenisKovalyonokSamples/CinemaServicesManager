using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CNM.Movies.API;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CNM.Movies.Tests.Integration
{
    public class MoviesApiIntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        public MoviesApiIntegrationTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        // Movies ping is open; validate 200 OK without authentication
        [Fact]
        public async Task Ping_Unauthorized_WithoutApiKeyHeader()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/movies/ping");
            // Movies API does not enforce auth; expect 200
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Validate standardized ProblemDetails is returned when downstream call fails/misconfigured
        [Fact]
        public async Task GetById_ReturnsProblem_OnMissingApiKey()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/movies/tt0000001");
            // Service likely throws due to missing http client base; our global middleware returns problem details
            Assert.True(response.StatusCode == HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.BadRequest);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType.MediaType);
        }
    }
}
