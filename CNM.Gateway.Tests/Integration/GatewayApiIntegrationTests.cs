// Integration tests for Gateway API. Validates proxying, endpoint configuration, and downstream service handling.
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CNM.Gateway.API;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CNM.Gateway.Tests.Integration
{
    public class GatewayApiIntegrationTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        public GatewayApiIntegrationTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((ctx, configBuilder) =>
                {
                    var memoryCollection = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["Gateway:ShowtimesEndpoint"] = "http://localhost/showtimes",
                        ["Gateway:MoviesEndpoint"] = "http://localhost/movies"
                    }).Build();
                    configBuilder.AddConfiguration(memoryCollection);
                });
            });
        }

        // Unknown downstream service should return 404 from gateway
        [Fact]
        public async Task Proxy_UnknownService_ReturnsNotFound()
        {
            var httpClient = _factory.CreateClient();
            var httpResponse = await httpClient.GetAsync("/unknown/whatever");
            Assert.Equal(HttpStatusCode.NotFound, httpResponse.StatusCode);
        }
    }
}
