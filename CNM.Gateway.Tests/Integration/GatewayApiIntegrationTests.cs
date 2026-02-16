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
            _factory = factory;
        }

        // Unknown downstream service should return 404 from gateway
        [Fact]
        public async Task Proxy_UnknownService_ReturnsNotFound()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/unknown/whatever");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
